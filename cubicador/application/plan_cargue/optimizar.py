"""Corre (o recorre) la optimizacion de un plan. Reoptimizar es este mismo
caso de uso con `respetar_bloqueadas=True`. Equivalente a
OptimizarPlanCommand + MotorCubicacion3DAdapter.OptimizarAsync de NEXO -
aqui no hace falta el puerto/adapter separado porque el motor vive en el
mismo proceso (a diferencia de NEXO, que si necesitaba esa indireccion para
poder apuntar a un microservicio distinto)."""

from __future__ import annotations

from dataclasses import dataclass

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.motor.dominio import (
    Colocacion,
    EstrategiaOptimizacion,
    Orientacion,
    ParametrosOptimizacion,
    PonderacionesCombinada,
)
from domain.motor.motor import MotorCubicacion3D
from domain.motor.optimizadores import algoritmos_disponibles

from . import mapeos
from .resultado import ResultadoPlan, obtener_resultado

_motor = MotorCubicacion3D()

# Tope de unidades expandidas por plan (proteccion anti-DoS de la
# optimizacion sincrona: O(n^2 * orientaciones)).
_MAX_UNIDADES_POR_PLAN = 8000

_ESTRATEGIAS_VALIDAS = {e.value.lower(): e for e in EstrategiaOptimizacion}


_ALIAS_ALGORITMO = {
    "bfd": "bfd", "bestfitdecreasing": "bfd", "bestfit": "bfd",
    "ffd": "ffd", "firstfitdecreasing": "ffd", "firstfit": "ffd",
}


def _normalizar_algoritmo(raw: str | None) -> str | None:
    """Acepta los alias conocidos de ffd/bfd; para cualquier otro nombre,
    valida directo contra el registro de domain.motor.optimizadores - asi un
    algoritmo nuevo registrado ahi queda aceptado aqui sin tocar este
    archivo (los alias amigables para ese algoritmo se agregan aparte si
    hacen falta)."""
    n = (raw or "").strip().lower().replace(" ", "").replace("_", "").replace("-", "")
    normalizado = _ALIAS_ALGORITMO.get(n, n)
    return normalizado if normalizado in algoritmos_disponibles() else None


@dataclass
class ParametrosOptimizacionInput:
    estrategia: str
    algoritmo: str
    semilla: int | None = None
    ponderaciones: PonderacionesCombinada | None = None
    respetar_bloqueadas: bool = False


async def optimizar_plan(
    planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository,
    plan_id: str, parametros: ParametrosOptimizacionInput,
) -> ResultadoPlan:
    estrategia_enum = _ESTRATEGIAS_VALIDAS.get(parametros.estrategia.lower())
    if estrategia_enum is None:
        raise ValidationFailedException("ESTRATEGIA_INVALIDA", f"Estrategia desconocida: '{parametros.estrategia}'.")
    algoritmo = _normalizar_algoritmo(parametros.algoritmo)
    if algoritmo is None:
        disponibles = ", ".join(sorted(algoritmos_disponibles()))
        raise ValidationFailedException(
            "ALGORITMO_INVALIDO", f"Algoritmo desconocido: '{parametros.algoritmo}'. Disponibles: {disponibles}.",
        )

    plan = await planes.obtener(plan_id)
    if plan is None:
        raise NotFoundException("PLAN_NO_ENCONTRADO", "Plan de cargue no encontrado.")
    vehiculo = await vehiculos.obtener(plan.vehiculo_id)
    if vehiculo is None:
        raise NotFoundException("VEHICULO_NO_ENCONTRADO", "El vehiculo del plan ya no existe.")

    contenedor = mapeos.a_contenedor(vehiculo)
    items = mapeos.expandir_items(plan.items)

    if len(items) > _MAX_UNIDADES_POR_PLAN:
        raise ValidationFailedException(
            "PLAN_DEMASIADO_GRANDE",
            f"El plan expande {len(items)} unidades; el maximo optimizable es {_MAX_UNIDADES_POR_PLAN}. "
            "Divida la carga en varios planes.",
        )

    # Reoptimizacion parcial: conserva las colocaciones bloqueadas como fijas.
    fijas: list[Colocacion] = []
    items_por_clave = {i.id: i for i in items}
    if parametros.respetar_bloqueadas:
        bloqueadas = await planes.obtener_colocaciones_bloqueadas(plan_id)
        ids_validos = {i.id for i in items}
        for b in bloqueadas:
            clave = mapeos.clave(b.item_plan_cargue_id, b.indice_unidad)
            if clave not in ids_validos:
                continue  # el item pudo borrarse mientras tanto
            item_carga = items_por_clave[clave]
            fijas.append(Colocacion(
                item=item_carga, x=b.x, y=b.y, z=b.z,
                orientacion=Orientacion(b.orientacion_indice, b.largo_colocado_mm, b.ancho_colocado_mm, b.alto_colocado_mm),
                bloqueada=True,
            ))
        ids_fijos = {f.item.id for f in fijas}
        items = [i for i in items if i.id not in ids_fijos]

    parametros_motor = ParametrosOptimizacion(
        estrategia=estrategia_enum, ponderaciones=parametros.ponderaciones, algoritmo=algoritmo, semilla=parametros.semilla,
    )
    resultado = _motor.optimizar(items, contenedor, parametros_motor, fijas)
    metricas = _motor.metricas(resultado, mapeos.expandir_items(plan.items))

    await planes.guardar_resultado_optimizacion(
        plan_id, resultado, metricas,
        estrategia=parametros.estrategia, algoritmo=algoritmo, semilla=parametros.semilla,
        ponderaciones=parametros.ponderaciones,
    )
    return await obtener_resultado(planes, vehiculos, plan_id)
