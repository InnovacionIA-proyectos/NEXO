"""S3-HU-10.1 (issue #131): un mensaje sin `planId` dejaba el worker atascado.

El sintoma NO es que se lance una excepcion — es que el mensaje **nunca se
ackea**. `_procesar` reportaba el fallo final con `cuerpo["planId"]`
(indexacion directa) ANTES del `ack()`, asi que un mensaje sin esa clave
lanzaba `KeyError` justo antes de confirmarlo y quedaba `unacknowledged` para
siempre.

Por que es grave (verificado en vivo, ver
tests/QA/s3-hu-01-02-03-061-cubicacion-por-colas/README.md bug 1): con
`prefetch_count=2` bastan DOS mensajes asi para que el worker deje de procesar
cualquier mensaje nuevo, de los 9 tipos. Reiniciar el contenedor no arregla
nada: RabbitMQ reentrega los atascados, vuelven a fallar igual y se atascan de
nuevo. La unica salida encontrada fue purgar la cola a mano.

No es hipotetico: `scripts/ci_docker_smoke.py` lo dispara en CADA corrida
porque su prueba de DLQ manda un mensaje sin `planId`. En GitHub Actions no se
nota porque el `docker compose down -v` destruye el entorno despues — en un QA
o produccion persistente, no.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import json

import pytest

import worker.main as worker_main
from infrastructure.persistence.db import crear_tablas


class _MensajeFalso:
    """Doble de `aio_pika.IncomingMessage`. Lo unico que importa es si `ack()`
    llego a ejecutarse: sin ack, RabbitMQ conserva el mensaje sin confirmar y
    ocupa un hueco del prefetch para siempre."""

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
    """El camino a la DLQ alerta por correo; acá no interesa y no debe tocar SMTP."""
    async def _nada(tipo: str, error: str, intentos: int) -> None:
        return None

    monkeypatch.setattr(worker_main, "alertar_mensaje_en_dlq", _nada)


async def test_mensaje_sin_planid_se_ackea_en_vez_de_atascarse():
    """El caso exacto que manda `ci_docker_smoke.py`: sin `planId`, y en el
    ultimo intento (el que reporta el fallo final y toca la linea del bug)."""
    mensaje = _MensajeFalso(
        {"tipo": "optimizar", "correlationId": "c-1", "solicitud": {}},
        intentos_previos=worker_main.MAX_INTENTOS - 1,
    )
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1, "El mensaje quedo sin ackear: ocupa un hueco del prefetch para siempre."
    assert [rk for rk, _ in publisher.crudos] == [worker_main.COLA_DLQ]
    assert publisher.rechazos and publisher.rechazos[0][1] == "ERROR_INTERNO"


async def test_mensaje_sin_planid_en_un_intento_intermedio_tambien_se_ackea():
    """El otro camino del mismo manejador: reencolar en vez de mandar a la DLQ.
    Tambien tiene que confirmar el mensaje original."""
    mensaje = _MensajeFalso({"tipo": "optimizar", "correlationId": "c-2", "solicitud": {}}, intentos_previos=0)
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1
    assert [rk for rk, _ in publisher.crudos] == ["cubicacion.solicitada"]


# ------------------------------------------------------------- lo que falla ANTES del try
#
# El arreglo de las 2 lineas del `except` cierra una instancia del bug, no la clase.
# Todo lo que corre ANTES del bloque `try` de `_procesar` tiene el mismo desenlace y
# es PEOR: ahi no hay manejador de errores ninguno, asi que ni siquiera llega a la
# DLQ. Un solo mensaje asi es un hueco de prefetch perdido para siempre.


class _MensajeCrudo(_MensajeFalso):
    """Permite un body que ni siquiera es JSON valido."""

    def __init__(self, body: bytes, headers: dict | None = None) -> None:
        self.body = body
        self.headers = headers if headers is not None else {"x-intentos": 0}
        self.acks = 0


async def test_body_que_no_es_json_se_ackea():
    mensaje = _MensajeCrudo(b"esto no es json")
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1, "Un body ilegible dejaba el mensaje sin ackear para siempre."


async def test_mensaje_sin_correlationid_se_ackea():
    """`correlation_id = cuerpo["correlationId"]` vive FUERA del try: sin la clave,
    revienta antes de que exista cualquier manejador."""
    mensaje = _MensajeFalso({"tipo": "optimizar", "planId": "p-1", "solicitud": {}})
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1


async def test_body_json_que_no_es_un_objeto_se_ackea():
    """Un JSON valido pero que no es un dict (una lista, un numero): el acceso por
    clave lanza TypeError, no KeyError - misma consecuencia."""
    mensaje = _MensajeCrudo(b'["no", "es", "un", "objeto"]')
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1


async def test_cabecera_x_intentos_no_numerica_se_ackea():
    """`int(...)` sobre una cabecera corrupta tambien corre antes del try."""
    mensaje = _MensajeCrudo(
        b'{"tipo": "optimizar", "planId": "p-1", "correlationId": "c-1", "solicitud": {}}',
        headers={"x-intentos": "muchos"},
    )
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1


async def test_mensaje_sin_tipo_se_ackea():
    """`_mover_a_dlq(..., cuerpo["tipo"], ...)` es la ultima indexacion directa que
    queda en el camino de error: si falta `tipo`, el propio camino a la DLQ estalla."""
    mensaje = _MensajeFalso(
        {"planId": "p-1", "correlationId": "c-1", "solicitud": {}},
        intentos_previos=worker_main.MAX_INTENTOS - 1,
    )
    publisher = _PublisherFalso()

    await worker_main._procesar(mensaje, publisher)

    assert mensaje.acks == 1
    assert [rk for rk, _ in publisher.crudos] == [worker_main.COLA_DLQ]


async def test_dos_mensajes_malformados_no_agotan_el_prefetch():
    """La consecuencia real del bug, no su mecanismo: con `prefetch_count=2`,
    dos mensajes sin ackear bloquean el worker por completo. Si los dos se
    confirman, el worker sigue disponible."""
    publisher = _PublisherFalso()
    mensajes = [
        _MensajeFalso({"tipo": "optimizar", "correlationId": f"c-{i}", "solicitud": {}},
                      intentos_previos=worker_main.MAX_INTENTOS - 1)
        for i in range(2)
    ]

    for m in mensajes:
        await worker_main._procesar(m, publisher)

    sin_ackear = [i for i, m in enumerate(mensajes) if m.acks == 0]
    assert not sin_ackear, f"Mensajes sin ackear: {sin_ackear}. Con prefetch_count=2 el worker queda bloqueado."


async def test_un_fallo_inesperado_no_publica_el_sql_crudo():
    """Lo que sale de aca lo LEE EL USUARIO (#344).

    NEXO guarda el mensaje del rechazo por linea y el front lo muestra tal
    cual. Con `str(exc)` de un error de SQLAlchemy, en QA se vio la consulta
    entera en pantalla -"... cargada IS 0 GROUP BY item_plan_cargue_id"-,
    seguida de "revisa las medidas y los pesos", que ademas desorienta porque
    los datos de la linea estaban bien.

    El detalle no se pierde: va al log y a la DLQ. Lo que no puede es viajar
    al usuario.
    """
    from sqlalchemy.exc import ProgrammingError

    async def _falla_como_sql_server(*_a, **_k):
        # El error exacto que se vio en QA: SQL Server rechaza `IS 0` con el 102.
        raise ProgrammingError(
            "SELECT colocaciones_cargue.item_plan_cargue_id, count(*) FROM "
            "colocaciones_cargue WHERE colocaciones_cargue.cargada IS 0 "
            "GROUP BY colocaciones_cargue.item_plan_cargue_id",
            {}, Exception("[42000] Incorrect syntax near '0'. (102)"))

    mensaje = _MensajeFalso(
        {"tipo": "optimizar", "correlationId": "c-sql", "planId": "p-1", "solicitud": {}},
        intentos_previos=worker_main.MAX_INTENTOS - 1,
    )
    publisher = _PublisherFalso()

    import pytest as _pytest
    with _pytest.MonkeyPatch.context() as mp:
        mp.setattr(worker_main, "_ejecutar_solicitud", _falla_como_sql_server)
        await worker_main._procesar(mensaje, publisher)

    assert publisher.rechazos, "no se publico el rechazo"
    codigo, texto = publisher.rechazos[0][1], publisher.rechazos[0][2]
    assert codigo == "ERROR_INTERNO"

    filtrado = [p for p in ("SELECT", "FROM", "WHERE", "GROUP BY", "IS 0", "IS 1", "Traceback")
                if p.lower() in texto.lower()]
    assert not filtrado, f"el mensaje al usuario trae detalle interno {filtrado}: {texto!r}"
    assert "c-sql" in texto, "sin el id de correlacion no hay como hallar el detalle en el log"
