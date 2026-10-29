"""Mide la interpretacion de descripciones libres contra el dataset (S6-HU-35.3, #368).

A diferencia de `evaluar.py`, que corre el motor y nunca sale a la red, **esto si
llama al modelo**. De ahi los topes: `--max-llamadas` y `--max-tokens` abortan la
corrida en vez de dejarla gastar, y `--simular` arma los mensajes sin llamar a
nadie, que es como se depura el prompt gratis. El por que esta en el README.

    # gratis: revisa que los mensajes salgan bien
    python evaluar_interpretacion.py --simular

    # de verdad (lee ANTHROPIC_API_KEY de cubicador/.env)
    python evaluar_interpretacion.py --salida-md informe_interpretacion.md

Que se mide:
  - **casos exactos**: las posiciones coinciden una a una con las esperadas;
  - **acierto por campo**: codigo, cantidad, las tres medidas y el peso por
    separado, porque no fallan igual -confundir un codigo no es lo mismo que
    equivocar una conversion de metros-;
  - **rechazos**: de los casos R-nn, cuantos se rechazaron y con el motivo justo.
    Este es el criterio que de verdad importa: inventar un peso que el texto no
    daba es peor que no responder.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import re
import sys
from dataclasses import dataclass
from datetime import date
from decimal import Decimal
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
CUBICADOR = RAIZ / "cubicador"
sys.path.insert(0, str(CUBICADOR))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from application.interpretacion.lectura import Interpretacion  # noqa: E402
from application.interpretacion.servicio import ServicioInterpretacion  # noqa: E402
from application.ia_sombra.configuracion import ConfiguracionSombra  # noqa: E402
from application.ports.ia import IaNoDisponible, RespuestaIa  # noqa: E402

from catalogo_qa import catalogo_qa  # noqa: E402

CASOS = Path(__file__).resolve().parent / "casos" / "interpretacion_sintetica.json"
CAMPOS = ("codigo", "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg")

# Referencia de precio del ADR (seccion 6) para `claude-sonnet-5-5`, en USD por
# millon de tokens. Es para dar una idea del gasto, no una factura.
USD_ENTRADA, USD_SALIDA = 2.0, 10.0


class TopeAlcanzado(RuntimeError):
    """Se corto la corrida. Es un corte limpio y a proposito, no un fallo del modelo."""


@dataclass
class Contador:
    """Los topes. Que aborten y no solo avisen es el punto: un bucle que
    reintenta no se nota hasta que llega la factura."""

    max_llamadas: int
    max_tokens: int
    llamadas: int = 0
    tokens_entrada: int = 0
    tokens_salida: int = 0

    @property
    def tokens(self) -> int:
        return self.tokens_entrada + self.tokens_salida

    @property
    def usd(self) -> float:
        return self.tokens_entrada / 1e6 * USD_ENTRADA + self.tokens_salida / 1e6 * USD_SALIDA

    def antes_de_llamar(self) -> None:
        if self.llamadas >= self.max_llamadas:
            raise TopeAlcanzado(f"tope de {self.max_llamadas} llamadas alcanzado")
        if self.tokens >= self.max_tokens:
            raise TopeAlcanzado(f"tope de {self.max_tokens} tokens alcanzado ({self.tokens} usados)")
        self.llamadas += 1

    def sumar(self, entrada: int, salida: int) -> None:
        self.tokens_entrada += entrada
        self.tokens_salida += salida


class ClienteQueNoLlama:
    """Para `--simular`: guarda lo que se le habria mandado y devuelve vacio."""

    def __init__(self) -> None:
        self.mensajes: list[str] = []

    async def completar(self, instrucciones: str, mensaje: str, esquema_salida: dict) -> RespuestaIa:
        self.mensajes.append(mensaje)
        # Cuatro caracteres por token es la regla de bolsillo de siempre: no es
        # exacta, pero basta para ver si un prompt se inflo.
        aprox = (len(instrucciones) + len(mensaje)) // 4
        return RespuestaIa(json.dumps({"posiciones": [], "rechazos": []}), "simulado", aprox, 0, 0)


class ClienteConTope:
    """Envuelve al cliente real para que los topes se apliquen aunque el
    servicio llame mas veces de las previstas."""

    def __init__(self, interno, contador: Contador) -> None:
        self._interno = interno
        self._contador = contador

    async def completar(self, instrucciones: str, mensaje: str, esquema_salida: dict) -> RespuestaIa:
        self._contador.antes_de_llamar()
        r = await self._interno.completar(instrucciones, mensaje, esquema_salida)
        self._contador.sumar(r.tokens_entrada, r.tokens_salida)
        return r


def _leer_env(clave: str) -> str:
    """Lee `cubicador/.env` sin dependencias: el entorno manda si ya esta puesto."""
    if os.environ.get(clave):
        return os.environ[clave]
    archivo = CUBICADOR / ".env"
    if not archivo.exists():
        return ""
    m = re.search(rf"^{re.escape(clave)}=(.*)$", archivo.read_text(encoding="utf-8", errors="replace"), re.M)
    return m.group(1).strip() if m else ""


def cliente_real(contador: Contador):
    from infrastructure.ia.anthropic_cliente import AnthropicClienteIa

    api_key = _leer_env("ANTHROPIC_API_KEY")
    if not api_key:
        raise SystemExit(
            "No hay ANTHROPIC_API_KEY (ni en el entorno ni en cubicador/.env).\n"
            "Para revisar los mensajes sin llamar a nadie: --simular")
    config = ConfiguracionSombra(
        habilitada=True, api_key=api_key,
        workspace_id=_leer_env("ANTHROPIC_WORKSPACE_ID") or None,
        modelo=_leer_env("ANTHROPIC_MODEL") or ConfiguracionSombra.modelo,
        esfuerzo=_leer_env("ANTHROPIC_EFFORT") or ConfiguracionSombra.esfuerzo,
        max_tokens_salida=int(_leer_env("IA_SOMBRA_MAX_TOKENS_SALIDA") or 1024))
    return ClienteConTope(AnthropicClienteIa(config), contador), config.modelo


# --------------------------------------------------------------- comparacion

def _normalizar(p: dict) -> tuple:
    return (p["codigo"], int(p["cantidad"]),
            *(Decimal(str(p[c])) for c in ("largoCm", "anchoCm", "altoCm", "pesoKg")))


def _obtenidas(interpretacion: Interpretacion) -> list[dict]:
    return [{"codigo": p.codigo_mercancia, "cantidad": p.cantidad, "largoCm": p.largo_cm,
             "anchoCm": p.ancho_cm, "altoCm": p.alto_cm, "pesoKg": p.peso_kg}
            for p in interpretacion.posiciones]


def comparar(esperadas: list[dict], obtenidas: list[dict]) -> dict:
    """Empareja por orden de clave, no por posicion: el modelo puede listarlas
    en otro orden y eso no es un error."""
    e = sorted(esperadas, key=lambda p: _normalizar(p))
    o = sorted(obtenidas, key=lambda p: _normalizar(p))
    campos = {c: 0 for c in CAMPOS}
    for esperada, obtenida in zip(e, o):
        for c in CAMPOS:
            izq, der = esperada[c], obtenida[c]
            if c == "codigo":
                igual = izq == der
            elif c == "cantidad":
                igual = int(izq) == int(der)
            else:
                igual = Decimal(str(izq)) == Decimal(str(der))
            campos[c] += int(igual)
    return {
        "exacto": len(e) == len(o) and all(_normalizar(x) == _normalizar(y) for x, y in zip(e, o)),
        "esperadas": len(e), "obtenidas": len(o),
        "campos_ok": campos, "campos_comparados": min(len(e), len(o)),
    }


async def correr(casos: list[dict], servicio: ServicioInterpretacion, contador: Contador) -> list[dict]:
    resultados = []
    for caso in casos:
        fila = {"id": caso["id"], "derivadoDe": caso.get("derivadoDe"), "esperaRechazo": caso.get("esperaRechazo")}
        try:
            r = await servicio.interpretar(caso["texto"])
        except TopeAlcanzado:
            raise
        except IaNoDisponible as ex:
            fila["error"] = f"{ex.codigo}: {ex}"
            resultados.append(fila)
            continue
        except Exception as ex:  # noqa: BLE001 - un caso malo no tumba la corrida
            fila["error"] = f"{type(ex).__name__}: {ex}"
            resultados.append(fila)
            continue

        interpretacion = r.interpretacion
        fila["motivos"] = [x.motivo for x in interpretacion.rechazos]
        esperado = caso.get("esperaRechazo")
        if esperado:
            # Lo que se pide es que NO invente: ninguna posicion y el motivo justo.
            fila["rechazo_ok"] = esperado in fila["motivos"] and not interpretacion.posiciones
            fila["invento"] = bool(interpretacion.posiciones)
        else:
            fila.update(comparar(caso["posicionesCorrectas"], _obtenidas(interpretacion)))
        resultados.append(fila)
    return resultados


# ----------------------------------------------------------------- el informe

def informe(meta: dict, resultados: list[dict]) -> str:
    con_posiciones = [r for r in resultados if "exacto" in r]
    de_rechazo = [r for r in resultados if "rechazo_ok" in r]
    con_error = [r for r in resultados if "error" in r]

    exactos = sum(r["exacto"] for r in con_posiciones)
    rechazos_ok = sum(r["rechazo_ok"] for r in de_rechazo)
    inventados = [r["id"] for r in de_rechazo if r.get("invento")]

    campos = {c: 0 for c in CAMPOS}
    comparados = 0
    for r in con_posiciones:
        comparados += r["campos_comparados"]
        for c in CAMPOS:
            campos[c] += r["campos_ok"][c]

    def pct(n, d):
        return f"{n}/{d} ({n / d * 100:.0f} %)" if d else "sin casos"

    lineas = [
        "# Interpretacion de descripciones libres — informe", "",
        *(f"- **{k}**: {v}" for k, v in meta.items()), "",
        "## Resultado", "",
        f"- Casos exactos: **{pct(exactos, len(con_posiciones))}**",
        f"- Rechazos correctos: **{pct(rechazos_ok, len(de_rechazo))}**",
        f"- Casos con error: {len(con_error)}", "",
    ]
    if inventados:
        lineas += [
            f"> **Invento datos en {len(inventados)} caso(s)**: {', '.join(inventados)}.",
            "> Es el fallo que mas importa: devolvio posiciones donde el texto no daba los datos.", ""]
    lineas += ["## Acierto por campo", "", "| Campo | Acierto |", "|---|---|"]
    lineas += [f"| {c} | {pct(campos[c], comparados)} |" for c in CAMPOS]
    lineas += ["", "## Caso por caso", "", "| Caso | Esperado | Resultado |", "|---|---|---|"]
    for r in resultados:
        if "error" in r:
            estado = f"error — {r['error']}"
            esperado = "—"
        elif "rechazo_ok" in r:
            esperado = f"rechazo {r['esperaRechazo']}"
            estado = "ok" if r["rechazo_ok"] else f"dio {r['motivos'] or 'nada'}"
        else:
            esperado = f"{r['esperadas']} posicion(es)"
            estado = "exacto" if r["exacto"] else f"{r['obtenidas']} obtenida(s), no coincide"
        lineas.append(f"| {r['id']} | {esperado} | {estado} |")
    return "\n".join(lineas) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--casos", type=Path, default=CASOS)
    ap.add_argument("--simular", action="store_true", help="arma los mensajes y no llama a nadie (gratis)")
    ap.add_argument("--solo", help="corre un caso por id (por ejemplo I-07), para afinar el prompt")
    ap.add_argument("--max-llamadas", type=int, default=0, help="0 = una por caso")
    ap.add_argument("--max-tokens", type=int, default=500_000)
    ap.add_argument("--salida-md", type=Path)
    ap.add_argument("--salida-json", type=Path)
    args = ap.parse_args(argv)

    datos = json.loads(args.casos.read_text(encoding="utf-8"))
    casos = [c for c in datos["casos"] if not args.solo or c["id"] == args.solo]
    if not casos:
        print(f"No hay casos que correr (--solo {args.solo}).", file=sys.stderr)
        return 2

    # Por defecto, exactamente una llamada por caso: cualquier reintento que
    # nadie penso aborta en vez de multiplicar la corrida.
    contador = Contador(args.max_llamadas or len(casos), args.max_tokens)
    catalogo = catalogo_qa()
    if args.simular:
        cliente, modelo = ClienteQueNoLlama(), "simulado"
    else:
        cliente, modelo = cliente_real(contador)

    servicio = ServicioInterpretacion(cliente, catalogo)
    try:
        resultados = asyncio.run(correr(casos, servicio, contador))
    except TopeAlcanzado as ex:
        print(f"CORRIDA ABORTADA: {ex}.\nLlevaba {contador.llamadas} llamadas y "
              f"{contador.tokens} tokens (~US${contador.usd:.2f}).", file=sys.stderr)
        return 3

    meta = {
        "fecha": date.today().isoformat(), "modelo": modelo, "casos": len(casos),
        "llamadas": contador.llamadas, "tokens": f"{contador.tokens_entrada} entrada / {contador.tokens_salida} salida",
        "costo estimado": f"US${contador.usd:.2f}",
        "dataset": args.casos.name,
    }
    if args.simular:
        meta["aviso"] = "SIMULACION: no se llamo al modelo, los resultados no significan nada"

    md = informe(meta, resultados)
    print(md)
    if args.salida_md:
        args.salida_md.write_text(md, encoding="utf-8")
    if args.salida_json:
        args.salida_json.write_text(
            json.dumps({"meta": meta, "resultados": resultados}, indent=2, ensure_ascii=False, default=str),
            encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
