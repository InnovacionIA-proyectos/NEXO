"""El camino de REPORTE de un fallo no puede impedir el `ack()`.

Caso real (14-sep-2026, ambiente de pre-despliegue): un `guardar_item` contra un
plan inexistente reventaba con `ForeignKeyViolationError` durante el flush. Eso
deja la sesion de SQLAlchemy ENVENENADA - cualquier operacion posterior sobre
ella lanza `PendingRollbackError` hasta que alguien haga `rollback()`. El
manejador de errores llamaba entonces a `marcar_error()`, que toca esa misma
sesion, y explotaba ANTES del `ack()`. El mensaje quedaba sin confirmar para
siempre.

Con `prefetch_count=2` bastaron DOS mensajes asi para congelar el worker
completo: 8 comandos quedaron encolados detras sin procesarse (verificado en el
broker de pre-despliegue: `messages_unacknowledged=2`, `messages_ready=8`, y los
8 con `x-intentos=0` porque nunca llegaron a manos del worker).

S3-HU-10.1 (#131) ya habia anticipado ESTA CLASE de fallo - su comentario lo
describe textualmente - y blindo una sola de sus causas (el `planId` faltante,
con `.get` en vez de indexar). Estos tests cubren la clase entera: pase lo que
pase en el reporte, el mensaje se ackea.

Por que la suite no lo agarro antes: corre sobre SQLite y `db.py` no activa
`PRAGMA foreign_keys=ON`, asi que la FK nunca se viola y la sesion nunca se
envenena. Solo aparece contra Postgres. Por eso aca el envenenamiento se inyecta
a proposito; la reproduccion contra Postgres real esta en tests/QA/.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import json

import pytest
from sqlalchemy.exc import PendingRollbackError

import worker.main as worker_main
from application.excepciones import AppException
from infrastructure.persistence.db import crear_tablas
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository


class _MensajeFalso:
    """Doble de `aio_pika.IncomingMessage`. Lo unico que importa es si `ack()`
    llego a ejecutarse."""

    def __init__(self, cuerpo: dict, intentos_previos: int = 0) -> None:
        self.body = json.dumps(cuerpo).encode("utf-8")
        self.headers = {"x-intentos": intentos_previos}
        self.acks = 0

    async def ack(self) -> None:
        self.acks += 1


class _PublisherFalso:
    def __init__(self) -> None:
        self.crudos: list[tuple[str, dict]] = []
        self.rechazos: list[tuple[str, str, str]] = []

    async def publicar_crudo(self, routing_key: str, body: bytes, headers: dict | None = None) -> None:
        self.crudos.append((routing_key, headers or {}))

    async def publicar_cubicacion_rechazada(self, correlation_id: str, codigo: str, mensaje: str) -> None:
        self.rechazos.append((correlation_id, codigo, mensaje))


@pytest.fixture(autouse=True)
async def _tablas():
    await crear_tablas()


@pytest.fixture(autouse=True)
def _sin_correo(monkeypatch):
    """El camino a la DLQ alerta por correo; aca no interesa y no debe tocar SMTP."""

    async def _nada(tipo: str, error: str, intentos: int) -> None:
        return None

    monkeypatch.setattr(worker_main, "alertar_mensaje_en_dlq", _nada)


def _envenenar_marcar_error(monkeypatch) -> None:
    """Reproduce el estado exacto de produccion: la sesion quedo inutilizable tras
    el flush fallido, asi que `marcar_error` lanza al tocarla."""

    async def _explota(self, plan_id, mensaje):  # noqa: ANN001
        raise PendingRollbackError(
            "This Session's transaction has been rolled back due to a previous "
            "exception during flush."
        )

    monkeypatch.setattr(SqlPlanCargueRepository, "marcar_error", _explota)


def _cuerpo(**extra):
    base = {"tipo": "optimizar", "planId": "p-1", "correlationId": "c-1", "solicitud": {}}
    base.update(extra)
    return base


# ------------------------------------------------- rama `except Exception` (fallo inesperado)


async def test_ultimo_intento_con_sesion_envenenada_igual_se_ackea(monkeypatch):
    """El caso textual de produccion: ultimo intento, el mensaje ya se copio a la
    DLQ, y `marcar_error` revienta contra la sesion envenenada."""

    async def _falla(*a, **k):
        raise RuntimeError("ForeignKeyViolationError simulada en el flush")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)
    _envenenar_marcar_error(monkeypatch)

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=worker_main.MAX_INTENTOS - 1)
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1, (
        "El mensaje quedo sin ackear: ocupa un hueco del prefetch para siempre. "
        "Con prefetch_count=2, dos de estos congelan el worker entero."
    )
    assert [rk for rk, _ in publisher.crudos] == [worker_main.COLA_DLQ], (
        "El mensaje tiene que llegar igual a la DLQ: es la unica constancia del fallo."
    )


async def test_intento_intermedio_si_falla_el_reencolado_igual_se_ackea(monkeypatch):
    """La otra rama del mismo manejador: reencolar en vez de ir a la DLQ.

    Aca `marcar_error` no llega a correr, asi que la sesion envenenada no la toca.
    El riesgo analogo es el reencolado: si el broker rebota esa publicacion, el
    mensaje original tampoco se ackea y el hueco de prefetch se pierde igual."""

    async def _falla(*a, **k):
        raise RuntimeError("fallo transitorio")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)

    class _PublisherQueNoReencola(_PublisherFalso):
        async def publicar_crudo(self, routing_key, body, headers=None):
            raise ConnectionError("el broker rechazo el reencolado")

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=0)

    await worker_main._procesar(mensaje, _PublisherQueNoReencola())

    assert mensaje.acks == 1


async def test_intento_intermedio_normal_reencola_y_ackea(monkeypatch):
    """Y el camino feliz de esa rama sigue intacto: republica el reintento y
    confirma el original. Sin esto, el `finally` podria estar tapando que el
    reencolado dejo de ocurrir."""

    async def _falla(*a, **k):
        raise RuntimeError("fallo transitorio")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=0)
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1
    assert [rk for rk, _ in publisher.crudos] == ["cubicacion.solicitada"]


# ------------------------------------------------- rama `except AppException` (error de negocio)


async def test_error_de_negocio_con_sesion_envenenada_igual_se_ackea(monkeypatch):
    """El mismo agujero en la rama de negocio: `marcar_error` corre antes del ack."""

    async def _falla(*a, **k):
        raise AppException("PLAN_NO_ENCONTRADO", "No existe el plan.")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)
    _envenenar_marcar_error(monkeypatch)

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=0)
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1


# ------------------------------------------------- el rollback, que es la CAUSA


async def test_se_hace_rollback_antes_de_reportar(monkeypatch):
    """Afirma la causa, no solo el sintoma. Sin `rollback()` la sesion sigue
    envenenada y `marcar_error` nunca puede funcionar: el plan jamas queda en
    estado 'error' y el operador se queda sin rastro. Si alguien deja el `finally`
    pero quita el rollback, los tests de arriba pasan igual y este falla."""
    rollbacks: list[int] = []

    async def _falla(*a, **k):
        raise RuntimeError("fallo que ensucia la sesion")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)

    original = worker_main.SessionLocal

    def _sesion_espia(*a, **k):
        sesion = original(*a, **k)
        rollback_real = sesion.rollback

        async def _rollback():
            rollbacks.append(1)
            await rollback_real()

        sesion.rollback = _rollback
        return sesion

    monkeypatch.setattr(worker_main, "SessionLocal", _sesion_espia)

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=0)
    await worker_main._procesar(mensaje, _PublisherFalso())

    assert rollbacks, "No se hizo rollback: la sesion queda envenenada para el reporte."
    assert mensaje.acks == 1


# ------------------------------------------------- publicar tampoco puede atascar


async def test_si_falla_publicar_el_rechazo_el_mensaje_igual_se_ackea(monkeypatch):
    """`marcar_error` no es lo unico que corre antes del ack: publicar el rechazo
    tambien. Si el broker rebota esa publicacion, el desenlace no puede ser un
    hueco de prefetch perdido."""

    async def _falla(*a, **k):
        raise RuntimeError("fallo cualquiera")

    monkeypatch.setattr(worker_main, "_ejecutar_solicitud", _falla)

    class _PublisherQueFalla(_PublisherFalso):
        async def publicar_cubicacion_rechazada(self, *a, **k):
            raise ConnectionError("el broker rechazo la publicacion")

    mensaje = _MensajeFalso(_cuerpo(), intentos_previos=worker_main.MAX_INTENTOS - 1)

    await worker_main._procesar(mensaje, _PublisherQueFalla())

    assert mensaje.acks == 1
