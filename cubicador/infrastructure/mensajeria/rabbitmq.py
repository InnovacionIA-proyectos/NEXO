"""Publicador de eventos hacia RabbitMQ (ver docs/CONTRATOS.md seccion B):
exchange `cubicador.eventos` (topic), routing keys `cubicacion.solicitada`,
`plan-de-cargue.generado`, `cubicacion.rechazada`, `plan-de-cargue.ajustado`.

Verificado contra un RabbitMQ real en Docker Compose (ver
docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2): flujo completo
API -> publica -> worker consume -> persiste -> publica evento de resultado,
mas el camino de reintentos/DLQ via `publicar_crudo`.
"""

from __future__ import annotations

import asyncio
import json
import logging
from typing import Any

import aio_pika

EXCHANGE = "cubicador.eventos"
logger = logging.getLogger("cubicador.rabbitmq")


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
        await self._publicar("plan-de-cargue.generado", {
            "correlationId": plan_id, "planId": plan_id, "resultado": resultado,
        })

    async def publicar_cubicacion_rechazada(self, plan_id: str, codigo: str, mensaje: str) -> None:
        await self._publicar("cubicacion.rechazada", {
            "correlationId": plan_id, "planId": plan_id, "codigo": codigo, "mensaje": mensaje,
        })
