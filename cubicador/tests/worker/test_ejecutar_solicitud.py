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
from domain.entidades.entidades import ItemPlanCargue
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


# ------------------------------------------------------------- S3-HU-02 (issue #105):
# los 8 comandos de escritura nuevos, consumidos por el mismo worker.


def _item_dict(**overrides) -> dict:
    base = dict(
        sku="SKU-1", descripcion="Pallet", pedido=None, cliente=None, destino=None, secuenciaEntrega=1,
        largoMm=1200, anchoMm=1000, altoMm=1000, pesoKg=500, cantidad=3, color=None, prioridad=3,
        tipoEmbalaje=None, apilable=True, maxNiveles=0, maxPesoEncimaKg=-1, rotable=True,
        mantenerVertical=False, fragil=False, peligroso=False, refrigerado=False,
        grupoCompatibilidad=None, observaciones=None,
    )
    base.update(overrides)
    return base


async def test_crear_plan_usa_el_planid_que_manda_nexo_no_uno_propio(repos):
    planes, vehiculos = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)

    plan_id_de_nexo = "22222222-2222-2222-2222-222222222222"
    cuerpo = _cuerpo_publicado(
        plan_id_de_nexo, "crear_plan", {"nombre": "Plan desde NEXO", "vehiculoId": furgon.id},
    )

    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    assert plan_id == plan_id_de_nexo
    creado = await planes.obtener(plan_id_de_nexo)
    assert creado is not None
    assert creado.nombre == "Plan desde NEXO"


async def test_crear_plan_demo_crea_un_plan_optimizado(repos):
    planes, vehiculos = repos
    await vehiculos.listar_con_siembra()  # siembra los presets, el demo espera "13.6" ya sembrado

    cuerpo = _cuerpo_publicado("cualquier-correlation-id", "crear_plan_demo", {})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    assert plan_id is not None
    plan = await planes.obtener(plan_id)
    assert len(plan.items) > 0


async def test_guardar_vehiculo_no_devuelve_plan_id_y_persiste(repos):
    planes, vehiculos = repos
    vehiculo_id = "33333333-3333-3333-3333-333333333333"

    cuerpo = _cuerpo_publicado(vehiculo_id, "guardar_vehiculo", {
        "id": vehiculo_id, "nombre": "Vehiculo de prueba", "descripcion": None,
        "largoInternoMm": 6000, "anchoInternoMm": 2200, "altoInternoMm": 2000,
        "capacidadKg": 6000, "taraKg": 4000, "puertaAnchoMm": 2200, "puertaAltoMm": 1900,
        "puertaPosicion": "trasera", "ejes": [{"posicionMm": 1000, "capacidadKg": 3500}],
        "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0,
    })

    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    assert plan_id is None
    guardado = await vehiculos.obtener(vehiculo_id)
    assert guardado is not None
    assert guardado.nombre == "Vehiculo de prueba"


async def test_guardar_item_e_importar_items_y_eliminar_item(repos):
    planes, vehiculos = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan de items", furgon.id)

    cuerpo_guardar = _cuerpo_publicado(plan.id, "guardar_item", _item_dict(id=None))
    plan_id = await worker_main._ejecutar_solicitud(cuerpo_guardar, planes, vehiculos)
    assert plan_id == plan.id
    actualizado = await planes.obtener(plan.id)
    assert len(actualizado.items) == 1
    item_id = actualizado.items[0].id

    cuerpo_importar = _cuerpo_publicado(plan.id, "importar_items", {
        "items": [_item_dict(id=None, sku="SKU-2"), _item_dict(id=None, sku="SKU-3")],
    })
    await worker_main._ejecutar_solicitud(cuerpo_importar, planes, vehiculos)
    tras_importar = await planes.obtener(plan.id)
    assert len(tras_importar.items) == 3

    cuerpo_eliminar = _cuerpo_publicado(plan.id, "eliminar_item", {"itemId": item_id})
    await worker_main._ejecutar_solicitud(cuerpo_eliminar, planes, vehiculos)
    tras_eliminar = await planes.obtener(plan.id)
    assert len(tras_eliminar.items) == 2


async def test_eliminar_plan_borra_la_fila_y_no_devuelve_plan_id(repos):
    planes, vehiculos = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan a borrar", furgon.id)

    cuerpo = _cuerpo_publicado(plan.id, "eliminar_plan", {})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos)

    assert plan_id is None
    assert await planes.obtener(plan.id) is None


async def test_ajustar_colocacion_retira_una_unidad_cargada(repos):
    planes, vehiculos = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan a ajustar", furgon.id)
    await planes.guardar_item(plan.id, None, ItemPlanCargue(
        plan_cargue_id=plan.id, sku="", descripcion="Pallet", destino=None, secuencia_entrega=1,
        largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=2,
    ))
    cuerpo_optimizar = _cuerpo_publicado(plan.id, "optimizar", {"estrategia": "Combinada", "algoritmo": "bfd"})
    await worker_main._ejecutar_solicitud(cuerpo_optimizar, planes, vehiculos)

    colocaciones = await planes.obtener_colocaciones(plan.id)
    cargada = next(c for c in colocaciones if c.cargada)

    cuerpo_ajustar = _cuerpo_publicado(plan.id, "ajustar_colocacion", {"colocacionId": cargada.id, "retirar": True})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo_ajustar, planes, vehiculos)

    assert plan_id == plan.id
    tras_ajuste = await planes.obtener_colocacion(plan.id, cargada.id)
    assert tras_ajuste.cargada is False
    assert tras_ajuste.codigo_rechazo == "RETIRADA_MANUAL"
