"""Mide cuanto aprovecha DBLF el espacio de los vehiculos reales (S5-HU-17.3).

Responde el ultimo criterio de aceptacion de la issue: *"si Maximal Empty
Spaces resulta necesario sobre DBLF, se justifica con la medicion y se
documenta; si no, se deja constancia de que DBLF alcanzo"*.

**Contra que se compara.** Para una carga homogenea el tope alcanzable con una
sola orientacion se calcula exacto: divisiones enteras en las tres dimensiones,
probando las seis orientaciones del prisma. Eso es la "rejilla optima". Si DBLF
queda por debajo, hay espacio que no esta usando.

**Que se encontro.** Con la version inicial de DBLF - que tomaba la primera
orientacion valida, es decir la original - quedaba **6,0 puntos porcentuales
por debajo** en promedio y hasta **28,8 puntos** en el peor caso: una estiba de
120x100x150 en un Sencillo daba 48,1% contra 76,9% de la rejilla, que es pedir
dos camiones donde alcanza uno.

La causa no era fragmentacion del espacio vacio, que es lo que resolveria
Maximal Empty Spaces, sino la eleccion de orientacion: 120 cm a lo largo de un
vehiculo de 650 deja 50 cm muertos, pero girada entran 6 estibas por fila en
vez de 5. Con las orientaciones ordenadas por cuantas unidades entrarian en el
vehiculo vacio (`DeepestBottomLeftFill._orientaciones`), DBLF iguala la rejilla
optima en los 16 casos medidos. **MES no hace falta.**

Correr:

    ./.venv/Scripts/python.exe cubicador/scripts/medir_empaque_dblf.py

No toca la base: las medidas de los vehiculos estan abajo, tomadas de
`dbo_cubicador.vehiculos`.
"""

from __future__ import annotations

import itertools
import os
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, RAIZ)

from domain.motor.dominio import (  # noqa: E402
    Contenedor,
    ItemCarga,
    ParametrosOptimizacion,
    PosicionPuerta,
)
from domain.motor.optimizadores import obtener_optimizador  # noqa: E402

# Largo, ancho y alto util en cm, como estan en el catalogo.
VEHICULOS = [
    ("2L2", 450, 220, 230),
    ("C2", 650, 240, 240),
    ("C3", 800, 240, 260),
    ("2S2", 1220, 240, 260),
    ("3S3", 1220, 240, 260),
]

# Bultos de paletizado corriente en Colombia. El peso se deja en 1 kg y la
# capacidad del vehiculo en un numero enorme a proposito: lo que se mide es el
# empaque geometrico, no el filtro de peso.
CARGAS = [
    ("estiba 120x100x100", 120, 100, 100),
    ("estiba 120x100x150", 120, 100, 150),
    ("caja 60x40x40", 60, 40, 40),
    ("caja 50x40x40", 50, 40, 40),
]


def rejilla_optima(vehiculo_cm, caja_cm) -> tuple[int, tuple[int, int, int]]:
    """Mejor conteo alcanzable con una orientacion uniforme."""
    largo, ancho, alto = vehiculo_cm
    mejor, cual = 0, caja_cm
    for orientacion in set(itertools.permutations(caja_cm)):
        cantidad = (largo // orientacion[0]) * (ancho // orientacion[1]) * (alto // orientacion[2])
        if cantidad > mejor:
            mejor, cual = cantidad, orientacion
    return mejor, cual


def main() -> int:
    print(f"{'veh':5s} {'carga':22s} {'dblf':>6s} {'rejilla':>8s} {'dblf%':>7s} "
          f"{'rejilla%':>9s}  mejor orientacion")
    brechas: list[float] = []

    for codigo, largo, ancho, alto in VEHICULOS:
        contenedor = Contenedor(
            largo_mm=largo * 10, ancho_mm=ancho * 10, alto_mm=alto * 10,
            capacidad_kg=1e9, tara_kg=0.0, puerta_ancho_mm=ancho * 10,
            puerta_alto_mm=alto * 10, puerta=PosicionPuerta.TRASERA, ejes=[],
        )
        for nombre, caja_l, caja_a, caja_h in CARGAS:
            tope, orientacion = rejilla_optima((largo, ancho, alto), (caja_l, caja_a, caja_h))
            items = [
                ItemCarga(
                    id=f"i{n}", sku="X", descripcion=nombre, pedido=None, cliente=None,
                    destino=None, secuencia_entrega=1, largo_mm=caja_l * 10,
                    ancho_mm=caja_a * 10, alto_mm=caja_h * 10, peso_kg=1.0,
                )
                for n in range(int(tope * 1.3) + 5)  # se ofrecen mas de los que caben
            ]
            colocadas, _ = obtener_optimizador("dblf").optimizar(
                items, contenedor, ParametrosOptimizacion())

            volumen_caja = caja_l * caja_a * caja_h
            volumen_vehiculo = largo * ancho * alto
            pct_dblf = len(colocadas) * volumen_caja / volumen_vehiculo * 100
            pct_tope = tope * volumen_caja / volumen_vehiculo * 100
            brechas.append(pct_tope - pct_dblf)
            print(f"{codigo:5s} {nombre:22s} {len(colocadas):6d} {tope:8d} "
                  f"{pct_dblf:6.1f}% {pct_tope:8.1f}%  {orientacion}")

    promedio = sum(brechas) / len(brechas)
    peor = max(brechas)
    print(f"\nBrecha contra la rejilla optima: promedio {promedio:.1f} puntos, peor {peor:.1f}.")
    if promedio < 0.5:
        print("DBLF alcanza: no hace falta Maximal Empty Spaces.")
        return 0
    print("DBLF deja espacio sin usar: revisar antes de descartar MES.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
