"""La traza tiene que sobrevivir el paso por RabbitMQ: lo que el publicador
escribe debe ser lo que el worker lee, venga con la cabecera W3C (asi publica
el Cubicador) o con la de MassTransit (asi publica NEXO)."""

import pytest
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider

from infrastructure import observabilidad


class _Mensaje:
    def __init__(self, headers: dict, body: bytes) -> None:
        self.headers = headers
        self.body = body


@pytest.mark.sin_base
def test_el_worker_continua_la_traza_de_quien_publico():
    trace.set_tracer_provider(TracerProvider())

    with observabilidad.tramo_de_publicacion("cubicacion.solicitada") as cabeceras:
        traza_origen = trace.get_current_span().get_span_context().trace_id
    assert cabeceras["traceparent"] == cabeceras["MT-Activity-Id"]

    mensajes = [
        _Mensaje({"traceparent": cabeceras["traceparent"]}, b"{}"),
        _Mensaje({"MT-Activity-Id": cabeceras["traceparent"].encode()}, b"{}"),
    ]
    for mensaje in mensajes:
        with observabilidad.tramo_de_consumo(mensaje, "cola") as datos:
            datos["resultado"] = "ok"
            assert trace.get_current_span().get_span_context().trace_id == traza_origen

    # Sin traza de origen el mensaje igual se procesa, en una traza nueva.
    with observabilidad.tramo_de_consumo(_Mensaje({}, b"{}"), "cola"):
        assert trace.get_current_span().get_span_context().trace_id != traza_origen
