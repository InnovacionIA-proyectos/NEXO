"""Trazas, metricas y logs del Cubicador hacia el colector central (Alloy ->
Prometheus / Loki / Jaeger), el mismo al que reporta nexo-api.

Se enciende solo con `OTEL_EXPORTER_OTLP_ENDPOINT` (OTLP por HTTP, puerto
4318). Sin esa variable no se exporta nada: quedan los logs de consola, que
igual ganan fecha, nivel y el id de traza. El ambiente viaja en la variable
estandar `OTEL_RESOURCE_ATTRIBUTES=deployment.environment.name=qa`.

La traza cruza RabbitMQ a mano y no con la instrumentacion de aio-pika porque
del otro lado esta MassTransit, que NO usa la cabecera W3C: escribe y lee el
contexto en la cabecera AMQP `MT-Activity-Id` (mismo formato que
`traceparent`). Comprobado contra MassTransit 8.5.10 y RabbitMQ reales: con
solo `traceparent`, o con la cabecera dentro del sobre, NEXO abre una traza
nueva. Por eso al publicar van las dos cabeceras AMQP y al consumir se acepta
cualquiera.
"""

from __future__ import annotations

import logging
import os
import time
from contextlib import contextmanager
from typing import Any, Iterator

from opentelemetry import metrics, propagate, trace
from opentelemetry.trace import SpanKind, Status, StatusCode

_NOMBRE = "cubicador"
_CABECERA_MASSTRANSIT = "MT-Activity-Id"

tracer = trace.get_tracer(_NOMBRE)
_medidor = metrics.get_meter(_NOMBRE)
_mensajes = _medidor.create_counter(
    "cubicador.mensajes", description="Mensajes de la cola procesados por el worker, por tipo y resultado.")
_duracion = _medidor.create_histogram(
    "cubicador.mensaje.duracion", unit="s", description="Tiempo que el worker tarda en procesar un mensaje.")
# Latido: vale 1 mientras el proceso vive. El worker no emite ninguna otra metrica hasta que llega
# un mensaje, asi que sin esto "sin trabajo" y "caido" se verian igual desde el monitoreo.
_medidor.create_observable_gauge(
    "cubicador.activo", callbacks=[lambda _: [metrics.Observation(1)]],
    description="1 mientras el proceso esta vivo.")

#: Resultados de un mensaje que cuentan como fallo del servicio (no de negocio).
_RESULTADOS_DE_FALLO = frozenset({"reintento", "dlq", "ilegible", "error"})


class _FiltroDeTraza(logging.Filter):
    """Pone en cada linea de log el id de la traza en curso: con el se abre en
    Jaeger el recorrido completo de esa peticion o de ese mensaje."""

    def filter(self, record: logging.LogRecord) -> bool:
        contexto = trace.get_current_span().get_span_context()
        record.traza = format(contexto.trace_id, "032x") if contexto.is_valid else "-"
        return True


def configurar(servicio: str, app: Any = None) -> None:
    """Llamar UNA vez al arrancar el proceso. `app` es la aplicacion FastAPI
    (la API); el worker no la pasa."""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)-7s %(name)s [traza=%(traza)s] %(message)s")
    for manejador in logging.getLogger().handlers:
        manejador.addFilter(_FiltroDeTraza())
    # uvicorn trae su propio formato y no pasa por el logger raiz: se le quita para que sus
    # lineas salgan igual que las demas y lleguen tambien al colector.
    for nombre in ("uvicorn", "uvicorn.error", "uvicorn.access"):
        logging.getLogger(nombre).handlers.clear()
        logging.getLogger(nombre).propagate = True
    # La sonda de salud llega cada pocos segundos: su linea de acceso solo hace bulto.
    logging.getLogger("uvicorn.access").addFilter(lambda registro: "/health" not in registro.getMessage())

    if not os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT"):
        return

    # Importes aca adentro: sin colector configurado no hace falta cargar el SDK ni los exportadores.
    from opentelemetry._logs import set_logger_provider
    from opentelemetry.exporter.otlp.proto.http._log_exporter import OTLPLogExporter
    from opentelemetry.exporter.otlp.proto.http.metric_exporter import OTLPMetricExporter
    from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
    from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
    from opentelemetry.instrumentation.sqlalchemy import SQLAlchemyInstrumentor
    from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
    from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
    from opentelemetry.sdk.metrics import MeterProvider
    from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
    from opentelemetry.sdk.resources import Resource
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor

    from infrastructure.persistence.db import engine

    # job en Prometheus = "nexo/<servicio>" (namespace/servicio), la convencion del servidor de monitoreo.
    recurso = Resource.create({"service.name": servicio, "service.namespace": "nexo"})

    trazas = TracerProvider(resource=recurso, sampler=sin_sondeos())
    trazas.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
    trace.set_tracer_provider(trazas)

    metrics.set_meter_provider(MeterProvider(
        resource=recurso, metric_readers=[PeriodicExportingMetricReader(OTLPMetricExporter())]))

    registros = LoggerProvider(resource=recurso)
    registros.add_log_record_processor(BatchLogRecordProcessor(OTLPLogExporter()))
    set_logger_provider(registros)
    logging.getLogger().addHandler(LoggingHandler(level=logging.INFO, logger_provider=registros))

    # Metricas HTTP con los nombres estables (http.server.request.duration, en segundos): los mismos
    # que emite nexo-api, para que los tableros usen una sola consulta para los dos servicios.
    os.environ.setdefault("OTEL_SEMCONV_STABILITY_OPT_IN", "http")
    # skip_dep_check: el instrumentador declara soporte hasta SQLAlchemy 2.0 y se niega con la 2.1
    # instalada, pero solo usa los eventos del motor, que no cambiaron (verificado: salen los tramos SQL).
    SQLAlchemyInstrumentor().instrument(engine=engine.sync_engine, skip_dep_check=True)
    HTTPXClientInstrumentor().instrument()
    if app is not None:
        from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor

        # La sonda de salud llega cada pocos segundos: trazarla tapa las peticiones reales.
        # Sin los tramos internos "http receive"/"http send" de ASGI: tres por peticion que no dicen nada.
        FastAPIInstrumentor.instrument_app(app, excluded_urls="health", exclude_spans=["receive", "send"])


def sin_sondeos() -> Any:
    """Muestreador que descarta las trazas que empiezan en una llamada saliente (SQL o
    HTTP sin peticion ni mensaje que la origine): son las consultas periodicas del
    vigilante, que en Jaeger tapan las cubicaciones reales."""
    from opentelemetry.sdk.trace.sampling import Decision, ParentBased, Sampler, SamplingResult

    class _SinSondeos(Sampler):
        def should_sample(self, parent_context, trace_id, name, kind=None, attributes=None, links=None, trace_state=None):
            return SamplingResult(Decision.DROP if kind == SpanKind.CLIENT else Decision.RECORD_AND_SAMPLE, attributes)

        def get_description(self) -> str:
            return "SinSondeos"

    return ParentBased(root=_SinSondeos())


def cabeceras_de_traza() -> dict[str, str]:
    """El contexto de la traza en curso, listo para viajar con un mensaje.
    Vacio si no hay traza (o si la observabilidad esta apagada)."""
    cabeceras: dict[str, str] = {}
    propagate.inject(cabeceras)
    if "traceparent" in cabeceras:
        cabeceras[_CABECERA_MASSTRANSIT] = cabeceras["traceparent"]
    return cabeceras


def _traza_de_origen(mensaje: Any) -> dict[str, str]:
    """La traza de quien publico el mensaje, leida de sus cabeceras AMQP."""
    cabeceras = getattr(mensaje, "headers", None) or {}
    origen = cabeceras.get("traceparent") or cabeceras.get(_CABECERA_MASSTRANSIT)
    if isinstance(origen, bytes):
        origen = origen.decode()
    return {"traceparent": origen} if isinstance(origen, str) else {}


@contextmanager
def tramo_de_consumo(mensaje: Any, cola: str) -> Iterator[dict[str, Any]]:
    """Tramo que cubre el procesamiento de UN mensaje, colgado de la traza de
    quien lo publico. Entrega un diccionario que el worker va llenando (`tipo`,
    `resultado`, `plan_id`, ...): al salir se vuelve etiquetas del tramo y la
    metrica de mensajes procesados."""
    datos: dict[str, Any] = {"tipo": "desconocido", "resultado": "error"}
    inicio = time.perf_counter()
    with tracer.start_as_current_span(
        f"procesar {cola}", context=propagate.extract(_traza_de_origen(mensaje)), kind=SpanKind.CONSUMER,
        attributes={"messaging.system": "rabbitmq", "messaging.destination.name": cola},
    ) as tramo:
        try:
            yield datos
        finally:
            tramo.update_name(f"procesar {datos['tipo']}")
            for clave, valor in datos.items():
                if valor is not None:
                    tramo.set_attribute(f"cubicador.{clave}", valor)
            if datos["resultado"] in _RESULTADOS_DE_FALLO:
                tramo.set_status(Status(StatusCode.ERROR, datos["resultado"]))
            etiquetas = {"tipo": datos["tipo"], "resultado": datos["resultado"]}
            _mensajes.add(1, etiquetas)
            _duracion.record(time.perf_counter() - inicio, etiquetas)


@contextmanager
def tramo_de_publicacion(routing_key: str) -> Iterator[dict[str, str]]:
    """Tramo de la publicacion de un mensaje. Entrega las cabeceras de traza
    que deben viajar con el."""
    with tracer.start_as_current_span(
        f"publicar {routing_key}", kind=SpanKind.PRODUCER,
        attributes={"messaging.system": "rabbitmq", "messaging.destination.name": routing_key},
    ):
        yield cabeceras_de_traza()
