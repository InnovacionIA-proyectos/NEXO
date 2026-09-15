"""Worker consumidor de RabbitMQ: procesa `optimizar` y `crear_desde_linea`
(las 2 operaciones pesadas, ver docs/DECISIONES_TECNICAS.md #5) mas, desde
S3-HU-02 (issue #105), los 8 comandos de escritura del plan de cargue
(crear_plan, crear_plan_demo, guardar_vehiculo, eliminar_vehiculo, guardar_item,
importar_items, eliminar_item, eliminar_plan, ajustar_colocacion) - antes solo llegaban por el
POST/DELETE/PATCH sincrono de `api/routers/planes_cargue.py` directo contra
esta base; ahora TODA escritura, pesada o no, pasa por esta misma cola. Consume
`cubicacion.solicitada` y publica `plan-de-cargue.generado` o
`cubicacion.rechazada` (ver CONTRATO.md seccion 6).

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
from api.dtos.plan_cargue import (
    GuardarItemPlanCargueInput as GuardarItemPlanCargueInputDto,
    GuardarVehiculoInput as GuardarVehiculoInputDto,
    ResultadoPlanDto,
    VehiculoCubicacionDto,
)
from application.excepciones import AppException
from application.plan_cargue import ajustar as ajustar_uc
from application.plan_cargue import demo as demo_uc
from application.plan_cargue import items as items_uc
from application.plan_cargue import mapeos
from application.plan_cargue import planes as planes_uc
from application.plan_cargue import vehiculos as vehiculos_uc
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


#: Tipos que NO terminan con un plan vivo para reportar: "guardar_vehiculo" y
#: "eliminar_vehiculo" no tienen plan (son catalogo suelto); "eliminar_plan" ya borro la
#: fila que reportaria. Ver `_procesar`: para estos no se llama a `obtener_resultado` ni
#: se publica "plan-de-cargue.generado" (S3-HU-02, issue #105).
TIPOS_SIN_RESULTADO_FINAL = frozenset({"guardar_vehiculo", "eliminar_vehiculo", "eliminar_plan"})


def _ajuste_de_solicitud(solicitud: dict) -> "ajustar_uc.AjusteColocacionInput":
    return ajustar_uc.AjusteColocacionInput(
        x=solicitud.get("x"), y=solicitud.get("y"), z=solicitud.get("z"),
        orientacion_indice=solicitud.get("orientacionIndice"), bloqueada=solicitud.get("bloqueada"),
        retirar=bool(solicitud.get("retirar")), reinsertar=bool(solicitud.get("reinsertar")),
    )


async def _ejecutar_solicitud(
    cuerpo: dict, planes: SqlPlanCargueRepository, vehiculos: SqlVehiculoCubicacionRepository,
    publisher: RabbitMqEventosPublisher,
) -> str | None:
    """Corre el caso de uso. Devuelve el plan_id resultante para que `_procesar`
    reporte "plan-de-cargue.generado" (None para los tipos en
    `TIPOS_SIN_RESULTADO_FINAL`, que no tienen plan que reportar).

    S3-HU-02 (issue #105): los 8 comandos nuevos reutilizan EXACTAMENTE los mismos
    casos de uso que ya usa `api/routers/planes_cargue.py` (el POST sincrono que este
    trabajo reemplaza) - nada de logica de negocio nueva acá, solo el cableado
    cola -> caso de uso -> escritura real en la base (antes solo llegaba por HTTP
    directo).

    S3-HU-03 (issue #110): `guardar_vehiculo` es el UNICO tipo que publica su
    propio evento acá mismo (vehiculo.creado/vehiculo.actualizado) en vez de
    dejarselo a `_procesar` - no tiene un plan_id que reportar via
    "plan-de-cargue.generado", y create-vs-update solo se sabe ANTES de guardar
    (¿ya existia esa fila?). Si publicar falla, la excepcion sube igual que
    cualquier otro fallo de este metodo: reintento normal (guardar_vehiculo es
    upsert, reintentarlo es seguro)."""
    tipo = cuerpo["tipo"]
    solicitud = cuerpo.get("solicitud") or {}

    if tipo == "optimizar":
        plan_id = cuerpo["planId"]
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
        plan = await crear_plan_desde_linea(planes, vehiculos, lineas, solicitud["lineaSolicitudId"])
        return plan.id

    if tipo == "crear_plan":
        # El plan_id lo genero NEXO (S3-HU-01.2): se crea la fila REAL con ESE id,
        # nunca uno propio (HU S3-HU-02, criterio 1).
        plan = await planes_uc.crear_plan(planes, solicitud["nombre"], solicitud["vehiculoId"], id=cuerpo["planId"])
        return plan.id

    if tipo == "crear_plan_demo":
        plan = await demo_uc.crear_plan_demo(planes, vehiculos)
        return plan.id

    if tipo == "guardar_vehiculo":
        dto = GuardarVehiculoInputDto.model_validate(solicitud)
        existia = dto.id is not None and await vehiculos.obtener(dto.id) is not None
        guardado = await vehiculos_uc.guardar_vehiculo(vehiculos, mapeos.vehiculo_de_input(dto))
        payload_vehiculo = VehiculoCubicacionDto.model_validate(guardado).model_dump(mode="json", by_alias=True)
        if existia:
            await publisher.publicar_vehiculo_actualizado(guardado.id, payload_vehiculo)
        else:
            await publisher.publicar_vehiculo_creado(guardado.id, payload_vehiculo)
        return None

    if tipo == "eliminar_vehiculo":
        # El id del vehiculo viaja en planId, igual que en "guardar_vehiculo": los
        # comandos de catalogo no tienen plan y reutilizan ese campo del sobre.
        # eliminar_vehiculo lanza ConflictException("VEHICULO_EN_USO") si el vehiculo
        # tiene planes asociados - es AppException, asi que `_procesar` publica el
        # rechazo y NO reintenta (el resultado seria el mismo siempre).
        vehiculo_id = cuerpo["planId"]
        await vehiculos_uc.eliminar_vehiculo(vehiculos, vehiculo_id)
        # Cierra el trio del catalogo junto a vehiculo.creado/actualizado (S3-HU-03):
        # sin este evento la proyeccion de NEXO se queda con el vehiculo para siempre
        # y GET /api/vehiculos seguiria ofreciendolo. Se publica DESPUES del borrado:
        # si el caso de uso rechaza (VEHICULO_EN_USO), no hay nada que anunciar.
        await publisher.publicar_vehiculo_eliminado(vehiculo_id)
        return None

    if tipo == "guardar_item":
        plan_id = cuerpo["planId"]
        dto = GuardarItemPlanCargueInputDto.model_validate(solicitud)
        await items_uc.guardar_item(planes, plan_id, dto.id, mapeos.item_de_input(dto, plan_id))
        return plan_id

    if tipo == "importar_items":
        plan_id = cuerpo["planId"]
        entidades = [
            mapeos.item_de_input(GuardarItemPlanCargueInputDto.model_validate(i), plan_id)
            for i in solicitud["items"]
        ]
        await items_uc.importar_items(planes, plan_id, entidades)
        return plan_id

    if tipo == "eliminar_item":
        plan_id = cuerpo["planId"]
        await items_uc.eliminar_item(planes, plan_id, solicitud["itemId"])
        return plan_id

    if tipo == "eliminar_plan":
        await planes_uc.eliminar_plan(planes, cuerpo["planId"])
        return None

    if tipo == "ajustar_colocacion":
        plan_id = cuerpo["planId"]
        await ajustar_uc.ajustar_colocacion(
            planes, vehiculos, plan_id, solicitud["colocacionId"], _ajuste_de_solicitud(solicitud),
        )
        return plan_id

    raise ValueError(f"Tipo de mensaje desconocido: {tipo}")


async def _procesar(mensaje: aio_pika.IncomingMessage, publisher: RabbitMqEventosPublisher) -> None:
    # S3-HU-10.1 (issue #131): TODO lo que corre antes del `try` de mas abajo va
    # dentro de esta guarda. Ahi no habia ningun manejador de errores, asi que
    # cualquier fallo -body ilegible, JSON que no es un objeto, falta
    # `correlationId`, cabecera `x-intentos` corrupta- lanzaba antes del `ack` y
    # dejaba el mensaje unacknowledged PARA SIEMPRE. Con prefetch_count=2 bastan dos
    # mensajes asi para que el worker deje de procesar cualquier cosa, y reiniciarlo
    # no ayuda: RabbitMQ los reentrega y se vuelven a atascar.
    #
    # Un mensaje que ni siquiera se puede interpretar no tiene reintento posible (el
    # resultado seria identico), asi que va derecho a la DLQ con su alerta y se
    # ackea. Perder un mensaje corrupto es aceptable; perder el worker, no.
    try:
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
        if not isinstance(cuerpo, dict):
            raise ValueError(f"El cuerpo del mensaje no es un objeto JSON: {type(cuerpo).__name__}")
        correlation_id = cuerpo.get("correlationId")
        intentos_previos = int((mensaje.headers or {}).get("x-intentos", 0))
    except Exception as exc:  # noqa: BLE001 - mensaje ilegible: nunca reintentable
        logger.exception("Mensaje ilegible, va directo a %s sin reintentar.", COLA_DLQ)
        await _mover_a_dlq(mensaje, publisher, "desconocido", f"Mensaje ilegible: {exc}", 0)
        await mensaje.ack()
        return

    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        try:
            plan_id = await _ejecutar_solicitud(cuerpo, planes, vehiculos, publisher)
            # S3-HU-02 (issue #105): "guardar_vehiculo" no tiene plan que reportar y
            # "eliminar_plan" ya borro el suyo - para esos dos no hay
            # "plan-de-cargue.generado" que publicar (ver TIPOS_SIN_RESULTADO_FINAL).
            if plan_id is not None and cuerpo["tipo"] not in TIPOS_SIN_RESULTADO_FINAL:
                resultado = await obtener_resultado(planes, vehiculos, plan_id)
                # S3-HU-03 (issue #109/#110): enriquecido con el `resultado` COMPLETO
                # (items, colocaciones con geometria, metricas, incidencias) - no solo
                # el conteo de antes. Reusa el mismo DTO que ya arma el GET sincrono
                # (`ResultadoPlanDto`), medido en docs/decisiones/
                # medicion-evento-enriquecido-plan-cargue.md ANTES de escribir esto.
                payload = ResultadoPlanDto.model_validate(resultado).model_dump(mode="json", by_alias=True)
                await publisher.publicar_plan_generado(plan_id, payload)
            await mensaje.ack()
        except AppException as exc:
            # Error de negocio (validacion, no encontrado, etc.): no tiene sentido
            # reintentar, es el mismo resultado siempre. `marcar_error` es un no-op
            # silencioso si el planId no corresponde a ningun plan existente
            # (p.ej. "guardar_vehiculo", o "crear_plan"/"crear_desde_linea" fallando
            # antes de crear nada) - por eso se llama igual para los 9 tipos, sin
            # bifurcar por tipo (antes solo cubria "optimizar").
            #
            # S3-HU-10.1 (issue #131): `.get`, NO indexacion directa. Este bloque
            # existe para REPORTAR un fallo; si el reporte lanza su propia excepcion
            # nos quedamos sin llegar al `ack()` de abajo y el mensaje queda
            # unacknowledged para siempre - con prefetch_count=2, dos mensajes asi
            # bloquean el worker entero y ni reiniciarlo lo recupera.
            await planes.marcar_error(cuerpo.get("planId"), exc.mensaje)
            await publisher.publicar_cubicacion_rechazada(correlation_id, exc.codigo, exc.mensaje)
            await mensaje.ack()
        except Exception as exc:  # noqa: BLE001 - fallo inesperado (bug, DB caida, etc.)
            logger.exception("Fallo procesando mensaje (intento %d/%d)", intentos_previos + 1, MAX_INTENTOS)
            if intentos_previos + 1 >= MAX_INTENTOS:
                # `.get` por lo mismo que el `planId` de abajo: es la ultima
                # indexacion directa del camino de error, y si falta `tipo` el propio
                # camino a la DLQ estallaria antes del ack (S3-HU-10.1, issue #131).
                await _mover_a_dlq(
                    mensaje, publisher, cuerpo.get("tipo", "desconocido"), str(exc), intentos_previos + 1,
                )
                # S3-HU-10.1 (issue #131): `.get` por lo mismo que arriba - es la
                # linea que el mensaje sin planId de ci_docker_smoke.py hacia estallar
                # en cada corrida, justo antes del ack.
                await planes.marcar_error(cuerpo.get("planId"), f"Fallo tras {MAX_INTENTOS} intentos: {exc}")
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
