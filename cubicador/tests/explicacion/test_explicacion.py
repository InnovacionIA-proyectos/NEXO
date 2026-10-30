"""La explicacion no inventa cifras, y si falla no rompe nada (#369).

Los dos criterios de la issue se prueban aca, con cliente falso y sin llave.

El primero -"los valores del texto coinciden con los del motor"- es el que
obliga a algo mas que confiar: se comprueba que una explicacion con una cifra
que el motor no calculo se descarte entera.
"""

from __future__ import annotations

import json
from decimal import Decimal as D

import pytest

from application.explicacion.prompt import cifras_permitidas, construir_mensaje
from application.explicacion.servicio import ServicioExplicacion
from application.explicacion.verificacion import verificar
from tests.ia_sombra.ayudas import ClienteIaFalso, IaNoDisponible, recomendacion


def _con_numeros(**cambios):
    """Una recomendacion de un camion con numeros concretos que mirar."""
    from dataclasses import replace
    base = recomendacion(("C2", "FURGON"))
    v = replace(base.vehiculos[0], nombre_vehiculo="Sencillo", peso_total_kg=D("4100"),
                volumen_total_m3=D("28.5"), porcentaje_llenado_volumen=D("87.47"),
                porcentaje_llenado_peso=D("48.2"), **cambios)
    return replace(base, vehiculos=(v,))


def _json(texto: str) -> str:
    return json.dumps({"explicacion": texto})


def _servicio(respuesta, **kw):
    return ServicioExplicacion(ClienteIaFalso(respuesta, **kw))


# ------------------------------------------- las cifras tienen que ser del motor

async def test_una_explicacion_fiel_se_devuelve():
    r = _con_numeros()
    texto = "Un Sencillo alcanza: la carga pesa 4100 kg y ocupa 28.5 m3, con el furgon al 87 % de su volumen."

    e = await _servicio(_json(texto)).explicar(r)

    assert e is not None and e.texto == texto


async def test_una_cifra_que_el_motor_no_calculo_descarta_la_explicacion_entera():
    """El fallo que esta prueba persigue: '93 %' se lee perfecto y es falso.

    Se descarta todo el texto, no la frase: una cifra inventada vuelve
    sospechoso el resto, y no mostrar nada no rompe la pantalla.
    """
    r = _con_numeros()

    e = await _servicio(_json("La carga pesa 4100 kg y el furgon queda al 93 % de su volumen.")).explicar(r)

    assert e is None


async def test_redondear_una_cifra_que_si_existe_es_fiel():
    """87,47 escrito como 87 o como 87,5 sigue siendo el numero del motor."""
    r = _con_numeros()

    for escrito in ("87", "87.5", "87,5", "87.47"):
        e = await _servicio(_json(f"El furgon va al {escrito} % de su volumen.")).explicar(r)
        assert e is not None, escrito


def test_el_formato_de_miles_no_se_confunde_con_decimales():
    permitidas = [D("4100"), D("28.5")]

    assert verificar("pesa 4.100 kg y ocupa 28,5 m3", permitidas).fiel
    assert verificar("pesa 4,100 kg", permitidas).fiel


def test_un_codigo_de_vehiculo_no_se_lee_como_una_cifra():
    """`2S2` y `3S3` son codigos del catalogo. Partirlos en digitos sueltos
    haria fallar explicaciones correctas."""
    v = verificar("Se eligio un 2S2 con carroceria FURGON.", [D(1)])

    assert v.fiel, v.cifras_inventadas


def test_las_cifras_permitidas_salen_de_lo_que_se_le_mando():
    """La contraparte: lo que entra al mensaje es lo que puede salir en el texto."""
    r = _con_numeros()
    enviado = json.loads(construir_mensaje(r))
    permitidas = cifras_permitidas(r)

    v = enviado["vehiculos"][0]
    for campo in ("pesoTotalKg", "volumenTotalM3", "llenadoVolumenPorcentaje", "llenadoPesoPorcentaje"):
        assert any(D(str(v[campo])) == p for p in permitidas), campo


async def test_los_ordinales_de_la_redaccion_son_validos():
    """"el segundo camion", "los 2 vehiculos": cifras normales que no salen de
    ningun campo."""
    from dataclasses import replace
    base = _con_numeros()
    r = replace(base, vehiculos=(base.vehiculos[0], replace(base.vehiculos[0], numero=2)))

    e = await _servicio(_json("Hicieron falta 2 vehiculos; el 1 va lleno y el 2 completa el resto.")).explicar(r)

    assert e is not None


# ------------------------------------------- si falla, la pantalla sigue (AC2)

async def test_si_el_proveedor_falla_no_hay_explicacion_pero_tampoco_excepcion():
    e = await _servicio(IaNoDisponible("IA_LIMITE_PROVEEDOR", "limite")).explicar(_con_numeros())

    assert e is None


async def test_un_fallo_inesperado_tampoco_sube():
    e = await _servicio(RuntimeError("algo raro")).explicar(_con_numeros())

    assert e is None


@pytest.mark.parametrize("crudo", ["no soy json", "[]", '{"otra": "cosa"}', '{"explicacion": ""}', '{"explicacion": 7}'])
async def test_una_respuesta_que_no_trae_texto_no_es_explicacion(crudo):
    assert await _servicio(crudo).explicar(_con_numeros()) is None


async def test_sin_flota_no_se_gasta_una_llamada():
    """El motivo ya viaja en el contrato: es un dato, no una redaccion."""
    from dataclasses import replace
    sin_flota = replace(_con_numeros(), vehiculos=(), resultado="SIN_SELECCION_AUTOMATICA")
    servicio = _servicio(_json("lo que sea"))

    assert await servicio.explicar(sin_flota) is None
    assert servicio._cliente.llamadas == []  # noqa: SLF001


async def test_el_texto_larguisimo_se_recorta():
    e = await _servicio(_json("4100 " * 500)).explicar(_con_numeros())

    assert e is not None and len(e.texto) <= 600


# ------------------------------------------------ lo que nunca sale al modelo

def test_el_mensaje_no_lleva_nada_comercial_ni_de_quien_pide():
    """Tarifas, valores y datos del cliente son nivel A del ADR: no salen ni
    enmascarados. Si algun dia se agrega un campo de esos, esta prueba lo nota."""
    enviado = json.loads(construir_mensaje(_con_numeros()))

    assert set(enviado) == {
        "resultado", "motivoSinSeleccion", "totalVehiculos", "vehiculos", "advertencias", "condicionada"}
    assert set(enviado["vehiculos"][0]) == {
        "numero", "vehiculo", "codigoVehiculo", "carroceria", "pesoTotalKg", "volumenTotalM3",
        "llenadoVolumenPorcentaje", "llenadoPesoPorcentaje", "alertaBajoLlenado", "mercancias"}
    crudo = json.dumps(enviado).lower()
    for prohibido in ("tarifa", "precio", "valor", "costo", "nit", "cliente", "flete"):
        assert prohibido not in crudo, prohibido


@pytest.mark.parametrize("escrito", ["62", "63"])
def test_la_mitad_exacta_vale_redondeada_hacia_arriba_o_hacia_abajo(escrito):
    """62,5 lo escribe la gente como 63 y Python lo redondea a 62.

    Con solo la convencion de Python se rechazaban explicaciones correctas:
    medido contra el modelo, 1 de cada 8, y el unico fallo de esa tanda era
    justamente un `62,5` escrito como `63`. Aceptar las dos no abre la puerta a
    nada: una cifra inventada sigue teniendo que caer a medio punto de un valor
    real del motor.
    """
    assert verificar(f"queda al {escrito} % de su volumen", [D("62.5")]).fiel


def test_una_cifra_lejana_sigue_sin_pasar():
    assert not verificar("queda al 70 % de su volumen", [D("62.5")]).fiel
