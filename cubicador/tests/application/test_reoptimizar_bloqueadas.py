"""Issue #151: 'Reoptimizar (respetar bloqueadas)' debe conservar el flag
`bloqueada` de las colocaciones que se dejaron fijas. Cada reoptimizacion
reconstruye TODAS las filas de ColocacionCargueModel desde cero (ver
`plan_cargue_repo.py::guardar_resultado_optimizacion`), asi que este es un
test de integracion real contra la base (no basta con el motor puro: el bug
estaba exclusivamente en la capa de persistencia)."""

from __future__ import annotations

from domain.entidades.entidades import ItemPlanCargue, VehiculoCubicacion
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

from application.plan_cargue import items as items_uc
from application.plan_cargue import planes as planes_uc
from application.plan_cargue import vehiculos as vehiculos_uc
from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan


async def test_bloqueada_sobrevive_a_reoptimizar_respetando_bloqueadas():
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)

        vehiculo = await vehiculos_uc.guardar_vehiculo(vehiculos, VehiculoCubicacion(
            nombre="V-151", largo_interno_mm=3000, ancho_interno_mm=1200, alto_interno_mm=1200,
            capacidad_kg=2000, tara_kg=0, puerta_ancho_mm=1200, puerta_alto_mm=1200,
        ))
        plan = await planes_uc.crear_plan(planes, "Plan #151", vehiculo.id)
        await items_uc.guardar_item(planes, plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, descripcion="A", largo_mm=1000, ancho_mm=1000, alto_mm=1000, peso_kg=100,
        ))
        await items_uc.guardar_item(planes, plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, descripcion="B", largo_mm=1000, ancho_mm=1000, alto_mm=1000, peso_kg=100,
        ))

        params = ParametrosOptimizacionInput(estrategia="Combinada", algoritmo="bfd")
        await optimizar_plan(planes, vehiculos, plan.id, params)

        colocaciones = await planes.obtener_colocaciones(plan.id)
        cargadas = [c for c in colocaciones if c.cargada]
        assert len(cargadas) == 2
        objetivo = cargadas[0]
        clave = (objetivo.item_plan_cargue_id, objetivo.indice_unidad)
        await planes.marcar_bloqueada(objetivo.id, True)

        # Reoptimizar respetando bloqueadas: la fila de esa colocacion se BORRA
        # y se reconstruye desde `fijas` (ver optimizar.py) - el bug era que la
        # fila nueva perdia bloqueada=True en ese ciclo.
        params_reopt = ParametrosOptimizacionInput(estrategia="Combinada", algoritmo="bfd", respetar_bloqueadas=True)
        await optimizar_plan(planes, vehiculos, plan.id, params_reopt)

        colocaciones_finales = await planes.obtener_colocaciones(plan.id)
        reconstruida = next(
            c for c in colocaciones_finales
            if (c.item_plan_cargue_id, c.indice_unidad) == clave
        )
        assert reconstruida.bloqueada is True
