"""Publicador de eventos hacia RabbitMQ (ver CONTRATO.md seccion 6):
exchange `cubicador.eventos` (topic), routing keys `cubicacion.solicitada`,
`plan-de-cargue.generado`, `cubicacion.rechazada` y, desde S3-HU-03 (issue
#110), `vehiculo.creado`/`vehiculo.actualizado`.

Verificado contra un RabbitMQ real en Docker Compose (ver
docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2): flujo completo
API -> publica -> worker consume -> persiste -> publica evento de resultado,
mas el camino de reintentos/DLQ via `publicar_crudo`.

Issue #28 (S2-HU-075): `plan-de-cargue.generado`/`cubicacion.rechazada` -los
2 eventos que consume NEXO con un IConsumer<T> real de MassTransit- se
publican envueltos en el "sobre" minimo de MassTransit (`_sobre_masstransit`)
para que ese consumer los pueda deserializar. `cubicacion.solicitada` NO
lleva sobre cuando lo publica Cubicador (nadie mas lo consume) - pero SI
puede llegar envuelto cuando lo publica NEXO, y el worker lo desenvuelve con
un fallback transparente (ver worker/main.py::_procesar). Ver NEXO/docs/
mensajeria/CUBICACION.md #1/#4 para el detalle completo del porque.
"""

from __future__ import annotations

import asyncio
import json
import logging
import uuid
from datetime import UTC, datetime
from typing import Any

import aio_pika

EXCHANGE = "cubicador.eventos"
logger = logging.getLogger("cubicador.rabbitmq")

# Namespace C# real de los mensajes que SI consume NEXO con un IConsumer<T>
# de MassTransit - ver NEXO/src/CabinaFletes.Mensajes/Cubicacion/
# MensajesDeCubicacion.cs. Acoplamiento cruzado real: si NEXO renombra esas
# clases o el namespace, este valor queda desincronizado sin ningun error de
# compilacion que lo avise - solo se nota si un mensaje deja de entregarse.
_NAMESPACE_MENSAJES_NEXO = "CabinaFletes.Mensajes.Cubicacion"


def _sobre_masstransit(tipo_csharp: str, payload: dict[str, Any]) -> dict[str, Any]:
    """Envuelve `payload` en el "sobre" minimo que un IConsumer<T> real de
    MassTransit necesita para deserializarlo (ver NEXO/docs/mensajeria/
    CUBICACION.md #1/#4) - solo hace falta para los eventos que SI consume
    NEXO (`plan-de-cargue.generado`/`cubicacion.rechazada`), no para
    `cubicacion.solicitada` (Cubicador-a-Cubicador, nadie mas lo lee).
    Verificado contra un IConsumer<T> real: MassTransit acepta este sobre
    minimo (messageType + message), no hace falta el resto de los campos
    opcionales del formato completo (host, headers, etc.)."""
    return {
        "messageId": str(uuid.uuid4()),
        "messageType": [f"urn:message:{_NAMESPACE_MENSAJES_NEXO}:{tipo_csharp}"],
        "message": payload,
        "sentTime": datetime.now(UTC).isoformat(),
    }


async def conectar_con_reintentos(url: str, intentos: int = 30, espera_s: float = 2.0) -> aio_pika.RobustConnection:
    """Reintenta la conexion INICIAL a RabbitMQ. Cubre dos carreras reales de
    arranque en Docker Compose:

    1. El healthcheck de RabbitMQ puede dar 'sano' un instante antes de que
       el puerto AMQP acepte conexiones de verdad (medido: hasta ~22s de hueco
       en un arranque en frio) - sin reintento aqui eso tumbaba el contenedor
       completo (`connect_robust` solo reintenta reconexiones DESPUES de
       conectar una vez, no el primer intento).
    2. Desde que el broker es el `rabbit` COMPARTIDO de NEXO (docker-compose.yml
       raiz, proyecto Compose separado - ver DECISIONES_TECNICAS.md #9), no hay
       forma de expresar `depends_on: rabbit: condition: service_healthy` entre
       proyectos distintos: este stack puede arrancar sin ninguna garantia de
       que el de NEXO ya este corriendo. El margen por defecto (30 intentos x
       2s = ~60s) cubre tanto el arranque en frio del broker como que alguien
       levante este stack un poco antes que el de NEXO.

    Ver docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md."""
    ultimo_error: Exception | None = None
    for intento in range(1, intentos + 1):
        try:
            return await aio_pika.connect_robust(url)
        except Exception as exc:  # noqa: BLE001 - cualquier fallo de conexion inicial se reintenta igual
            ultimo_error = exc
            if intento < intentos:
                logger.warning("No se pudo conectar a RabbitMQ (intento %d/%d): %s", intento, intentos, exc)
                await asyncio.sleep(espera_s)
    assert ultimo_error is not None
    raise ultimo_error


class RabbitMqEventosPublisher:
    def __init__(self, url: str) -> None:
        self._url = url
        self._connection: aio_pika.RobustConnection | None = None
        self._exchange: aio_pika.Exchange | None = None

    async def conectar(self) -> None:
        self._connection = await conectar_con_reintentos(self._url)
        canal = await self._connection.channel()
        self._exchange = await canal.declare_exchange(EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True)

    async def cerrar(self) -> None:
        if self._connection is not None:
            await self._connection.close()

    async def _publicar(self, routing_key: str, payload: dict[str, Any]) -> None:
        assert self._exchange is not None, "Llamar a conectar() antes de publicar."
        mensaje = aio_pika.Message(
            body=json.dumps(payload).encode("utf-8"),
            content_type="application/json",
            delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
        )
        await self._exchange.publish(mensaje, routing_key=routing_key)

    async def publicar_crudo(self, routing_key: str, body: bytes, headers: dict[str, Any] | None = None) -> None:
        """Republica un mensaje tal cual (usado por el worker para reintentos y DLQ - ver worker/main.py)."""
        assert self._exchange is not None, "Llamar a conectar() antes de publicar."
        mensaje = aio_pika.Message(
            body=body, content_type="application/json",
            delivery_mode=aio_pika.DeliveryMode.PERSISTENT, headers=headers or {},
        )
        await self._exchange.publish(mensaje, routing_key=routing_key)

    async def publicar_cubicacion_solicitada(self, plan_id: str, tipo: str, solicitud: dict[str, Any]) -> None:
        await self._publicar("cubicacion.solicitada", {
            "tipo": tipo, "planId": plan_id, "correlationId": plan_id, "solicitud": solicitud,
        })

    async def publicar_plan_generado(self, plan_id: str, resultado: dict[str, Any]) -> None:
        payload = {"correlationId": plan_id, "planId": plan_id, "resultado": resultado}
        await self._publicar("plan-de-cargue.generado", _sobre_masstransit("PlanDeCargueGenerado", payload))

    async def publicar_cubicacion_rechazada(self, plan_id: str, codigo: str, mensaje: str) -> None:
        payload = {"correlationId": plan_id, "planId": plan_id, "codigo": codigo, "mensaje": mensaje}
        await self._publicar("cubicacion.rechazada", _sobre_masstransit("CubicacionRechazada", payload))

    # S3-HU-03 (issue #110): catalogo de vehiculos - NEXO proyecta estos 2 eventos
    # para no tener que llamar a Cubicador cada vez que alguien lee GET /api/vehiculos
    # (issue #112). Mismo "sobre" de MassTransit que plan-de-cargue.generado/
    # cubicacion.rechazada: los consume un IConsumer<T> real del lado de NEXO.
    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None:
        payload = {"correlationId": vehiculo_id, "vehiculo": vehiculo}
        await self._publicar("vehiculo.creado", _sobre_masstransit("VehiculoCreado", payload))

    async def publicar_vehiculo_actualizado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None:
        payload = {"correlationId": vehiculo_id, "vehiculo": vehiculo}
        await self._publicar("vehiculo.actualizado", _sobre_masstransit("VehiculoActualizado", payload))

    # Cierra el trio del catalogo. Sin este evento la proyeccion de NEXO se queda con
    # el vehiculo para siempre: GET /api/vehiculos (issue #112) lee de ahi, asi que
    # seguiria ofreciendo un vehiculo que ya no existe en esta base, y el crear_plan
    # que lo eligiera fallaria con VEHICULO_INVALIDO. Solo lleva el id: no hay fila
    # que proyectar, solo una que borrar.
    async def publicar_vehiculo_eliminado(self, vehiculo_id: str) -> None:
        payload = {"correlationId": vehiculo_id, "vehiculoId": vehiculo_id}
        await self._publicar("vehiculo.eliminado", _sobre_masstransit("VehiculoEliminado", payload))
