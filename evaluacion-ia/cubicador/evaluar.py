"""Mide el motor de seleccion de flota contra el dataset dorado (S6-HU-35.1).

Corre el motor REAL (`domain.motor.seleccion_flota.recomendar`, sin reescribirlo) sobre
cada caso y calcula las cinco metricas de `metricas.py`: acierto del vehiculo, flota
minima, respeto de capacidad y volumen, latencia p95 y costo por cubicacion. Su
resultado es la linea base contra la que se compara la IA.

**No es parte del servicio ni del CI.** Se corre bajo demanda y no toca la base de
datos, la red ni ningun servicio: el motor es puro y el catalogo es el de QA
(`catalogo_qa.py`). La latencia es la del motor en proceso; la de extremo a extremo
(HTTP + base) se mide aparte, en modo sombra.

    python evaluacion-ia/cubicador/evaluar.py
    python evaluacion-ia/cubicador/evaluar.py --casos otra/carpeta --repeticiones 20

Para evaluar otro candidato (la IA) se escribe un adaptador con la forma de
`adaptador_motor`: recibe un `Caso` y devuelve una `Respuesta`.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import time
from datetime import date
from decimal import Decimal
from pathlib import Path
from typing import Callable

CUBICADOR = Path(__file__).resolve().parents[2] / "cubicador"
sys.path.insert(0, str(CUBICADOR))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from domain.entidades.catalogo import Catalogo  # noqa: E402
from domain.motor import seleccion_flota as sf  # noqa: E402

import metricas as m  # noqa: E402
from catalogo_qa import MEDIDAS_ESTANDAR, catalogo_qa  # noqa: E402

CASOS_POR_DEFECTO = Path(__file__).resolve().parent / "casos"

Adaptador = Callable[[m.Caso], m.Respuesta]


# ----------------------------------------------------------------------- dataset


def _flota(items: list[dict]) -> m.Flota:
    return tuple(sorted((i["vehiculo"], i["carroceria"]) for i in items))


def leer_caso(d: dict) -> m.Caso:
    decision = d["decisionCorrecta"]
    flotas = tuple(_flota(f) for f in decision.get("flotas", []))
    cantidad_minima = int(decision.get("cantidadMinima", len(flotas[0]) if flotas else 0))
    if decision["resultado"] == m.ASIGNADO:
        if cantidad_minima < 1:
            raise ValueError(f"El caso {d['id']} se debe asignar pero no dice cuantos vehiculos hacen falta.")
        if any(len(f) != cantidad_minima for f in flotas):
            raise ValueError(
                f"El caso {d['id']}: todas las flotas correctas deben tener la cantidad minima de vehiculos "
                f"({cantidad_minima}); una flota mayor no es otra respuesta correcta, es un exceso.")
    return m.Caso(
        id=d["id"], origen=d["origen"], etiqueta_validada=bool(d.get("etiquetaValidada")),
        descripcion=d.get("descripcion", ""), clase_viaje=d.get("claseViaje"),
        mercancias=tuple(
            m.Posicion(p["codigo"], int(p["cantidad"]), Decimal(str(p["largoCm"])), Decimal(str(p["anchoCm"])),
                       Decimal(str(p["altoCm"])), Decimal(str(p["pesoKg"])))
            for p in d["mercancias"]),
        resultado_esperado=decision["resultado"], motivo_esperado=decision.get("motivo"),
        cantidad_minima=cantidad_minima, flotas_correctas=flotas,
    )


def leer_casos(ruta: Path, incluir_sin_validar: bool = False) -> tuple[list[m.Caso], str]:
    """Todos los `*.json` de la carpeta (o el archivo). Devuelve tambien una huella del dataset."""
    archivos = sorted(ruta.glob("*.json")) if ruta.is_dir() else [ruta]
    casos: list[m.Caso] = []
    huella = hashlib.sha256()
    for archivo in archivos:
        contenido = archivo.read_bytes()
        huella.update(contenido)
        casos.extend(leer_caso(c) for c in json.loads(contenido.decode("utf-8"))["casos"])
    ids = [c.id for c in casos]
    if len(ids) != len(set(ids)):
        raise ValueError("Hay ids de caso repetidos en el dataset.")
    if not incluir_sin_validar:
        casos = [c for c in casos if c.etiqueta_validada]
    return casos, huella.hexdigest()[:12]


# ----------------------------------------------------------------------- candidatos


def adaptador_motor(catalogo: Catalogo) -> Adaptador:
    """El motor actual: mismo recorrido que `recomendar_flota`, sin base de datos de por medio."""

    def correr(caso: m.Caso) -> m.Respuesta:
        lineas = []
        for p in caso.mercancias:
            tipo = catalogo.mercancia_por_codigo(p.codigo)
            if tipo is None:
                raise ValueError(f"El caso {caso.id} usa '{p.codigo}', que no esta en el catalogo.")
            lineas.append(sf.MercanciaSolicitada(tipo, p.cantidad, p.largo_cm, p.ancho_cm, p.alto_cm, p.peso_kg))
        solicitud = sf.SolicitudRecomendacion(tuple(lineas), MEDIDAS_ESTANDAR, caso.clase_viaje)

        inicio = time.perf_counter()
        recomendacion = sf.recomendar(catalogo, solicitud)
        latencia_ms = (time.perf_counter() - inicio) * 1000

        return m.Respuesta(
            resultado=recomendacion.resultado, motivo=recomendacion.motivo_sin_seleccion,
            vehiculos=tuple(
                m.VehiculoPropuesto(v.codigo_vehiculo, v.codigo_carroceria,
                                    tuple((x.indice, x.cantidad) for x in v.mercancias))
                for v in recomendacion.vehiculos),
            latencia_ms=latencia_ms,
        )

    return correr


def correr(casos: list[m.Caso], adaptador: Adaptador, repeticiones: int) -> m.Evaluacion:
    evaluacion = m.Evaluacion()
    for caso in casos:
        evaluacion.por_caso[caso.id] = [adaptador(caso) for _ in range(repeticiones)]
    return evaluacion


# ----------------------------------------------------------------------- informe


def _pct(t: dict) -> str:
    return "n/a" if t["tasa"] is None else f"{t['tasa'] * 100:.1f} % ({t['aciertos']}/{t['total']})"


def informe_markdown(meta: dict, r: dict) -> str:
    v, f, c = r["acierto_vehiculo"], r["flota_minima"], r["respeto_de_capacidad"]
    lineas = [
        f"# Linea base del {meta['candidato']}",
        "",
        f"- Fecha: {meta['fecha']} · casos: {r['casos']} · repeticiones por caso: {meta['repeticiones']}",
        f"- Dataset: huella `{meta['huella_dataset']}` · {meta['origenes']}",
        f"- Catalogo: {meta['catalogo']}",
        "",
        "| Metrica | Resultado |",
        "|---|---|",
        f"| Decision (asignar / no asignar) | {_pct(r['acierto_decision']['resultado'])} |",
        f"| Motivo cuando no asigna | {_pct(r['acierto_decision']['motivo_cuando_no_asigna'])} |",
        f"| Acierto del vehiculo (flota exacta) | {_pct(v['flota_exacta'])} |",
        f"| Acierto de la configuracion (sin mirar carroceria) | {_pct(v['solo_configuracion'])} |",
        f"| Flota minima (cantidad exacta) | {_pct(f['cantidad_exacta'])} |",
        f"| Exceso medio de vehiculos | {f['exceso_medio']} |",
        f"| Respeta el peso | {_pct(c['peso'])} |",
        f"| Respeta el volumen | {_pct(c['volumen'])} |",
        f"| Conserva las unidades | {_pct(c['unidades_conservadas'])} |",
        f"| Latencia p50 / p95 / max | {r['latencia']['p50_ms']} / {r['latencia']['p95_ms']} / {r['latencia']['max_ms']} ms |",
        f"| Costo por cubicacion | {r['costo'].get('usd_por_cubicacion', 0)} USD (tokens de entrada/salida medios: "
        f"{r['costo'].get('tokens_entrada_medio', 0)}/{r['costo'].get('tokens_salida_medio', 0)}) |",
    ]
    if f["con_exceso"] or f["con_defecto"] or c["violaciones"]:
        lineas += ["", "## Para revisar", ""]
        lineas += [f"- Exceso: caso {x['caso']}, {x['de_mas']} vehiculo(s) de mas" for x in f["con_exceso"]]
        lineas += [f"- **Defecto**: caso {x['caso']}, {x['de_menos']} vehiculo(s) de menos" for x in f["con_defecto"]]
        lineas += [f"- **Violacion**: caso {x['caso']}, {x['vehiculo']}: {x['detalle']}" for x in c["violaciones"]]
    return "\n".join(lineas) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--casos", type=Path, default=CASOS_POR_DEFECTO, help="carpeta o archivo .json del dataset")
    ap.add_argument("--repeticiones", type=int, default=20, help="corridas por caso (para la latencia)")
    ap.add_argument("--incluir-sin-validar", action="store_true",
                    help="tambien los casos cuya etiqueta nadie ha confirmado (no usar para decidir)")
    ap.add_argument("--salida-json", type=Path, help="guardar el informe completo en JSON")
    ap.add_argument("--salida-md", type=Path, help="guardar el resumen en Markdown")
    args = ap.parse_args(argv)

    casos, huella = leer_casos(args.casos, args.incluir_sin_validar)
    if not casos:
        print("No hay casos con etiqueta validada.", file=sys.stderr)
        return 2

    catalogo = catalogo_qa()
    sf.validar_dominancia(catalogo)
    evaluacion = correr(casos, adaptador_motor(catalogo), args.repeticiones)
    resultado = m.evaluar(casos, evaluacion, catalogo)

    meta = {
        "candidato": "motor actual (seleccion_flota)", "fecha": date.today().isoformat(),
        "repeticiones": args.repeticiones, "huella_dataset": huella,
        "origenes": ", ".join(f"{n} {o}" for o, n in sorted(
            {o: sum(1 for c in casos if c.origen == o) for o in {c.origen for c in casos}}.items())),
        "catalogo": "QA calcado (5 vehiculos, 9 tipos de mercancia)",
    }
    md = informe_markdown(meta, resultado)
    print(md)
    if args.salida_md:
        args.salida_md.write_text(md, encoding="utf-8")
    if args.salida_json:
        args.salida_json.write_text(
            json.dumps({"meta": meta, "resultado": resultado}, indent=2, ensure_ascii=False), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
