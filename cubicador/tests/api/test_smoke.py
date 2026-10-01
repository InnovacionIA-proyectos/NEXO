"""Prueba de humo end-to-end contra SQLite en memoria: crear vehiculo -> plan
-> item -> optimizar -> leer resultado. Verifica que toda la cadena
api -> application -> infrastructure -> domain funciona de punta a punta.

`optimizar`/`desde-linea` ahora publican a RabbitMQ y responden 202 de
inmediato (ver api/routers/planes_cargue.py) - el procesamiento real lo hace
worker/main.py, ya verificado contra un broker real (ver
docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2.3). Este test usa
un publisher falso que solo registra lo publicado (no hay broker disponible
via ASGITransport) y luego simula al worker llamando al mismo caso de uso
(`optimizar_plan`) directamente contra la misma base SQLite en memoria, para
seguir verificando la cadena completa sin depender de un broker real.
"""

from __future__ import annotations

import os

os.environ["DATABASE_URL"] = "sqlite+aiosqlite:///:memory:"
os.environ["CUBICADOR_API_KEY"] = "test-key"

import pytest
from httpx import ASGITransport, AsyncClient

from api.dependencias import obtener_eventos_publisher
from api.main import app
from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan
from infrastructure.persistence.db import SessionLocal, crear_tablas
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

HEADERS = {"Authorization": "Bearer test-key"}


class _PublisherFalso:
    """Sustituye RabbitMqEventosPublisher en los tests: solo registra lo
    publicado, sin conectarse a ningun broker."""

    def __init__(self) -> None:
        self.publicados: list[tuple[str, str, dict]] = []
        self.vehiculos_publicados: list[str] = []

    async def publicar_cubicacion_solicitada(self, plan_id: str, tipo: str, solicitud: dict) -> None:
        self.publicados.append((tipo, plan_id, solicitud))

    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict) -> None:
        # Issue #162: listar_con_siembra() publica esto por cada preset nuevo -
        # el primer GET /vehiculos de la suite siembra los 9 presets.
        self.vehiculos_publicados.append(vehiculo_id)


@pytest.fixture
def publisher_falso():
    return _PublisherFalso()


@pytest.fixture
async def client(publisher_falso):
    # ASGITransport no dispara el lifespan de FastAPI (crear_tablas se
    # registra ahi) - se crea explicitamente aqui para el test.
    await crear_tablas()
    app.dependency_overrides[obtener_eventos_publisher] = lambda: publisher_falso
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as c:
        yield c
    app.dependency_overrides.clear()


async def _simular_worker_optimizar(plan_id: str) -> None:
    """Corre el mismo caso de uso que worker/main.py ejecutaria al consumir
    el mensaje encolado - la base SQLite en memoria es compartida (StaticPool),
    asi que una sesion nueva sigue viendo los mismos datos."""
    async with SessionLocal() as sesion:
        planes_repo = SqlPlanCargueRepository(sesion)
        vehiculos_repo = SqlVehiculoCubicacionRepository(sesion)
        parametros = ParametrosOptimizacionInput(estrategia="Combinada", algoritmo="bfd")
        await optimizar_plan(planes_repo, vehiculos_repo, plan_id, parametros)


async def test_health(client):
    r = await client.get("/health")
    assert r.status_code == 200


async def test_flujo_completo_crear_optimizar(client, publisher_falso):
    # Catalogo de vehiculos (se siembra solo la primera vez que se pide).
    r = await client.get("/api/planes-cargue/vehiculos", headers=HEADERS)
    assert r.status_code == 200
    vehiculos = r.json()
    assert len(vehiculos) == 9
    furgon = next(v for v in vehiculos if "13.6" in v["nombre"])

    # Crear plan vacio.
    r = await client.post("/api/planes-cargue", headers=HEADERS, json={"nombre": "Plan de prueba", "vehiculoId": furgon["id"]})
    assert r.status_code == 200, r.text
    plan = r.json()
    assert plan["estado"] == "borrador"

    # Agregar un item que sabemos que cabe.
    r = await client.post(
        f"/api/planes-cargue/{plan['id']}/items", headers=HEADERS,
        json={"descripcion": "Pallet de prueba", "largoMm": 1200, "anchoMm": 1000, "altoMm": 1000, "pesoKg": 500, "cantidad": 3},
    )
    assert r.status_code == 200, r.text

    # Optimizar: responde 202 de inmediato, el plan queda "procesando" y la
    # solicitud queda encolada (aqui, solo registrada por el publisher falso).
    r = await client.post(f"/api/planes-cargue/{plan['id']}/optimizar", headers=HEADERS, json={})
    assert r.status_code == 202, r.text
    assert r.json()["estado"] == "procesando"
    assert len(publisher_falso.publicados) == 1
    assert publisher_falso.publicados[0][:2] == ("optimizar", plan["id"])

    # Simula al worker consumiendo el mensaje encolado.
    await _simular_worker_optimizar(plan["id"])

    r = await client.get(f"/api/planes-cargue/{plan['id']}/resultado", headers=HEADERS)
    assert r.status_code == 200, r.text
    resultado = r.json()
    assert resultado["plan"]["estado"] == "optimizado"
    assert len(resultado["colocaciones"]) == 3
    assert all(c["cargada"] for c in resultado["colocaciones"])
    assert resultado["metricas"]["unidadesCargadas"] == 3

    # Ajuste manual: bloquear la primera colocacion (esto si sigue sincrono).
    colocacion_id = resultado["colocaciones"][0]["id"]
    r = await client.patch(
        f"/api/planes-cargue/{plan['id']}/colocaciones/{colocacion_id}", headers=HEADERS, json={"bloqueada": True},
    )
    assert r.status_code == 200, r.text
    assert r.json()["plan"]["estado"] == "ajustado"


async def test_cubicacion_rapida(client):
    r = await client.post(
        "/api/cubicacion", headers=HEADERS,
        json={"items": [{"descripcion": "Caja", "cantidad": 5, "largoCm": 100, "anchoCm": 100, "altoCm": 100, "pesoKg": 50}]},
    )
    assert r.status_code == 200, r.text
    data = r.json()
    assert len(data["matrices"]) == 9  # todo el catalogo evaluado
    assert data["sugerencia"] is not None


async def test_sin_api_key_es_rechazado(client):
    r = await client.get("/api/planes-cargue/vehiculos")
    assert r.status_code == 401
