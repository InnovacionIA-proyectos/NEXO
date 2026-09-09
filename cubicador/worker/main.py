"""Worker consumidor de RabbitMQ: procesa `optimizar` y `crear_desde_linea`
(las 2 operaciones pesadas, ver docs/DECISIONES_TECNICAS.md #5) fuera del
proceso de la API. Consume `cubicacion.solicitada` y publica
`plan-de-cargue.generado` o `cubicacion.rechazada` (ver CONTRATO.md
seccion 6).

Verificado contra un RabbitMQ real en Docker Compose (ver
docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2.3): mensaje
publicado -> worker consume -> optimiza contra Postgres -> publica el evento
de resultado -> el plan queda persistido.

Resiliencia (ver docs/DECISIONES_TECNICAS.md #10): reintentos limitados via
un contador en la cabecera del mensaje (no via TTL/DLX nativo de RabbitMQ,
para mantenerlo simple) - al agotarse, el mensaje se publica en
`cubicacion.dlq` para inspeccion manual Y se alerta al equipo por correo
(`infrastructure/alertas/correo.py::alertar_mensaje_en_dlq`). Un vigilante
periodico marca `error` los planes que llevan demasiado tiempo en
`procesando` (por si el worker se cae a mitad de un mensaje y este nunca
llega a fallar ni a completarse - un caso que el reintento por si solo no
cubre).
"""

from __future__ import annotations

import asyncio
import json
import logging
import os
from datetime import datetime, timedelta, timezone

import aio_pika

from api.dependencias import NEXO_BASE_URL, NEXO_CALLBACK_API_KEY
from application.excepciones import AppException
from application.plan_cargue.desde_linea import crear_plan_desde_linea
from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan
from application.plan_cargue.resultado import obtener_resultado
from domain.motor.dominio import PonderacionesCombinada
from infrastructure.alertas.correo import alertar_mensaje_en_dlq
from infrastructure.clientes.nexo_client import NexoLineaSolicitudClient
from infrastructure.mensajeria.rabbitmq import EXCHANGE, RabbitMqEventosPublisher, conectar_con_reintentos
from infrastructure.persistence.db import SessionLocal, crear_tablas
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

RABBITMQ_URL = os.environ.get("CUBICADOR_RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")
COLA = "cubicador.optimizar"
COLA_DLQ = "cubicacion.dlq"
MAX_INTENTOS = 3
# Umbrales del vigilante, configurables por ambiente (S3-HU-06.1): un entorno
# con planes grandes puede necesitar mas de 5 min antes de dar uno por perdido,
# y bajar el intervalo acelera la deteccion a costa de mas consultas.
TIMEOUT_PROCESANDO_MINUTOS = int(os.environ.get("CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS", "5"))
INTERVALO_VIGILANTE_SEGUNDOS = int(os.environ.get("CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS", "60"))
# Codigo de rechazo propio del vigilante - no lo produce ningun caso de uso,
# solo este camino (ver CONTRATO.md seccion 6).
CODIGO_RECHAZO_TIMEOUT = "TIMEOUT_PROCESAMIENTO"
logger = logging.getLogger("cubicador.worker")


async def _ejecutar_solicitud(cuerpo: dict, planes: SqlPlanCargueRepository, vehiculos: SqlVehiculoCubicacionRepository) -> str:
    """Corre el caso de uso pesado. Devuelve el plan_id resultante (para
    `optimizar` es el mismo que llego en el mensaje; para `crear_desde_linea`
    es nuevo, se crea aqui)."""
    tipo = cuerpo["tipo"]
    if tipo == "optimizar":
        plan_id = cuerpo["planId"]
        solicitud = cuerpo["solicitud"]
        ponderaciones = (
            PonderacionesCombinada(**solicitud["ponderaciones"]) if solicitud.get("ponderaciones") else None
        )
        parametros = ParametrosOptimizacionInput(
            estrategia=solicitud.get("estrategia", "Combinada"),
            algoritmo=solicitud.get("algoritmo", "bfd"),
            semilla=solicitud.get("semilla"),
            ponderaciones=ponderaciones,
            respetar_bloqueadas=solicitud.get("respetarBloqueadas", False),
        )
        await optimizar_plan(planes, vehiculos, plan_id, parametros)
        return plan_id

    if tipo == "crear_desde_linea":
        lineas = NexoLineaSolicitudClient(NEXO_BASE_URL, NEXO_CALLBACK_API_KEY)
        plan = await crear_plan_desde_linea(planes, vehiculos, lineas, cuerpo["solicitud"]["lineaSolicitudId"])
        return plan.id

    raise ValueError(f"Tipo de mensaje desconocido: {tipo}")


async def _procesar(mensaje: aio_pika.IncomingMessage, publisher: RabbitMqEventosPublisher) -> None:
    cuerpo = json.loads(mensaje.body)
    # Issue #28: NEXO publica CubicacionSolicitada con MassTransit real, que
    # envuelve el payload real bajo la clave "message" (mas messageType, etc
    # - ver NEXO/docs/mensajeria/CUBICACION.md #1/#4). Cubicador no necesita
    # nada de MassTransit para si mismo (sus propios tests y su API siguen
    # publicando el payload plano, sin sobre) - por eso el desenvuelto es
    # opcional: si no hay "message", cuerpo queda igual que antes. Verificado
    # en runtime contra un mensaje real de NEXO.
    if isinstance(cuerpo, dict) and "message" in cuerpo and "messageType" in cuerpo:
        cuerpo = cuerpo["message"]
    correlation_id = cuerpo["correlationId"]
    intentos_previos = int((mensaje.headers or {}).get("x-intentos", 0))

    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        try:
            plan_id = await _ejecutar_solicitud(cuerpo, planes, vehiculos)
            resultado = await obtener_resultado(planes, vehiculos, plan_id)
            await publisher.publicar_plan_generado(plan_id, {
                "estado": resultado.plan.estado, "puntaje": resultado.plan.puntaje,
                "colocaciones": len(resultado.colocaciones),
            })
            await mensaje.ack()
        except AppException as exc:
            # Error de negocio (validacion, no encontrado, etc.): no tiene sentido
            # reintentar, es el mismo resultado siempre. Si ya existia un plan
            # (tipo=optimizar), se marca error; si no (crear_desde_linea fallo
            # antes de crear nada), solo se notifica.
            if cuerpo["tipo"] == "optimizar":
                await planes.marcar_error(cuerpo["planId"], exc.mensaje)
            await publisher.publicar_cubicacion_rechazada(correlation_id, exc.codigo, exc.mensaje)
            await mensaje.ack()
        except Exception as exc:  # noqa: BLE001 - fallo inesperado (bug, DB caida, etc.)
            logger.exception("Fallo procesando mensaje (intento %d/%d)", intentos_previos + 1, MAX_INTENTOS)
            if intentos_previos + 1 >= MAX_INTENTOS:
                await _mover_a_dlq(mensaje, publisher, cuerpo["tipo"], str(exc), intentos_previos + 1)
                if cuerpo["tipo"] == "optimizar":
                    await planes.marcar_error(cuerpo["planId"], f"Fallo tras {MAX_INTENTOS} intentos: {exc}")
                await publisher.publicar_cubicacion_rechazada(correlation_id, "ERROR_INTERNO", str(exc))
                await mensaje.ack()
            else:
                await _reencolar(mensaje, publisher, intentos_previos + 1)
                await mensaje.ack()  # el reintento se publica aparte; este mensaje ya cumplio su ciclo


async def _reencolar(mensaje: aio_pika.IncomingMessage, publisher: RabbitMqEventosPublisher, intentos: int) -> None:
    await publisher.publicar_crudo("cubicacion.solicitada", mensaje.body, headers={"x-intentos": intentos})


async def _mover_a_dlq(
    mensaje: aio_pika.IncomingMessage, publisher: RabbitMqEventosPublisher, tipo: str, error: str, intentos: int,
) -> None:
    await publisher.publicar_crudo(COLA_DLQ, mensaje.body, headers={"x-intentos": intentos, "x-error": error[:1000]})
    logger.error("Mensaje movido a %s tras %d intentos: %s", COLA_DLQ, intentos, error)
    await alertar_mensaje_en_dlq(tipo, error, intentos)


def _mensaje_timeout() -> str:
    return (
        f"Timeout: el plan quedo mas de {TIMEOUT_PROCESANDO_MINUTOS} min en 'procesando' "
        "(el worker probablemente se cayo a mitad del calculo)."
    )


async def _revisar_timeouts(publisher: RabbitMqEventosPublisher) -> list[str]:
    """UNA pasada del vigilante: resuelve los planes atascados en `procesando`.
    Devuelve los ids resueltos. Sin bucle ni sleep a proposito - el "cuando" es
    responsabilidad de `_vigilante_de_timeouts`, y separarlos es lo que hace
    esto testeable sin relojes de por medio.

    Por cada plan atascado PUBLICA el rechazo y RECIEN DESPUES lo marca
    `error`. Ese orden es el punto de S3-HU-06.1, no un detalle: si se marcara
    primero y el broker estuviera caido, el plan saldria de `procesando` sin
    que nadie se enterara y el proximo ciclo ya no lo encontraria - NEXO se
    quedaria en `procesando` para siempre, que es exactamente el hueco que
    esta issue cierra. Publicando primero, un fallo del broker deja el plan
    como estaba y el proximo ciclo reintenta. El costo es la posibilidad de un
    rechazo duplicado (si el proceso muere entre publicar y marcar), inofensivo
    porque el consumer de NEXO solo vuelve a poner `Estado = "error"`. Es el
    mismo criterio que el Outbox transaccional de NEXO: publicar antes de
    persistir."""
    limite = datetime.now(timezone.utc) - timedelta(minutes=TIMEOUT_PROCESANDO_MINUTOS)
    mensaje = _mensaje_timeout()
    resueltos: list[str] = []

    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        for plan_id in await planes.obtener_atascados(limite):
            # El plan_id va tambien como correlationId, y es el valor CORRECTO,
            # no una aproximacion: el unico punto de todo el repo que pone un
            # plan en `procesando` es POST /{id}/optimizar
            # (api/routers/planes_cargue.py), y para ese flujo NEXO publica el
            # mismo valor en ambos campos (CubicacionPublicadorAdapter).
            # `crear_desde_linea` no cuenta: sus planes nacen en `borrador` y
            # nunca pasan por `marcar_procesando`, asi que el vigilante no los
            # alcanza. INVARIANTE A SOSTENER: si algun comando nuevo empieza a
            # marcar `procesando` con un correlationId distinto del planId (hoy
            # ninguno de los 8 de S3-HU-02 lo hace), hay que guardar ese
            # correlationId en la fila del plan y leerlo aqui.
            await publisher.publicar_cubicacion_rechazada(plan_id, CODIGO_RECHAZO_TIMEOUT, mensaje)
            await planes.marcar_error(plan_id, mensaje)
            resueltos.append(plan_id)

    if resueltos:
        logger.warning(
            "Vigilante: %d plan(es) marcados 'error' por timeout de procesamiento y rechazados (%s).",
            len(resueltos), CODIGO_RECHAZO_TIMEOUT,
        )
    return resueltos


async def _vigilante_de_timeouts(publisher: RabbitMqEventosPublisher) -> None:
    """Corre en loop propio: cada `INTERVALO_VIGILANTE_SEGUNDOS` dispara una
    pasada de `_revisar_timeouts`. Cubre el caso que el reintento por
    `x-intentos` no cubre - que el worker se caiga a mitad de un mensaje, sin
    llegar ni a fallar ni a completar."""
    while True:
        await asyncio.sleep(INTERVALO_VIGILANTE_SEGUNDOS)
        try:
            await _revisar_timeouts(publisher)
        except Exception:  # noqa: BLE001 - el vigilante no debe morir por un fallo puntual
            logger.exception("Fallo en el vigilante de timeouts (se reintenta en el proximo ciclo).")


async def main() -> None:
    logging.basicConfig(level=logging.INFO)
    await crear_tablas()

    publisher = RabbitMqEventosPublisher(RABBITMQ_URL)
    await publisher.conectar()

    conexion = await conectar_con_reintentos(RABBITMQ_URL)
    async with conexion:
        canal = await conexion.channel()
        await canal.set_qos(prefetch_count=2)  # ver docs/DECISIONES_TECNICAS.md #10: prefetch bajo
        exchange = await canal.declare_exchange(EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True)
        cola = await canal.declare_queue(COLA, durable=True)
        await cola.bind(exchange, routing_key="cubicacion.solicitada")
        # Cola de mensajes agotados, para inspeccion manual - routing key propia,
        # ligada al mismo exchange. Nadie la CONSUME automaticamente (el mensaje
        # se queda ahi para revisión manual), pero SÍ se alerta por correo en el
        # momento en que un mensaje entra (ver _mover_a_dlq).
        cola_dlq = await canal.declare_queue(COLA_DLQ, durable=True)
        await cola_dlq.bind(exchange, routing_key=COLA_DLQ)

        vigilante = asyncio.create_task(_vigilante_de_timeouts(publisher))
        logger.info(
            "Worker escuchando en '%s' (routing_key=cubicacion.solicitada); vigilante cada %ds, timeout %dmin.",
            COLA, INTERVALO_VIGILANTE_SEGUNDOS, TIMEOUT_PROCESANDO_MINUTOS,
        )
        try:
            await cola.consume(lambda m: _procesar(m, publisher))
            await asyncio.Future()  # correr para siempre
        finally:
            vigilante.cancel()


if __name__ == "__main__":
    asyncio.run(main())
