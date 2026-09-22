"""Construye el 'resultado' completo de un plan (plan + vehiculo +
colocaciones + metricas + incidencias) - usado por el GET y por el retorno
de optimizar/ajustar. Equivalente a ResultadoPlanReader.LeerAsync de NEXO.

Nota: la mayoria de los campos de MetricasCubicacion NO se persisten (solo un
resumen: peso/volumen/utilizacion/puntaje en el propio PlanCargue) - se
recalculan en cada lectura a partir de las colocaciones crudas, igual que en
el original.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from application.excepciones import NotFoundException
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.entidades.entidades import ColocacionCargue, ItemPlanCargue, PlanCargue, VehiculoCubicacion
from domain.motor.dominio import IncidenciaValidacion, Rechazo, ResultadoCubicacion
from domain.motor.motor import MetricasCubicacion, MotorCubicacion3D

from . import mapeos

_motor = MotorCubicacion3D()


@dataclass
class ResultadoPlan:
    plan: PlanCargue
    vehiculo: VehiculoCubicacion
    colocaciones: list[ColocacionCargue]
    metricas: MetricasCubicacion | None
    incidencias: list[IncidenciaValidacion]


async def obtener_resultado(
    planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository, plan_id: str,
) -> ResultadoPlan:
    plan = await planes.obtener(plan_id)
    if plan is None:
        raise NotFoundException("PLAN_NO_ENCONTRADO", "Plan de cargue no encontrado.")
    vehiculo = await vehiculos.obtener(plan.vehiculo_id)
    if vehiculo is None:
        raise NotFoundException("VEHICULO_NO_ENCONTRADO", "El vehiculo del plan ya no existe.")

    colocaciones: list[ColocacionCargue] = await planes.obtener_colocaciones(plan_id)
    incidencias = _deserializar_incidencias(plan.incidencias_json)

    metricas: MetricasCubicacion | None = None
    if colocaciones:
        contenedor = mapeos.a_contenedor(vehiculo)
        todos_items = mapeos.expandir_items(plan.items)
        items_por_id = {i.id: i for i in plan.items}

        cargadas = [c for c in colocaciones if c.cargada]
        rechazadas = [c for c in colocaciones if not c.cargada]

        colocaciones_motor = [_a_colocacion_motor(c, items_por_id) for c in cargadas]
        rechazos_motor = [
            Rechazo(
                mapeos.a_item_carga(items_por_id[c.item_plan_cargue_id], c.indice_unidad),
                c.codigo_rechazo or "SIN_ESPACIO", c.razon_rechazo or "",
            )
            for c in rechazadas
        ]
        resultado = ResultadoCubicacion(colocaciones=colocaciones_motor, rechazos=rechazos_motor, contenedor=contenedor)
        # El puntaje no se recalcula aqui (no se vuelve a correr el algoritmo de
        # ubicacion) - se reconstruye desde el plan persistido, igual que hace
        # NEXO (ResultadoPlanReader: "resultado.Puntaje = plan.Puntaje") antes de
        # pedirle las metricas al motor, porque Metricas() copia r.Puntaje/
        # r.PuntajesParciales tal cual a la salida.
        resultado.puntaje = plan.puntaje
        # Issue #150: igual de necesario para las incidencias - sin esta linea
        # `resultado.incidencias` queda en su default_factory=list (vacio) y
        # `_motor.metricas()` cuenta advertencias/violaciones criticas SIEMPRE en
        # 0, sin importar las incidencias reales ya persistidas en el plan. Mismo
        # patron que ya usa `ajustar.py::ajustar_colocacion` antes de pedir metricas.
        resultado.incidencias = incidencias
        metricas = _motor.metricas(resultado, todos_items)

    return ResultadoPlan(plan, vehiculo, colocaciones, metricas, incidencias)


def _a_colocacion_motor(c: ColocacionCargue, items_por_id: dict[str, ItemPlanCargue]):
    from domain.motor.dominio import Colocacion, Orientacion

    item = mapeos.a_item_carga(items_por_id[c.item_plan_cargue_id], c.indice_unidad)
    return Colocacion(
        item=item, x=c.x, y=c.y, z=c.z,
        orientacion=Orientacion(c.orientacion_indice, c.largo_colocado_mm, c.ancho_colocado_mm, c.alto_colocado_mm),
        secuencia_cargue=c.secuencia_cargue,
        porcentaje_soporte=c.porcentaje_soporte, peso_encima_kg=c.peso_encima_kg,
        estado_estabilidad=c.estado_estabilidad, accesibilidad=c.accesibilidad,
        bloqueada=c.bloqueada, ajuste_manual=c.ajuste_manual,
    )


def _deserializar_incidencias(incidencias_json: str) -> list[IncidenciaValidacion]:
    try:
        crudas = json.loads(incidencias_json or "[]")
    except json.JSONDecodeError:
        return []
    return [
        IncidenciaValidacion(
            severidad=i.get("severidad", "informativa"),
            codigo_regla=i.get("codigoRegla") or i.get("codigo_regla", ""),
            titulo=i.get("titulo", ""),
            descripcion=i.get("descripcion", ""),
            accion_sugerida=i.get("accionSugerida") or i.get("accion_sugerida"),
            colocacion_item_id=i.get("colocacionItemId") or i.get("colocacion_item_id"),
        )
        for i in crudas
    ]
