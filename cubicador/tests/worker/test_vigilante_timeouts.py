"""Cubre S3-HU-06.1: el vigilante de timeouts publica `cubicacion.rechazada`.

Antes de esto el vigilante marcaba `error` en Postgres y NO publicaba nada, asi
que NEXO se quedaba en `procesando` para siempre y el usuario no se enteraba
nunca (ver docs/DECISIONES_TECNICAS.md #10 y CONTRATO.md seccion 6). Era el
unico camino de fallo del sistema que terminaba en silencio.

El test que de verdad importa es
`test_no_marca_error_si_falla_la_publicacion`: fija el ORDEN (publicar antes de
marcar). Si alguien lo invierte "para simplificar", un broker caido dejaria los
planes fuera de `procesando` sin haber avisado a nadie, y el proximo ciclo ya no
los encontraria - el mismo bug, de vuelta.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import pytest

import worker.main as worker_main
from infrastructure.persistence.db import SessionLocal, crear_tablas
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository


class _PublisherFalso:
    """Doble de prueba del publisher (sin broker). `explota=True` simula
    RabbitMQ caido en el momento exacto de publicar."""

    def __init__(self, explota: bool = False) -> None:
        self.explota = explota
        self.rechazos: list[tuple[str, str, str]] = []

    async def publicar_cubicacion_rechazada(self, plan_id: str, codigo: str, mensaje: str) -> None:
        if self.explota:
            raise ConnectionError("RabbitMQ no disponible")
        self.rechazos.append((plan_id, codigo, mensaje))


async def _crear_plan_procesando() -> str:
    await crear_tablas()
    async with SessionLocal() as sesion:
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        planes = SqlPlanCargueRepository(sesion)
        catalogo = await vehiculos.listar_con_siembra()
        plan = await planes.crear("Plan atascado", catalogo[0].id)
        await planes.marcar_procesando(plan.id)
        return plan.id


async def _estado_de(plan_id: str) -> str:
    async with SessionLocal() as sesion:
        plan = await SqlPlanCargueRepository(sesion).obtener(plan_id)
        assert plan is not None
        return plan.estado


@pytest.fixture
def timeout_vencido(monkeypatch):
    """Umbral negativo => el limite queda en el FUTURO, asi que cualquier plan
    en `procesando` cuenta como atascado. Evita tener que falsear timestamps."""
    monkeypatch.setattr(worker_main, "TIMEOUT_PROCESANDO_MINUTOS", -1)


async def test_publica_rechazo_y_marca_error(timeout_vencido):
    plan_id = await _crear_plan_procesando()
    publisher = _PublisherFalso()

    resueltos = await worker_main._revisar_timeouts(publisher)

    assert resueltos == [plan_id]
    assert len(publisher.rechazos) == 1
    publicado_id, codigo, mensaje = publisher.rechazos[0]
    assert publicado_id == plan_id
    assert codigo == worker_main.CODIGO_RECHAZO_TIMEOUT
    assert "procesando" in mensaje
    assert await _estado_de(plan_id) == "error"


async def test_no_marca_error_si_falla_la_publicacion(timeout_vencido):
    """El corazon de S3-HU-06.1: con el broker caido el plan debe quedar como
    estaba, para que el proximo ciclo lo reintente. Si esto empieza a fallar,
    alguien invirtio el orden y el rechazo se puede perder en silencio."""
    plan_id = await _crear_plan_procesando()
    publisher = _PublisherFalso(explota=True)

    with pytest.raises(ConnectionError):
        await worker_main._revisar_timeouts(publisher)

    assert await _estado_de(plan_id) == "procesando"


async def test_ignora_planes_dentro_del_umbral():
    """Sin el fixture: rige el umbral real (5 min por defecto), y un plan que
    acaba de entrar en `procesando` no debe tocarse."""
    plan_id = await _crear_plan_procesando()
    publisher = _PublisherFalso()

    resueltos = await worker_main._revisar_timeouts(publisher)

    assert resueltos == []
    assert publisher.rechazos == []
    assert await _estado_de(plan_id) == "procesando"


def test_umbrales_configurables_por_entorno(monkeypatch):
    """La issue pide que el umbral NO sea un valor fijo en codigo. Se leen al
    importar el modulo, asi que se recarga para verificarlo de verdad."""
    import importlib

    monkeypatch.setenv("CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS", "17")
    monkeypatch.setenv("CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS", "23")
    recargado = importlib.reload(worker_main)
    try:
        assert recargado.TIMEOUT_PROCESANDO_MINUTOS == 17
        assert recargado.INTERVALO_VIGILANTE_SEGUNDOS == 23
    finally:
        monkeypatch.undo()
        importlib.reload(worker_main)  # dejar el modulo con los valores por defecto
