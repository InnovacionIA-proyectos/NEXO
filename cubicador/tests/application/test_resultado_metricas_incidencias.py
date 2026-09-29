"""Issue #150: `metricas.advertencias`/`metricas.violacionesCriticas` deben
reflejar las incidencias reales del plan al leerlo (GET .../resultado), no
siempre 0. El bug estaba en `resultado.py::obtener_resultado`: reconstruye un
`ResultadoCubicacion` nuevo para pedirle metricas al motor, pero nunca le
copiaba `incidencias` (que si habia deserializado de `plan.incidencias_json`)
antes de llamar a `_motor.metricas()` - por eso este test persiste las
incidencias exactamente como lo hace `optimizar_plan` (via
`guardar_resultado_optimizacion`) y verifica la LECTURA posterior, que es el
punto exacto donde se perdian."""

from __future__ import annotations

from domain.entidades.entidades import ItemPlanCargue, VehiculoCubicacion
from domain.motor.dominio import (
    Colocacion,
    IncidenciaValidacion,
    Orientacion,
    ResultadoCubicacion,
)
from domain.motor.motor import MotorCubicacion3D
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

from application.plan_cargue import mapeos
from application.plan_cargue import items as items_uc
from application.plan_cargue import planes as planes_uc
from application.plan_cargue import vehiculos as vehiculos_uc
from application.plan_cargue.resultado import obtener_resultado

_motor = MotorCubicacion3D()


async def test_metricas_advertencias_y_violaciones_criticas_reflejan_incidencias_reales():
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)

        vehiculo = await vehiculos_uc.guardar_vehiculo(vehiculos, VehiculoCubicacion(
            nombre="V-150", largo_interno_mm=2000, ancho_interno_mm=1000, alto_interno_mm=1000,
            capacidad_kg=1000, tara_kg=0, puerta_ancho_mm=1000, puerta_alto_mm=1000,
        ))
        plan = await planes_uc.crear_plan(planes, "Plan #150", vehiculo.id)
        item = await items_uc.guardar_item(planes, plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, descripcion="Item", largo_mm=1000, ancho_mm=1000, alto_mm=1000, peso_kg=100,
        ))

        contenedor = mapeos.a_contenedor(vehiculo)
        item_carga = mapeos.a_item_carga(item, 0)
        colocacion = Colocacion(item=item_carga, x=0, y=0, z=0, orientacion=Orientacion(0, 1000, 1000, 1000))
        resultado = ResultadoCubicacion(
            colocaciones=[colocacion], rechazos=[], contenedor=contenedor,
            incidencias=[
                IncidenciaValidacion("advertencia", "ESTABILIDAD", "t", "d"),
                IncidenciaValidacion("critica", "COLISION", "t", "d"),
            ],
        )
        metricas = _motor.metricas(resultado, [item_carga])

        await planes.guardar_resultado_optimizacion(
            plan.id, resultado, metricas,
            estrategia="Combinada", algoritmo="bfd", semilla=None, ponderaciones=None,
        )

        leido = await obtener_resultado(planes, vehiculos, plan.id)

        assert leido.metricas is not None
        assert leido.metricas.advertencias == 1
        assert leido.metricas.violaciones_criticas == 1
