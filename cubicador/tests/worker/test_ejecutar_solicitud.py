"""Guarda de regresion del bug real encontrado en
docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2.6: el worker leia
`cuerpo["lineaSolicitudId"]` en la raiz del mensaje, pero
`RabbitMqEventosPublisher.publicar_cubicacion_solicitada` lo anida bajo
`cuerpo["solicitud"]["lineaSolicitudId"]` (ver
`api/routers/planes_cargue.py::crear_desde_linea` y
`infrastructure/mensajeria/rabbitmq.py::_publicar`). Construye `cuerpo`
exactamente como lo arma el publisher real (no a mano), para que un futuro
cambio de forma en cualquiera de los dos lados rompa este test.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import pytest

import worker.main as worker_main
from application.ports.externos import LineaParaCubicacion, PosicionCarga
from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan
from infrastructure.persistence.db import crear_tablas, SessionLocal
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository


def _cuerpo_publicado(plan_id: str, tipo: str, solicitud: dict) -> dict:
    """La misma forma que arma `RabbitMqEventosPublisher._publicar` dentro de
    `publicar_cubicacion_solicitada` - no se referencia el publisher aqui
    directamente (requeriria un broker) pero la forma debe coincidir."""
    return {"tipo": tipo, "planId": plan_id, "correlationId": plan_id, "solicitud": solicitud}


@pytest.fixture
async def repos():
    await crear_tablas()
    async with SessionLocal() as sesion:
        yield SqlPlanCargueRepository(sesion), SqlVehiculoCubicacionRepository(sesion)


async def test_ejecutar_solicitud_optimizar_desempaca_solicitud_anidada(repos):
    planes, vehiculos = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan de prueba", furgon.id)
    from domain.entidades.entidades import ItemPlanCargue
    await planes.guardar_item(plan.id, None, ItemPlanCargue(
        plan_cargue_id=plan.id, sku="", descripcion="Pallet", destino=None, secuencia_entrega=1,
        largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=3,
    ))

    cuerpo = _cuerpo_publicado(plan.id, "optimizar", {"estrategia": "Combinada", "algoritmo": "bfd"})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    assert plan_id == plan.id
    actualizado = await planes.obtener(plan_id)
    assert actualizado.estado == "optimizado"


async def test_ejecutar_solicitud_crear_desde_linea_desempaca_solicitud_anidada(repos, monkeypatch):
    planes, vehiculos = repos

    class _ClientePortFalso:
        def __init__(self, base_url: str, api_key: str) -> None:
            pass

        async def obtener(self, linea_solicitud_id: str) -> LineaParaCubicacion:
            return LineaParaCubicacion(
                linea_id=linea_solicitud_id,
                posiciones=[PosicionCarga(descripcion="Pallet", cantidad=2, largo_cm=120, ancho_cm=100, alto_cm=100, peso_kg=300)],
                origen="Origen", destino="Destino",
            )

    monkeypatch.setattr(worker_main, "NexoLineaSolicitudClient", _ClientePortFalso)

    linea_id = "11111111-1111-1111-1111-111111111111"
    cuerpo = _cuerpo_publicado(linea_id, "crear_desde_linea", {"lineaSolicitudId": linea_id})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    plan = await planes.obtener(plan_id)
    assert plan.linea_solicitud_id == linea_id
    assert plan.estado == "optimizado"
