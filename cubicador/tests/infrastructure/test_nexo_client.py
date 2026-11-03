"""El cliente que lee la linea de NEXO (`GET /api/lineas-solicitud/{id}/cubicacion`).

La respuesta de ese endpoint es la de `clsLineaParaCubicacionDto` serializada en
camelCase por NEXO, y el tipo de mercancia de cada posicion viaja como
`codigoTipoMercancia`. Estos tests usan ese mismo cuerpo, tal cual lo emite NEXO,
para que un cambio de nombre en cualquiera de los dos lados rompa aca y no en
produccion con un `MERCANCIA_NO_DECLARADA` enganoso.
"""

from __future__ import annotations

import httpx
import pytest

from infrastructure.clientes import nexo_client
from infrastructure.clientes.nexo_client import NexoLineaSolicitudClient

LINEA_ID = "11111111-1111-1111-1111-111111111111"

# Cuerpo con la forma exacta que devuelve NEXO (nombres camelCase del DTO).
CUERPO_NEXO = {
    "lineaId": LINEA_ID,
    "posiciones": [
        {
            "descripcion": "Pallet de bebidas", "cantidad": 3, "largoCm": 120.0, "anchoCm": 100.0,
            "altoCm": 100.0, "pesoKg": 500.0, "codigoTipoMercancia": "BEBIDAS",
        },
        {
            "descripcion": "Bobina", "cantidad": 1, "largoCm": 150.0, "anchoCm": 150.0,
            "altoCm": 120.0, "pesoKg": 2000.0, "codigoTipoMercancia": None,
        },
    ],
    "origen": "Bogota",
    "destino": "Cali",
}


def _instalar_nexo_falso(monkeypatch, responder) -> list[httpx.Request]:
    """Reemplaza el `httpx.AsyncClient` del modulo por uno que atiende `responder`."""
    recibidas: list[httpx.Request] = []
    cliente_real = httpx.AsyncClient

    def atender(request: httpx.Request) -> httpx.Response:
        recibidas.append(request)
        return responder(request)

    def fabrica(*args, **kwargs):
        return cliente_real(*args, transport=httpx.MockTransport(atender), **kwargs)

    monkeypatch.setattr(nexo_client.httpx, "AsyncClient", fabrica)
    return recibidas


async def test_el_tipo_de_mercancia_se_lee_de_codigoTipoMercancia(monkeypatch):
    _instalar_nexo_falso(monkeypatch, lambda _: httpx.Response(200, json=CUERPO_NEXO))

    linea = await NexoLineaSolicitudClient("http://nexo", "clave").obtener(LINEA_ID)

    assert linea is not None
    assert [p.codigo_mercancia for p in linea.posiciones] == ["BEBIDAS", None]


async def test_el_resto_de_la_posicion_y_la_ruta_se_leen_igual(monkeypatch):
    _instalar_nexo_falso(monkeypatch, lambda _: httpx.Response(200, json=CUERPO_NEXO))

    linea = await NexoLineaSolicitudClient("http://nexo", "clave").obtener(LINEA_ID)

    pallet = linea.posiciones[0]
    assert (pallet.descripcion, pallet.cantidad) == ("Pallet de bebidas", 3)
    assert (pallet.largo_cm, pallet.ancho_cm, pallet.alto_cm, pallet.peso_kg) == (120.0, 100.0, 100.0, 500.0)
    assert (linea.linea_id, linea.origen, linea.destino) == (LINEA_ID, "Bogota", "Cali")


async def test_llama_al_endpoint_de_la_linea_con_la_clave_de_servicio(monkeypatch):
    recibidas = _instalar_nexo_falso(monkeypatch, lambda _: httpx.Response(200, json=CUERPO_NEXO))

    await NexoLineaSolicitudClient("http://nexo/", "clave-de-servicio").obtener(LINEA_ID)

    (peticion,) = recibidas
    assert peticion.method == "GET"
    assert str(peticion.url) == f"http://nexo/api/lineas-solicitud/{LINEA_ID}/cubicacion"
    assert peticion.headers["Authorization"] == "Bearer clave-de-servicio"


async def test_una_linea_sin_posiciones_devuelve_lista_vacia(monkeypatch):
    _instalar_nexo_falso(
        monkeypatch, lambda _: httpx.Response(200, json={"lineaId": LINEA_ID, "posiciones": []}))

    linea = await NexoLineaSolicitudClient("http://nexo", "clave").obtener(LINEA_ID)

    assert linea.posiciones == []
    assert linea.origen is None and linea.destino is None


async def test_una_linea_inexistente_devuelve_none(monkeypatch):
    _instalar_nexo_falso(monkeypatch, lambda _: httpx.Response(404))

    assert await NexoLineaSolicitudClient("http://nexo", "clave").obtener(LINEA_ID) is None


async def test_un_error_de_nexo_se_propaga(monkeypatch):
    _instalar_nexo_falso(monkeypatch, lambda _: httpx.Response(401))

    with pytest.raises(httpx.HTTPStatusError):
        await NexoLineaSolicitudClient("http://nexo", "clave").obtener(LINEA_ID)
