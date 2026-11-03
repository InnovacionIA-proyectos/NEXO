"""El modo sombra de punta a punta contra `POST /recomendaciones`.

Lo que se prueba es lo que pide el criterio de aceptacion: la respuesta al usuario
no cambia ni se retrasa, las diferencias con el motor quedan registradas, el
interruptor apaga la funcion y un fallo del modelo no se nota desde afuera.
Corre contra la app real por ASGI y SQLite en memoria; el modelo es un doble.
"""

from __future__ import annotations

import time

import pytest
from sqlalchemy import select

from api.dependencias import obtener_servicio_sombra
from api.main import app
from application.ia_sombra.servicio import ServicioSombra
from application.ports.ia import IaNoDisponible
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.modelos import IaSombraModel
from infrastructure.persistence.repositorios.catalogo_repo import SqlCatalogoRepository
from infrastructure.persistence.repositorios.ia_sombra_repo import SqlIaSombraRepository
from infrastructure.persistence.repositorios.parametros_repo import SqlParametrosCubicacionRepository
from tests.api.test_contrato_recomendaciones import COTIFLY, _cuerpo, client  # noqa: F401  (el fixture se reusa)
from tests.ia_sombra.ayudas import ClienteIaFalso, config, json_ia, vehiculo_ia


class _Escenario:
    """El servicio de sombra con un modelo doble y la configuracion que cada prueba quiera."""

    def __init__(self, cliente: ClienteIaFalso, **cfg) -> None:
        self.cliente = cliente
        self.cfg = config(**cfg)
        self.servicio = ServicioSombra(
            SessionLocal,
            lambda s: (SqlCatalogoRepository(s), SqlParametrosCubicacionRepository(s), SqlIaSombraRepository(s)),
            lambda _: cliente, config=lambda: self.cfg)


@pytest.fixture
def escenario(client):  # noqa: F811
    creados: list[_Escenario] = []

    def crear(cliente: ClienteIaFalso, **cfg) -> _Escenario:
        e = _Escenario(cliente, **cfg)
        creados.append(e)
        app.dependency_overrides[obtener_servicio_sombra] = lambda: e.servicio
        return e

    yield crear
    app.dependency_overrides.pop(obtener_servicio_sombra, None)


async def _filas():
    async with SessionLocal() as s:
        return list((await s.execute(select(IaSombraModel))).scalars())


async def _borrar_filas():
    from sqlalchemy import delete
    async with SessionLocal() as s:
        await s.execute(delete(IaSombraModel))
        await s.commit()


def _sin_ids(datos: dict) -> dict:
    """La respuesta sin los ids ni las fechas, que cambian entre corridas."""
    limpio = {k: v for k, v in datos.items() if k not in ("idRecomendacion",)}
    limpio["vehiculos"] = [{k: v for k, v in vh.items() if k != "idRecomendacionVehiculo"} for vh in datos["vehiculos"]]
    return limpio


async def test_la_respuesta_es_identica_con_el_modo_sombra_encendido_y_apagado(client, escenario):  # noqa: F811
    await _borrar_filas()
    apagado = escenario(ClienteIaFalso(), habilitada=False)
    sin_sombra = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    assert apagado.cliente.llamadas == []

    encendido = escenario(ClienteIaFalso(json_ia("SIN_SELECCION_AUTOMATICA", motivo="OTRO")))
    con_sombra = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    await encendido.servicio.esperar_pendientes(10)

    assert len(encendido.cliente.llamadas) == 1, "la comparacion si corrio"
    assert _sin_ids(con_sombra) == _sin_ids(sin_sombra), "y aun asi la respuesta no cambio ni en un campo"


async def test_la_respuesta_no_espera_al_modelo(client, escenario):  # noqa: F811
    e = escenario(ClienteIaFalso(json_ia("SIN_SELECCION_AUTOMATICA", motivo="OTRO"), demora=3.0))

    inicio = time.perf_counter()
    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())
    espera = time.perf_counter() - inicio

    assert r.status_code == 200
    assert espera < 1.5, f"la respuesta tardo {espera:.2f} s con un modelo que tarda 3 s"
    assert e.servicio.pendientes == 1, "la comparacion sigue en curso, sin bloquear a nadie"
    await e.servicio.cerrar(timeout=0.01)


async def test_la_diferencia_con_el_motor_queda_registrada_junto_a_la_recomendacion(client, escenario):  # noqa: F811
    await _borrar_filas()
    e = escenario(ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia("C2", "FURGON", ((0, 100),))], explicacion="un Sencillo alcanza")))

    r = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    await e.servicio.esperar_pendientes(10)

    (fila,) = await _filas()
    assert fila.id_recomendacion == r["idRecomendacion"]
    assert (fila.origen_sistema, fila.estado, fila.resultado_motor, fila.resultado_ia) == ("COTIFLY", "OK", "ASIGNADO", "ASIGNADO")
    assert fila.cantidad_motor == len(r["vehiculos"])
    assert fila.explicacion == "un Sencillo alcanza"


async def test_si_el_modelo_falla_la_respuesta_es_la_misma_y_queda_el_intento(client, escenario):  # noqa: F811
    await _borrar_filas()
    e = escenario(ClienteIaFalso(IaNoDisponible("IA_LIMITE_PROVEEDOR", "limite")))

    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())
    await e.servicio.esperar_pendientes(10)

    assert r.status_code == 200 and r.json()["resultado"] == "ASIGNADO"
    (fila,) = await _filas()
    assert (fila.estado, fila.coincide_flota) == ("IA_LIMITE_PROVEEDOR", None)


async def test_el_interruptor_apaga_todo_sin_tocar_el_modelo_ni_la_base(client, escenario):  # noqa: F811
    await _borrar_filas()
    e = escenario(ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()])), habilitada=False)

    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())

    assert r.status_code == 200
    assert e.cliente.llamadas == [] and await _filas() == []


async def test_las_peticiones_invalidas_no_disparan_ninguna_comparacion(client, escenario):  # noqa: F811
    e = escenario(ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()])))

    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": "NO_EXISTE", "cantidad": 1, "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))

    assert r.status_code == 400
    assert e.servicio.pendientes == 0 and e.cliente.llamadas == []


async def test_el_resultado_sin_seleccion_tambien_se_compara(client, escenario):  # noqa: F811
    await _borrar_filas()
    e = escenario(ClienteIaFalso(json_ia("SIN_SELECCION_AUTOMATICA", motivo="EXTRADIMENSIONADO")))

    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": "BEBIDAS", "cantidad": 2, "largoCm": 1400, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))
    await e.servicio.esperar_pendientes(10)

    assert r.json()["resultado"] == "SIN_SELECCION_AUTOMATICA"
    (fila,) = await _filas()
    assert (fila.resultado_motor, fila.motivo_motor, fila.resultado_ia, fila.coincide_resultado) == (
        "SIN_SELECCION_AUTOMATICA", "EXTRADIMENSIONADO", "SIN_SELECCION_AUTOMATICA", True)
