"""Convierte el resultado de `extraer_cargas_reales.sql` en casos del dataset dorado.

    python evaluacion-ia/cubicador/importar_cargas_reales.py cargas.csv casos/reales_por_validar.json

Cada carga real queda como un caso con la decision que tomo la persona (el vehiculo de cada
linea) **como etiqueta candidata**, no como verdad: `etiquetaValidada` nace en `false` y el
evaluador ignora esos casos hasta que un experto de operaciones los revise (ver
`docs/ia/ESTUDIO-CUBICADOR-IA.md`, seccion "Como se arma el dataset dorado").

Por que no se da la decision registrada por buena sin mas:
  - si la linea traia la recomendacion del motor, la persona pudo aceptarla sin pensarla, y
    medir al motor contra su propia sugerencia lo dejaria siempre bien. Esos casos salen
    marcados con `usoRecomendacion` para poder separarlos;
  - la persona pudo pedir mas vehiculos de los necesarios, o uno que el Cubicador no tiene.

El archivo que resulta tiene datos de operacion del negocio: NO se versiona (ver `.gitignore`).
"""

from __future__ import annotations

import csv
import io
import json
import sys
import unicodedata
from collections import defaultdict
from pathlib import Path

CUBICADOR = Path(__file__).resolve().parents[2] / "cubicador"
sys.path.insert(0, str(CUBICADOR))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from catalogo_qa import catalogo_qa  # noqa: E402

METODO = "decision humana registrada en la linea; pendiente de revision por un experto"


def _normalizar(texto: str) -> str:
    sin_tildes = "".join(c for c in unicodedata.normalize("NFD", texto) if unicodedata.category(c) != "Mn")
    return "_".join(sin_tildes.upper().split())


def _numero(valor: str) -> float:
    return float(valor.replace(",", ".")) if valor else 0.0


def importar(contenido: str, separador: str = ",") -> list[dict]:
    """Agrupa las filas por carga y arma un caso por cada una."""
    catalogo = catalogo_qa()
    configuraciones = {v.codigo for v in catalogo.vehiculos}
    carrocerias = {c.codigo for c in catalogo.carrocerias}

    filas = [f for f in csv.DictReader(io.StringIO(contenido), delimiter=separador)
             if f.get("CargaId") and not set(f["CargaId"]) <= {"-"}]
    por_carga: dict[str, list[dict]] = defaultdict(list)
    for fila in filas:
        por_carga[fila["CargaId"].strip()].append(fila)

    casos = []
    for carga_id, grupo in por_carga.items():
        vehiculos: dict[str, tuple[str, str]] = {}
        uso_recomendacion = False
        for fila in grupo:
            vehiculos[fila["NumeroLinea"]] = (fila["VehiculoElegido"].strip().upper(), _normalizar(fila["CarroceriaElegida"]))
            uso_recomendacion = uso_recomendacion or fila["UsoRecomendacion"].strip() in ("1", "true", "True")

        observaciones = []
        for config, carroceria in sorted(set(vehiculos.values())):
            if config not in configuraciones:
                observaciones.append(f"el vehiculo {config} no existe en el catalogo del Cubicador")
            elif carroceria not in carrocerias:
                observaciones.append(f"la carroceria {carroceria} no existe en el catalogo del Cubicador")
            elif catalogo.vehiculo_por_codigo(config).capacidad_con(carroceria) is None:
                observaciones.append(f"el {config} no admite la carroceria {carroceria} en el catalogo")

        flota = [{"vehiculo": c, "carroceria": k} for c, k in sorted(vehiculos.values())]
        casos.append({
            "id": f"R-{carga_id}", "origen": "real", "etiquetaValidada": False, "etiquetaMetodo": METODO,
            "descripcion": "", "claseViaje": (grupo[0].get("ClaseViaje") or "").strip() or None,
            "usoRecomendacion": uso_recomendacion, "observaciones": observaciones,
            "mercancias": [
                {"codigo": f["CodigoTipoMercancia"].strip(), "cantidad": int(f["Cantidad"]),
                 "largoCm": _numero(f["LargoCm"]), "anchoCm": _numero(f["AnchoCm"]),
                 "altoCm": _numero(f["AltoCm"]), "pesoKg": _numero(f["PesoKg"])}
                for f in grupo],
            "decisionCorrecta": {
                "resultado": "ASIGNADO", "motivo": None, "cantidadMinima": len(flota), "flotas": [flota]},
        })
    return sorted(casos, key=lambda c: c["id"])


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    casos = importar(Path(argv[1]).read_text(encoding="utf-8-sig"))
    Path(argv[2]).write_text(json.dumps({"version": "1", "casos": casos}, indent=2, ensure_ascii=False), encoding="utf-8")
    sin_motor = sum(1 for c in casos if not c["usoRecomendacion"])
    con_obs = sum(1 for c in casos if c["observaciones"])
    print(f"{len(casos)} cargas ({sin_motor} elegidas sin la recomendacion del motor, {con_obs} con observaciones). "
          "Todas con etiquetaValidada=false: falta la revision del experto.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
