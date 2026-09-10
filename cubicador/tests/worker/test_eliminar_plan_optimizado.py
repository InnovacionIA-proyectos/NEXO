"""S3-HU-10.3 (issue #133): borrar un plan optimizado fallaba siempre.

`plan_cargue_repo.py::eliminar()` borraba la fila del plan sin borrar antes sus
`colocaciones_cargue` — a diferencia de `eliminar_item()`, que si respeta ese
orden. En Postgres cualquier plan ya optimizado revienta con
`ForeignKeyViolationError`, agota los 3 reintentos y termina en la DLQ.

Y como el `DELETE /api/planes-cargue/{id}` de NEXO hace un borrado OPTIMISTA
(borra su fila y responde antes de saber si Cubicador lo logro), el desenlace
verificado en vivo es el peor posible: NEXO cree que el plan no existe,
Cubicador lo conserva intacto, y no hay nada que reconcilie la diferencia (ver
tests/QA/s3-hu-01-02-03-061-cubicacion-por-colas/README.md bug 3).

## Por que estos tests NO esperan ForeignKeyViolationError

Seria un test que pasa siempre, con o sin arreglo. La suite corre sobre SQLite
en memoria y `db.py` no activa `PRAGMA foreign_keys=ON`, asi que SQLite **no
aplica las claves foraneas**: antes del arreglo, en vez de fallar, simplemente
deja las colocaciones huerfanas apuntando a un plan que ya no existe.

Se afirma entonces la propiedad real —que no queden filas huerfanas—, que es
lo que en Postgres impide el error y en SQLite impide la basura. Verificable en
los dos motores, y falla de verdad si alguien revierte el arreglo.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")


from uuid import uuid4

import pytest

from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan
from domain.entidades.entidades import ItemPlanCargue
from infrastructure.persistence.db import SessionLocal, crear_tablas
from infrastructure.persistence.modelos import ColocacionCargueModel, ItemPlanCargueModel
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

from sqlalchemy import func, select


async def _contar(sesion, modelo, plan_id: str) -> int:
    return (await sesion.execute(
        select(func.count()).select_from(modelo).where(modelo.plan_cargue_id == plan_id)
    )).scalar_one()


@pytest.fixture
async def escenario():
    """Plan OPTIMIZADO, es decir con colocaciones reales — que es la precondicion
    del bug: un plan sin optimizar se borraba bien."""
    await crear_tablas()
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)

        catalogo = await vehiculos.listar_con_siembra()
        furgon = next(v for v in catalogo if "13.6" in v.nombre)
        plan = await planes.crear("Plan a borrar", furgon.id)
        await planes.guardar_item(plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, sku="PAL", descripcion="Pallet", destino=None, secuencia_entrega=1,
            largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=3,
        ))
        await optimizar_plan(planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))

        yield sesion, planes, vehiculos, plan.id


async def test_el_escenario_tiene_colocaciones(escenario):
    """Sin esto los otros dos no prueban nada: si el plan no llegara a tener
    colocaciones, borrarlo funcionaria igual antes y despues del arreglo."""
    sesion, planes, _, plan_id = escenario

    assert await _contar(sesion, ColocacionCargueModel, plan_id) > 0
    assert (await planes.obtener(plan_id)).estado == "optimizado"


async def test_eliminar_un_plan_optimizado_no_lanza(escenario):
    _, planes, _, plan_id = escenario

    await planes.eliminar(plan_id)

    assert await planes.obtener(plan_id) is None


async def test_eliminar_un_plan_optimizado_no_deja_filas_huerfanas(escenario):
    """El corazon del bug. En Postgres estas filas son las que disparan el
    ForeignKeyViolationError; en SQLite quedan como basura apuntando a un plan
    inexistente. En los dos casos el arreglo es el mismo: borrarlas antes."""
    sesion, planes, _, plan_id = escenario

    await planes.eliminar(plan_id)

    assert await _contar(sesion, ColocacionCargueModel, plan_id) == 0, \
        "Quedaron colocaciones apuntando a un plan borrado."
    assert await _contar(sesion, ItemPlanCargueModel, plan_id) == 0, \
        "Quedaron items apuntando a un plan borrado."


async def test_eliminar_planes_de_linea_tampoco_deja_filas_huerfanas():
    """El MISMO defecto en el metodo hermano, que la issue no menciona.

    `eliminar_planes_de_linea` hace un `delete()` MASIVO, y ahi es peor que en
    `eliminar()`: un delete masivo no dispara la cascada del ORM, asi que quedan
    huerfanos los items ADEMAS de las colocaciones.

    No es teorico: lo llama `crear_plan_desde_linea` para reconstruir la flota, o
    sea que volver a cubicar una linea que ya tiene flota optimizada revienta en
    Postgres cada vez."""
    await crear_tablas()
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)

        catalogo = await vehiculos.listar_con_siembra()
        furgon = next(v for v in catalogo if "13.6" in v.nombre)
        linea_id = str(uuid4())

        plan = await planes.crear("Flota - vehiculo 1", furgon.id, linea_id, 1)
        await planes.guardar_item(plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, sku="PAL", descripcion="Pallet", destino=None, secuencia_entrega=1,
            largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=3,
        ))
        await optimizar_plan(planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))
        assert await _contar(sesion, ColocacionCargueModel, plan.id) > 0

        await planes.eliminar_planes_de_linea(linea_id)

        assert await planes.obtener(plan.id) is None
        assert await _contar(sesion, ColocacionCargueModel, plan.id) == 0, \
            "Quedaron colocaciones de una flota borrada."
        assert await _contar(sesion, ItemPlanCargueModel, plan.id) == 0, \
            "Quedaron items de una flota borrada (el delete masivo no dispara la cascada)."
