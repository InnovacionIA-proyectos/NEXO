"""Compara lo que propuso el modelo con lo que recomendo el motor.

Las cuentas son las mismas del estudio comparativo (`evaluacion-ia/cubicador/
metricas.py`): flota igual, misma configuracion sin mirar la carroceria, cantidad
de vehiculos, y respeto de peso, volumen y unidades **recalculado** con el
catalogo, sin creerle al modelo. Aqui es una comparacion por llamada; el estudio
es la agregada sobre un dataset.

Funcion pura: no llama al modelo ni a la base.
"""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal

from application.ia_sombra.propuesta import RESULTADO_ASIGNADO, PropuestaIa
from application.recomendaciones.casos_de_uso import LineaMercancia
from domain.entidades.catalogo import Catalogo
from domain.entidades.recomendacion import RecomendacionGuardada

Flota = tuple[tuple[str, str], ...]


@dataclass(frozen=True)
class Comparacion:
    resultado_motor: str
    resultado_ia: str
    motivo_motor: str | None
    motivo_ia: str | None
    flota_motor: Flota
    flota_ia: Flota
    coincide_resultado: bool
    coincide_flota: bool
    coincide_configuracion: bool
    # None cuando el modelo no asigno vehiculos: no hay nada que verificar.
    ia_respeta_peso: bool | None
    ia_respeta_volumen: bool | None
    ia_conserva_unidades: bool | None
    violaciones: tuple[str, ...]

    @property
    def cantidad_motor(self) -> int:
        return len(self.flota_motor)

    @property
    def cantidad_ia(self) -> int:
        return len(self.flota_ia)


def _motivo(valor: str | None) -> str | None:
    # El motor guarda "NINGUNO" cuando asigna.
    return None if valor in (None, "", "NINGUNO") else valor


def comparar(
    catalogo: Catalogo, lineas: list[LineaMercancia], motor: RecomendacionGuardada, ia: PropuestaIa,
) -> Comparacion:
    flota_motor: Flota = tuple(sorted((v.codigo_vehiculo, v.codigo_carroceria) for v in motor.vehiculos))
    flota_ia: Flota = tuple(sorted(v.par for v in ia.vehiculos))

    peso_ok: bool | None = None
    volumen_ok: bool | None = None
    unidades_ok: bool | None = None
    violaciones: list[str] = []

    if ia.resultado == RESULTADO_ASIGNADO:
        peso_ok = volumen_ok = True
        repartidas: dict[int, int] = {}
        for n, v in enumerate(ia.vehiculos, start=1):
            ficha = catalogo.vehiculo_por_codigo(v.codigo_vehiculo)
            capacidad = ficha.capacidad_con(v.codigo_carroceria) if ficha else None
            if ficha is None or capacidad is None:
                peso_ok = volumen_ok = False
                violaciones.append(f"vehiculo {n}: {v.codigo_vehiculo}/{v.codigo_carroceria} no es una combinacion del catalogo")
                continue
            peso = sum((lineas[c.indice].peso_kg * c.cantidad for c in v.carga), Decimal(0))
            volumen = sum(
                (lineas[c.indice].largo_cm * lineas[c.indice].ancho_cm * lineas[c.indice].alto_cm / Decimal(1_000_000) * c.cantidad
                 for c in v.carga), Decimal(0))
            for c in v.carga:
                repartidas[c.indice] = repartidas.get(c.indice, 0) + c.cantidad
            if peso > capacidad:
                peso_ok = False
                violaciones.append(f"vehiculo {n}: {peso:.0f} kg superan los {capacidad:.0f} kg de {v.codigo_vehiculo}/{v.codigo_carroceria}")
            if volumen > ficha.volumen_m3:
                volumen_ok = False
                violaciones.append(f"vehiculo {n}: {volumen:.2f} m3 superan los {ficha.volumen_m3:.2f} m3 de {v.codigo_vehiculo}")

        unidades_ok = all(repartidas.get(i, 0) == linea.cantidad for i, linea in enumerate(lineas))
        if not unidades_ok:
            violaciones.append("alguna unidad pedida no quedo en ningun vehiculo, o quedo en mas de uno")

    return Comparacion(
        resultado_motor=motor.resultado, resultado_ia=ia.resultado,
        motivo_motor=_motivo(motor.motivo_sin_seleccion), motivo_ia=ia.motivo,
        flota_motor=flota_motor, flota_ia=flota_ia,
        coincide_resultado=motor.resultado == ia.resultado,
        coincide_flota=flota_motor == flota_ia,
        coincide_configuracion=sorted(c for c, _ in flota_motor) == sorted(c for c, _ in flota_ia),
        ia_respeta_peso=peso_ok, ia_respeta_volumen=volumen_ok, ia_conserva_unidades=unidades_ok,
        violaciones=tuple(violaciones),
    )
