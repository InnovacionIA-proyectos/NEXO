"""La interpretacion valida contra el catalogo y rechaza con motivo (#368).

Los dos primeros criterios de la issue se cierran aca, sin red y sin llave: el
cliente es falso y lo que se prueba es lo que hacemos NOSOTROS con lo que el
modelo responda. Que tan bien responde el modelo es el tercer criterio, y eso
se mide con el corredor de evaluacion, no aqui.

El caso que mas importa es que no se complete un dato ausente: el peso es lo
que cuesta el flete, y una cotizacion con un peso estimado parece correcta y no
lo es.
"""

from __future__ import annotations

import json
from decimal import Decimal as D

import pytest

from application.interpretacion.lectura import RespuestaInvalida, leer_interpretacion
from application.interpretacion.prompt import construir_mensaje, esquema_salida
from application.interpretacion.servicio import (
    IaNoDisponible, ServicioInterpretacion, TextoVacio,
)
from tests.ia_sombra.ayudas import ClienteIaFalso, catalogo

CATALOGO = catalogo()  # solo trae BEBIDAS y FERTILIZANTES


def _posicion(**cambios) -> dict:
    base = {"codigo": "BEBIDAS", "cantidad": 50, "largoCm": 50, "anchoCm": 40,
            "altoCm": 40, "pesoKg": 20, "textoOriginal": "50 cajas de gaseosa"}
    base.update(cambios)
    return base


def _json(posiciones=None, rechazos=None) -> str:
    return json.dumps({"posiciones": posiciones if posiciones is not None else [_posicion()],
                       "rechazos": rechazos or []})


def _leer(posiciones=None, rechazos=None):
    return leer_interpretacion(_json(posiciones, rechazos), CATALOGO)


def _servicio(respuesta=None, **kw):
    return ServicioInterpretacion(ClienteIaFalso(respuesta, **kw), CATALOGO)


# --------------------------------------------------- lo que sale bien

async def test_un_texto_completo_produce_la_posicion_con_sus_cinco_datos():
    r = await _servicio(_json()).interpretar("50 cajas de gaseosa de 50x40x40 cm, 20 kilos")

    (p,) = r.interpretacion.posiciones
    assert (p.codigo_mercancia, p.cantidad) == ("BEBIDAS", 50)
    assert (p.largo_cm, p.ancho_cm, p.alto_cm, p.peso_kg) == (D(50), D(40), D(40), D(20))
    assert r.interpretacion.completa


async def test_la_medicion_viaja_para_que_el_corredor_pueda_topar_el_gasto():
    r = await _servicio(_json(), tokens=(1234, 56)).interpretar("50 cajas")

    assert (r.medicion.tokens_entrada, r.medicion.tokens_salida) == (1234, 56)
    assert r.medicion.modelo == "modelo-falso"


def test_los_decimales_no_se_corrompen():
    """Por `Decimal(str(x))` y no por `Decimal(float)`.

    `Decimal(0.1)` da 0.1000000000000000055511151231257827, y esa medida
    terminaria en el motor y en la cotizacion.
    """
    (p,) = _leer([_posicion(largoCm=0.1, pesoKg=2.5)]).posiciones

    assert p.largo_cm == D("0.1")
    assert p.peso_kg == D("2.5")


def test_el_codigo_se_normaliza_contra_el_catalogo():
    """Llega como lo escriba el modelo y sale como lo escribe el catalogo."""
    (p,) = _leer([_posicion(codigo=" bebidas ")]).posiciones

    assert p.codigo_mercancia == "BEBIDAS"


# ------------------------------------- lo que falta NO se completa (AC2)

@pytest.mark.parametrize("campo,motivo", [
    ("pesoKg", "PESO_FALTANTE"),
    ("largoCm", "DIMENSION_FALTANTE"),
    ("anchoCm", "DIMENSION_FALTANTE"),
    ("altoCm", "DIMENSION_FALTANTE"),
    ("cantidad", "CANTIDAD_FALTANTE"),
])
async def test_un_dato_ausente_se_rechaza_con_su_motivo_y_no_se_estima(campo, motivo):
    r = await _servicio(_json([_posicion(**{campo: None})])).interpretar("algo sin " + campo)

    assert r.interpretacion.posiciones == (), "no se devolvio una posicion a medio llenar"
    assert [x.motivo for x in r.interpretacion.rechazos] == [motivo]
    assert not r.interpretacion.completa


@pytest.mark.parametrize("valor", [0, -5, "dos", True, None])
def test_una_cantidad_que_no_es_un_entero_de_al_menos_uno_se_rechaza(valor):
    r = _leer([_posicion(cantidad=valor)])

    assert r.posiciones == ()
    assert r.rechazos[0].motivo in ("CANTIDAD_INVALIDA", "CANTIDAD_FALTANTE")


@pytest.mark.parametrize("valor", [0, -1, "x", float("inf"), float("nan")])
def test_una_medida_que_no_es_un_numero_positivo_se_rechaza(valor):
    r = _leer([_posicion(largoCm=valor)])

    assert r.posiciones == ()
    assert r.rechazos[0].motivo == "DIMENSION_INVALIDA"


def test_un_codigo_fuera_del_catalogo_no_se_fuerza_al_mas_parecido():
    r = _leer([_posicion(codigo="MATERIAL_RADIACTIVO")])

    assert r.posiciones == ()
    assert r.rechazos[0].motivo == "CODIGO_DESCONOCIDO"


def test_un_tipo_no_cubicable_es_el_mismo_caso_que_uno_inexistente():
    """Lo dice el docstring de `TipoMercancia`: para el motor son lo mismo."""
    from dataclasses import replace
    sin_cubicar = replace(CATALOGO, mercancias=tuple(
        replace(m, cubicable=False) if m.codigo == "BEBIDAS" else m for m in CATALOGO.mercancias))

    r = leer_interpretacion(_json(), sin_cubicar)

    assert r.posiciones == ()
    assert r.rechazos[0].motivo == "CODIGO_DESCONOCIDO"


# ------------------------------------------------ la parcial no se oculta

async def test_una_posicion_mala_no_tumba_a_las_buenas_pero_queda_a_la_vista():
    """La decision de #368: interpretacion PARCIAL.

    Y el resguardo: `completa` es False, porque la forma de fallo de #304 es
    justamente una posicion que se pierde sin que nadie lo note y un peso total
    que sale corto.
    """
    r = await _servicio(_json([
        _posicion(),
        _posicion(codigo="FERTILIZANTES", cantidad=100, pesoKg=None, textoOriginal="100 bultos de abono"),
        _posicion(cantidad=5, largoCm=30, anchoCm=30, altoCm=30, pesoKg=2),
    ])).interpretar("tres cosas")

    assert len(r.interpretacion.posiciones) == 2, "las dos completas sobrevivieron"
    assert [(x.motivo, x.texto_original) for x in r.interpretacion.rechazos] == [
        ("PESO_FALTANTE", "100 bultos de abono")]
    assert not r.interpretacion.completa, "con un rechazo pendiente no esta completa"


def test_sin_posiciones_ni_rechazos_se_marca_sin_posiciones():
    """Un texto que no describe ninguna carga no puede volver como exito vacio."""
    r = _leer([], [])

    assert r.posiciones == ()
    assert [x.motivo for x in r.rechazos] == ["SIN_POSICIONES"]
    assert not r.completa


def test_el_rechazo_que_viene_del_modelo_se_conserva():
    r = _leer([], [{"textoOriginal": "unas cajas", "motivo": "CANTIDAD_FALTANTE"}])

    assert [(x.motivo, x.texto_original) for x in r.rechazos] == [("CANTIDAD_FALTANTE", "unas cajas")]


# --------------------------------------- lo que el modelo no puede romper

def test_una_respuesta_que_no_es_json_se_rechaza_entera():
    with pytest.raises(RespuestaInvalida):
        leer_interpretacion("no soy json", CATALOGO)


def test_un_motivo_que_no_es_de_la_lista_se_rechaza_entero():
    """Inventar un motivo es romper el contrato, no un rechazo de negocio."""
    with pytest.raises(RespuestaInvalida):
        leer_interpretacion(_json([], [{"textoOriginal": "x", "motivo": "PORQUE_SI"}]), CATALOGO)


def test_mas_posiciones_de_las_razonables_se_rechazan_enteras():
    with pytest.raises(RespuestaInvalida):
        leer_interpretacion(_json([_posicion()] * 51), CATALOGO)


# ----------------------------------------------- lo que sale hacia afuera

async def test_el_mensaje_lleva_el_catalogo_y_el_texto_y_nada_mas():
    """Nivel C del ADR. Si algun dia se le pasa la solicitud entera en vez del
    texto, esta prueba es la que lo nota."""
    servicio = _servicio(_json())

    await servicio.interpretar("50 cajas de gaseosa")

    (_, mensaje, _) = servicio._cliente.llamadas[0]  # noqa: SLF001
    enviado = json.loads(mensaje)
    assert set(enviado) == {"catalogoMercancias", "descripcion"}
    assert enviado["descripcion"] == "50 cajas de gaseosa"
    assert {m["codigo"] for m in enviado["catalogoMercancias"]} == {"BEBIDAS", "FERTILIZANTES"}


def test_el_esquema_cierra_los_codigos_al_catalogo():
    propiedades = esquema_salida(CATALOGO)["properties"]["posiciones"]["items"]["properties"]

    assert propiedades["codigo"]["enum"] == ["BEBIDAS", "FERTILIZANTES"]


def test_el_texto_larguisimo_se_recorta_antes_de_salir():
    enviado = json.loads(construir_mensaje(CATALOGO, "x" * 9000))

    assert len(enviado["descripcion"]) == 4000


async def test_un_texto_vacio_no_gasta_una_llamada():
    servicio = _servicio(_json())

    with pytest.raises(TextoVacio):
        await servicio.interpretar("   ")

    assert servicio._cliente.llamadas == []  # noqa: SLF001


async def test_si_el_proveedor_falla_la_excepcion_sube():
    """No se envuelve en una interpretacion vacia: "el proveedor fallo" y "el
    texto no traia peso" son cosas distintas y se atienden distinto."""
    servicio = _servicio(IaNoDisponible("IA_LIMITE_PROVEEDOR", "limite"))

    with pytest.raises(IaNoDisponible):
        await servicio.interpretar("50 cajas de gaseosa")
