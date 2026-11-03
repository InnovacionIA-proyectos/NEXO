"""Mide la recomendacion de la IA del modo sombra contra el dataset dorado (S6-HU-35.5, #370).

Es el adaptador que `evaluar.py` deja previsto para "otro candidato": arma la peticion
EXACTAMENTE como el modo sombra en produccion (`application/ia_sombra`: mismo prompt,
mismo esquema de salida, misma lectura y validacion de la propuesta, misma configuracion
de modelo y esfuerzo) y la califica con las mismas metricas que la linea base del motor.

Asi mide lo que el modo sombra no puede: el **acierto** contra respuestas validadas, no
solo el parecido con el motor.

Como la evaluacion de la interpretacion, **esto si llama al modelo**: `--max-llamadas` y
`--max-tokens` abortan la corrida en vez de dejarla gastar, y `--simular` arma los
mensajes sin llamar a nadie.

    # gratis: revisa que los mensajes salgan bien
    python evaluacion-ia/cubicador/evaluar_ia.py --simular

    # de verdad (lee ANTHROPIC_API_KEY de cubicador/.env o del entorno)
    python evaluacion-ia/cubicador/evaluar_ia.py --salida-md evaluacion-ia/cubicador/informe_ia_semilla.md
"""

from __future__ import annotations

import argparse
import asyncio
import json
import sys
import time
from datetime import date
from pathlib import Path

import evaluar as ev
import evaluar_interpretacion as ei
import metricas as m

from application.ia_sombra import prompt  # noqa: E402
from application.ia_sombra.configuracion import ConfiguracionSombra  # noqa: E402
from application.ia_sombra.propuesta import RespuestaInvalida, leer_propuesta  # noqa: E402
from application.ports.ia import IaNoDisponible  # noqa: E402
from application.recomendaciones.casos_de_uso import LineaMercancia  # noqa: E402
from catalogo_qa import MEDIDAS_ESTANDAR, catalogo_qa  # noqa: E402
from domain.entidades.catalogo import Catalogo  # noqa: E402


def configuracion(modelo: str | None, esfuerzo: str | None) -> ConfiguracionSombra:
    """La configuracion del modo sombra en produccion; modelo y esfuerzo se pueden cambiar para comparar."""
    api_key = ei._leer_env("ANTHROPIC_API_KEY")
    if not api_key:
        raise SystemExit(
            "No hay ANTHROPIC_API_KEY (ni en el entorno ni en cubicador/.env).\n"
            "Para revisar los mensajes sin llamar a nadie: --simular")
    base = ConfiguracionSombra()
    return ConfiguracionSombra(
        habilitada=True, api_key=api_key,
        workspace_id=ei._leer_env("ANTHROPIC_WORKSPACE_ID") or None,
        modelo=modelo or ei._leer_env("ANTHROPIC_MODEL") or base.modelo,
        esfuerzo=esfuerzo or ei._leer_env("ANTHROPIC_EFFORT") or base.esfuerzo,
        timeout_segundos=base.timeout_segundos, max_tokens_salida=base.max_tokens_salida)


def adaptador_ia(catalogo: Catalogo, cliente, errores: list[dict]) -> ev.Adaptador:
    """La IA con el mismo recorrido que `ServicioSombra._comparar`, sin base de datos de por medio."""
    esquema = prompt.esquema_salida(catalogo)

    def correr(caso: m.Caso) -> m.Respuesta:
        lineas = [LineaMercancia(p.codigo, p.cantidad, p.largo_cm, p.ancho_cm, p.alto_cm, p.peso_kg)
                  for p in caso.mercancias]
        mensaje = prompt.construir_mensaje(catalogo, lineas, MEDIDAS_ESTANDAR, caso.clase_viaje)

        inicio = time.perf_counter()
        try:
            respuesta = asyncio.run(cliente.completar(prompt.INSTRUCCIONES, mensaje, esquema))
        except IaNoDisponible as ex:
            errores.append({"caso": caso.id, "estado": ex.codigo})
            return m.Respuesta("ERROR", ex.codigo, (), (time.perf_counter() - inicio) * 1000)
        tokens = {"tokens_entrada": respuesta.tokens_entrada + respuesta.tokens_cache, "tokens_salida": respuesta.tokens_salida}
        try:
            propuesta = leer_propuesta(respuesta.json, catalogo, len(lineas))
        except RespuestaInvalida as ex:
            errores.append({"caso": caso.id, "estado": "IA_RESPUESTA_INVALIDA", "detalle": str(ex)})
            return m.Respuesta("ERROR", "IA_RESPUESTA_INVALIDA", (), respuesta.latencia_ms, **tokens)

        return m.Respuesta(
            resultado=propuesta.resultado, motivo=propuesta.motivo,
            vehiculos=tuple(m.VehiculoPropuesto(v.codigo_vehiculo, v.codigo_carroceria,
                                                tuple((c.indice, c.cantidad) for c in v.carga))
                            for v in propuesta.vehiculos),
            latencia_ms=respuesta.latencia_ms, **tokens)

    return correr


def informe(meta: dict, resultado: dict, errores: list[dict], contador: ei.Contador) -> str:
    md = ev.informe_markdown(meta, resultado).replace("# Linea base del", "# Evaluacion de la")
    extra = ["", "## Corrida", "",
             f"- Llamadas al modelo: {contador.llamadas} · tokens de entrada/salida: "
             f"{contador.tokens_entrada}/{contador.tokens_salida} · costo aproximado: US${contador.usd:.4f}",
             f"- Precio de referencia: US${ei.USD_ENTRADA}/US${ei.USD_SALIDA} por millon de tokens de entrada/salida."]
    if errores:
        extra += ["", "## Llamadas que no dieron una propuesta", ""]
        extra += [f"- caso {e['caso']}: {e['estado']}{' (' + e['detalle'] + ')' if e.get('detalle') else ''}" for e in errores]
    return md + "\n".join(extra) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--casos", type=Path, default=ev.CASOS_POR_DEFECTO, help="carpeta o archivo .json del dataset")
    ap.add_argument("--repeticiones", type=int, default=1, help="corridas por caso (cada una es una llamada)")
    ap.add_argument("--solo", help="corre un caso por id (por ejemplo S-03)")
    ap.add_argument("--modelo", help="por defecto, el del modo sombra (ANTHROPIC_MODEL)")
    ap.add_argument("--esfuerzo", help="por defecto, el del modo sombra (ANTHROPIC_EFFORT)")
    ap.add_argument("--simular", action="store_true", help="arma los mensajes y no llama a nadie (gratis)")
    ap.add_argument("--max-llamadas", type=int, default=0, help="0 = casos x repeticiones")
    ap.add_argument("--max-tokens", type=int, default=500_000)
    ap.add_argument("--salida-md", type=Path)
    ap.add_argument("--salida-json", type=Path)
    args = ap.parse_args(argv)

    casos, huella = ev.leer_casos(args.casos)
    if args.solo:
        casos = [c for c in casos if c.id == args.solo]
    if not casos:
        print("No hay casos con etiqueta validada.", file=sys.stderr)
        return 2

    catalogo = catalogo_qa()
    contador = ei.Contador(args.max_llamadas or len(casos) * args.repeticiones, args.max_tokens)

    if args.simular:
        cliente = ei.ClienteQueNoLlama()
        esquema = prompt.esquema_salida(catalogo)
        for caso in casos:
            lineas = [LineaMercancia(p.codigo, p.cantidad, p.largo_cm, p.ancho_cm, p.alto_cm, p.peso_kg) for p in caso.mercancias]
            asyncio.run(cliente.completar(prompt.INSTRUCCIONES, prompt.construir_mensaje(catalogo, lineas, MEDIDAS_ESTANDAR, caso.clase_viaje), esquema))
        print(f"{len(cliente.mensajes)} mensajes armados. Primero:\n{cliente.mensajes[0]}")
        return 0

    config = configuracion(args.modelo, args.esfuerzo)
    from infrastructure.ia.anthropic_cliente import AnthropicClienteIa
    cliente = ei.ClienteConTope(AnthropicClienteIa(config), contador)

    errores: list[dict] = []
    try:
        evaluacion = ev.correr(casos, adaptador_ia(catalogo, cliente, errores), args.repeticiones)
    except ei.TopeAlcanzado as ex:
        print(f"Corrida cortada: {ex}.", file=sys.stderr)
        return 3
    resultado = m.evaluar(casos, evaluacion, catalogo, ei.USD_ENTRADA, ei.USD_SALIDA)

    meta = {
        "candidato": f"IA del modo sombra ({config.modelo}, esfuerzo {config.esfuerzo}, prompt v{prompt.VERSION})",
        "fecha": date.today().isoformat(), "repeticiones": args.repeticiones, "huella_dataset": huella,
        "origenes": ", ".join(f"{n} {o}" for o, n in sorted(
            {o: sum(1 for c in casos if c.origen == o) for o in {c.origen for c in casos}}.items())),
        "catalogo": "QA calcado (5 vehiculos, 9 tipos de mercancia)",
    }
    md = informe(meta, resultado, errores, contador)
    print(md)
    if args.salida_md:
        args.salida_md.write_text(md, encoding="utf-8")
    if args.salida_json:
        args.salida_json.write_text(json.dumps(
            {"meta": meta, "resultado": resultado, "errores": errores,
             "llamadas": contador.llamadas, "tokens": [contador.tokens_entrada, contador.tokens_salida]},
            indent=2, ensure_ascii=False, default=str), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
