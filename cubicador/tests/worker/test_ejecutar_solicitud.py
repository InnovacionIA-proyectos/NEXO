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

import json
import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import pytest

import worker.main as worker_main
from application.excepciones import ConflictException
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


class _PublisherFalso:
    """Doble de prueba de RabbitMqEventosPublisher (S3-HU-03, issue #110): solo
    registra las llamadas, no requiere broker. `_ejecutar_solicitud` ahora
    publica vehiculo.creado/vehiculo.actualizado directamente (ver worker/main.py)."""

    def __init__(self) -> None:
        self.vehiculos_creados: list[tuple[str, dict]] = []
        self.vehiculos_actualizados: list[tuple[str, dict]] = []
        self.vehiculos_eliminados: list[str] = []
        self.planes_generados: list[tuple[str, dict]] = []

    async def publicar_vehiculo_eliminado(self, vehiculo_id: str) -> None:
        self.vehiculos_eliminados.append(vehiculo_id)

    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict) -> None:
        self.vehiculos_creados.append((vehiculo_id, vehiculo))

    async def publicar_vehiculo_actualizado(self, vehiculo_id: str, vehiculo: dict) -> None:
        self.vehiculos_actualizados.append((vehiculo_id, vehiculo))

    async def publicar_plan_generado(self, plan_id: str, resultado: dict) -> None:
        self.planes_generados.append((plan_id, resultado))


class _MensajeFalso:
    """Doble minimo de aio_pika.IncomingMessage - suficiente para `_procesar`."""

    def __init__(self, cuerpo: dict) -> None:
        self.body = json.dumps(cuerpo).encode("utf-8")
        self.headers: dict = {}
        self.acked = False

    async def ack(self) -> None:
        self.acked = True


@pytest.fixture
async def repos():
    await crear_tablas()
    async with SessionLocal() as sesion:
        yield SqlPlanCargueRepository(sesion), SqlVehiculoCubicacionRepository(sesion), _PublisherFalso()


async def test_ejecutar_solicitud_optimizar_desempaca_solicitud_anidada(repos):
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan de prueba", furgon.id)
    from domain.entidades.entidades import ItemPlanCargue
    await planes.guardar_item(plan.id, None, ItemPlanCargue(
        plan_cargue_id=plan.id, sku="", descripcion="Pallet", destino=None, secuencia_entrega=1,
        largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=3,
    ))

    cuerpo = _cuerpo_publicado(plan.id, "optimizar", {"estrategia": "Combinada", "algoritmo": "bfd"})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id == plan.id
    actualizado = await planes.obtener(plan_id)
    assert actualizado.estado == "optimizado"


async def test_ejecutar_solicitud_crear_desde_linea_desempaca_solicitud_anidada(repos, monkeypatch):
    planes, vehiculos, publisher = repos

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
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

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
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)

    plan_id_de_nexo = "22222222-2222-2222-2222-222222222222"
    cuerpo = _cuerpo_publicado(
        plan_id_de_nexo, "crear_plan", {"nombre": "Plan desde NEXO", "vehiculoId": furgon.id},
    )

    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id == plan_id_de_nexo
    creado = await planes.obtener(plan_id_de_nexo)
    assert creado is not None
    assert creado.nombre == "Plan desde NEXO"


async def test_crear_plan_demo_crea_un_plan_optimizado(repos):
    planes, vehiculos, publisher = repos
    await vehiculos.listar_con_siembra()  # siembra los presets, el demo espera "13.6" ya sembrado

    cuerpo = _cuerpo_publicado("cualquier-correlation-id", "crear_plan_demo", {})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id is not None
    plan = await planes.obtener(plan_id)
    assert len(plan.items) > 0


async def test_guardar_vehiculo_no_devuelve_plan_id_y_persiste(repos):
    planes, vehiculos, publisher = repos
    vehiculo_id = "33333333-3333-3333-3333-333333333333"

    cuerpo = _cuerpo_publicado(vehiculo_id, "guardar_vehiculo", {
        "id": vehiculo_id, "nombre": "Vehiculo de prueba", "descripcion": None,
        "largoInternoMm": 6000, "anchoInternoMm": 2200, "altoInternoMm": 2000,
        "capacidadKg": 6000, "taraKg": 4000, "puertaAnchoMm": 2200, "puertaAltoMm": 1900,
        "puertaPosicion": "trasera", "ejes": [{"posicionMm": 1000, "capacidadKg": 3500}],
        "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0,
    })

    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id is None
    guardado = await vehiculos.obtener(vehiculo_id)
    assert guardado is not None
    assert guardado.nombre == "Vehiculo de prueba"

    # S3-HU-03 (issue #110): alta -> vehiculo.creado, con el vehiculo completo.
    assert publisher.vehiculos_creados == [(vehiculo_id, publisher.vehiculos_creados[0][1])]
    assert publisher.vehiculos_creados[0][1]["nombre"] == "Vehiculo de prueba"
    assert publisher.vehiculos_actualizados == []


async def test_guardar_vehiculo_dos_veces_publica_creado_y_luego_actualizado(repos):
    planes, vehiculos, publisher = repos
    vehiculo_id = "44444444-4444-4444-4444-444444444444"
    base = {
        "id": vehiculo_id, "nombre": "Vehiculo v1", "descripcion": None,
        "largoInternoMm": 6000, "anchoInternoMm": 2200, "altoInternoMm": 2000,
        "capacidadKg": 6000, "taraKg": 4000, "puertaAnchoMm": 2200, "puertaAltoMm": 1900,
        "puertaPosicion": "trasera", "ejes": [{"posicionMm": 1000, "capacidadKg": 3500}],
        "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0,
    }

    await worker_main._ejecutar_solicitud(
        _cuerpo_publicado(vehiculo_id, "guardar_vehiculo", base), planes, vehiculos, publisher,
    )
    await worker_main._ejecutar_solicitud(
        _cuerpo_publicado(vehiculo_id, "guardar_vehiculo", {**base, "nombre": "Vehiculo v2"}),
        planes, vehiculos, publisher,
    )

    assert len(publisher.vehiculos_creados) == 1
    assert len(publisher.vehiculos_actualizados) == 1
    assert publisher.vehiculos_actualizados[0][1]["nombre"] == "Vehiculo v2"


async def test_eliminar_vehiculo_borra_y_publica_vehiculo_eliminado(repos):
    """Cierra el par de guardar_vehiculo. El id del vehiculo viaja en `planId`, igual que
    en el alta: los comandos de catalogo no tienen plan.

    El evento importa tanto como el borrado: la proyeccion de NEXO se alimenta SOLO de
    vehiculo.creado/actualizado/eliminado, asi que sin publicarlo GET /api/vehiculos
    seguiria ofreciendo un vehiculo que ya no existe."""
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    objetivo = catalogo[0]

    cuerpo = _cuerpo_publicado(objetivo.id, "eliminar_vehiculo", {})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id is None
    assert await vehiculos.obtener(objetivo.id) is None
    assert publisher.vehiculos_eliminados == [objetivo.id]


async def test_eliminar_vehiculo_en_uso_es_rechazo_y_no_publica_nada(repos):
    """Un vehiculo con planes asociados no se puede borrar: eliminar_vehiculo lanza
    ConflictException (AppException), que `_procesar` traduce a `cubicacion.rechazada`
    SIN reintentar - el resultado seria el mismo siempre.

    Y no debe publicarse vehiculo.eliminado: la proyeccion de NEXO borraria una fila que
    en Cubicador sigue existiendo, dejando las dos bases en desacuerdo."""
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    en_uso = catalogo[0]
    await planes.crear("Plan que usa el vehiculo", en_uso.id)

    cuerpo = _cuerpo_publicado(en_uso.id, "eliminar_vehiculo", {})
    with pytest.raises(ConflictException) as excinfo:
        await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert excinfo.value.codigo == "VEHICULO_EN_USO"
    assert await vehiculos.obtener(en_uso.id) is not None
    assert publisher.vehiculos_eliminados == []


async def test_guardar_item_e_importar_items_y_eliminar_item(repos):
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan de items", furgon.id)

    cuerpo_guardar = _cuerpo_publicado(plan.id, "guardar_item", _item_dict(id=None))
    plan_id = await worker_main._ejecutar_solicitud(cuerpo_guardar, planes, vehiculos, publisher)
    assert plan_id == plan.id
    actualizado = await planes.obtener(plan.id)
    assert len(actualizado.items) == 1
    item_id = actualizado.items[0].id

    cuerpo_importar = _cuerpo_publicado(plan.id, "importar_items", {
        "items": [_item_dict(id=None, sku="SKU-2"), _item_dict(id=None, sku="SKU-3")],
    })
    await worker_main._ejecutar_solicitud(cuerpo_importar, planes, vehiculos, publisher)
    tras_importar = await planes.obtener(plan.id)
    assert len(tras_importar.items) == 3

    cuerpo_eliminar = _cuerpo_publicado(plan.id, "eliminar_item", {"itemId": item_id})
    await worker_main._ejecutar_solicitud(cuerpo_eliminar, planes, vehiculos, publisher)
    tras_eliminar = await planes.obtener(plan.id)
    assert len(tras_eliminar.items) == 2


async def test_eliminar_plan_borra_la_fila_y_no_devuelve_plan_id(repos):
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan a borrar", furgon.id)

    cuerpo = _cuerpo_publicado(plan.id, "eliminar_plan", {})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)

    assert plan_id is None
    assert await planes.obtener(plan.id) is None


async def test_ajustar_colocacion_retira_una_unidad_cargada(repos):
    planes, vehiculos, publisher = repos
    catalogo = await vehiculos.listar_con_siembra()
    furgon = next(v for v in catalogo if "13.6" in v.nombre)
    plan = await planes.crear("Plan a ajustar", furgon.id)
    await planes.guardar_item(plan.id, None, ItemPlanCargue(
        plan_cargue_id=plan.id, sku="", descripcion="Pallet", destino=None, secuencia_entrega=1,
        largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=2,
    ))
    cuerpo_optimizar = _cuerpo_publicado(plan.id, "optimizar", {"estrategia": "Combinada", "algoritmo": "bfd"})
    await worker_main._ejecutar_solicitud(cuerpo_optimizar, planes, vehiculos, publisher)

    colocaciones = await planes.obtener_colocaciones(plan.id)
    cargada = next(c for c in colocaciones if c.cargada)

    cuerpo_ajustar = _cuerpo_publicado(plan.id, "ajustar_colocacion", {"colocacionId": cargada.id, "retirar": True})
    plan_id = await worker_main._ejecutar_solicitud(cuerpo_ajustar, planes, vehiculos, publisher)

    assert plan_id == plan.id
    tras_ajuste = await planes.obtener_colocacion(plan.id, cargada.id)
    assert tras_ajuste.cargada is False
    assert tras_ajuste.codigo_rechazo == "RETIRADA_MANUAL"


# ------------------------------------------------------------- S3-HU-03 (issue #110):
# plan-de-cargue.generado enriquecido - lista completa de items/colocaciones, no un conteo.


async def test_procesar_publica_el_resultado_enriquecido_con_items_y_colocaciones(monkeypatch):
    publisher = _PublisherFalso()
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        await crear_tablas()
        catalogo = await vehiculos.listar_con_siembra()
        furgon = next(v for v in catalogo if "13.6" in v.nombre)
        plan = await planes.crear("Plan enriquecido", furgon.id)
        await planes.guardar_item(plan.id, None, ItemPlanCargue(
            plan_cargue_id=plan.id, sku="SKU-9", descripcion="Pallet enriquecido", destino=None,
            secuencia_entrega=1, largo_mm=1200, ancho_mm=1000, alto_mm=1000, peso_kg=500, cantidad=2,
        ))

    # `_procesar` abre su PROPIA sesion (SessionLocal) - por eso el plan/item se
    # crean y confirman ANTES, en una sesion aparte, igual que llegaria por cola.
    cuerpo = _cuerpo_publicado(plan.id, "optimizar", {"estrategia": "Combinada", "algoritmo": "bfd"})
    mensaje = _MensajeFalso(cuerpo)

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acked is True
    assert len(publisher.planes_generados) == 1
    plan_id_publicado, resultado = publisher.planes_generados[0]
    assert plan_id_publicado == plan.id

    # Shape enriquecido (issue #110): ya no es {estado, puntaje, colocaciones: int}.
    assert "colocaciones" not in resultado or isinstance(resultado["colocaciones"], list)
    assert isinstance(resultado["colocaciones"], list)
    assert len(resultado["colocaciones"]) == 2  # cantidad=2 -> 2 unidades fisicas
    primera = resultado["colocaciones"][0]
    assert {"x", "y", "z", "orientacionIndice", "cargada", "itemPlanCargueId"} <= primera.keys()

    assert isinstance(resultado["plan"]["items"], list)
    assert resultado["plan"]["items"][0]["descripcion"] == "Pallet enriquecido"
    assert resultado["metricas"] is not None
    assert "unidadesCargadas" in resultado["metricas"]
