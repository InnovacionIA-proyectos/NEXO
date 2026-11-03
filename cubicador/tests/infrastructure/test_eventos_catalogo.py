"""Forma de los eventos de catalogo (S5-HU-17.4).

NEXO proyecta mercancias y carrocerias con un `IConsumer<T>` real de
MassTransit, igual que ya proyecta vehiculos. Ese consumidor no deserializa un
JSON cualquiera: necesita el "sobre" con `messageType` apuntando al namespace
exacto de la clase C#. Si el sobre o la routing key se escriben mal, el mensaje
sale, nadie lo consume, y la proyeccion de NEXO se queda vieja **sin que nada
falle** - que es exactamente el modo de fallo que estos eventos existen para
evitar.

No hay broker en estas pruebas: se intercepta `_publicar` y se mira lo que se
habria mandado.
"""

from __future__ import annotations

import pytest

from infrastructure.mensajeria.rabbitmq import (
    _NAMESPACE_MENSAJES_NEXO,
    RabbitMqEventosPublisher,
)

# El namespace C# del otro lado. Se fija aqui ademas de leerlo del modulo
# porque cambiarlo rompe la deserializacion en NEXO y nada de este lado se
# entera: el mensaje sale igual y nadie lo consume.
NAMESPACE = "Nexo.Mensajes.Cubicacion"


@pytest.fixture
def publicados() -> list[tuple[str, dict]]:
    return []


@pytest.fixture
def publisher(publicados, monkeypatch) -> RabbitMqEventosPublisher:
    p = RabbitMqEventosPublisher("amqp://sin-broker/")

    async def _capturar(routing_key, payload):
        publicados.append((routing_key, payload))

    monkeypatch.setattr(p, "_publicar", _capturar)
    return p


def _tipo(payload: dict) -> str:
    return payload["messageType"][0]


@pytest.mark.asyncio
async def test_los_seis_eventos_de_catalogo_usan_su_routing_key(publisher, publicados):
    await publisher.publicar_mercancia_creada("m-1", {"codigo": "BEBIDAS"})
    await publisher.publicar_mercancia_actualizada("m-1", {"codigo": "BEBIDAS"})
    await publisher.publicar_mercancia_eliminada("m-1")
    await publisher.publicar_carroceria_creada("c-1", {"codigo": "FURGON"})
    await publisher.publicar_carroceria_actualizada("c-1", {"codigo": "FURGON"})
    await publisher.publicar_carroceria_eliminada("c-1")

    assert [k for k, _ in publicados] == [
        "mercancia.creada", "mercancia.actualizada", "mercancia.eliminada",
        "carroceria.creada", "carroceria.actualizada", "carroceria.eliminada",
    ]


@pytest.mark.asyncio
async def test_van_envueltos_en_el_sobre_de_masstransit(publisher, publicados):
    assert _NAMESPACE_MENSAJES_NEXO == NAMESPACE, "cambio el namespace C# del otro lado"
    await publisher.publicar_mercancia_creada("m-1", {"codigo": "BEBIDAS"})
    _, payload = publicados[0]
    assert _tipo(payload) == f"urn:message:{NAMESPACE}:MercanciaCreada"
    assert payload["message"]["correlationId"] == "m-1"
    assert payload["message"]["mercancia"] == {"codigo": "BEBIDAS"}


@pytest.mark.asyncio
async def test_el_nombre_csharp_corresponde_al_evento(publisher, publicados):
    """El `messageType` tiene que coincidir EXACTO con el nombre de la clase
    del otro lado: es un acoplamiento cruzado sin verificacion en tiempo de
    compilacion en ninguno de los dos lados (ver DECISIONES_TECNICAS #12)."""
    await publisher.publicar_carroceria_actualizada("c-1", {"codigo": "FURGON"})
    await publisher.publicar_mercancia_eliminada("m-9")

    assert _tipo(publicados[0][1]).endswith(":CarroceriaActualizada")
    assert _tipo(publicados[1][1]).endswith(":MercanciaEliminada")


@pytest.mark.asyncio
async def test_el_evento_de_borrado_solo_lleva_el_id(publisher, publicados):
    """No hay fila que proyectar, solo una que quitar - mismo criterio que
    `vehiculo.eliminado`."""
    await publisher.publicar_mercancia_eliminada("m-9")
    await publisher.publicar_carroceria_eliminada("c-9")

    assert publicados[0][1]["message"] == {"correlationId": "m-9", "mercanciaId": "m-9"}
    assert publicados[1][1]["message"] == {"correlationId": "c-9", "carroceriaId": "c-9"}
