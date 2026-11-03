"""La comparacion entre la flota del motor y la del modelo."""

from __future__ import annotations

from application.ia_sombra.comparador import comparar
from application.ia_sombra.propuesta import leer_propuesta
from tests.ia_sombra.ayudas import catalogo, json_ia, linea, recomendacion, vehiculo_ia


def _comparar(motor, ia_json, lineas=None):
    lineas = lineas or [linea()]
    return comparar(catalogo(), lineas, motor, leer_propuesta(ia_json, catalogo(), len(lineas)))


def test_la_misma_flota_coincide_en_todo():
    c = _comparar(recomendacion(("2L2", "FURGON")), json_ia(vehiculos=[vehiculo_ia("2L2", "FURGON")]))

    assert (c.coincide_resultado, c.coincide_flota, c.coincide_configuracion) == (True, True, True)
    assert (c.cantidad_motor, c.cantidad_ia) == (1, 1)
    assert (c.ia_respeta_peso, c.ia_respeta_volumen, c.ia_conserva_unidades) == (True, True, True)
    assert c.violaciones == ()


def test_otra_carroceria_es_otra_flota_pero_la_misma_configuracion():
    c = _comparar(recomendacion(("2L2", "FURGON")), json_ia(vehiculos=[vehiculo_ia("2L2", "ESTACAS")]))

    assert (c.coincide_flota, c.coincide_configuracion) == (False, True)


def test_otro_vehiculo_no_coincide_y_se_ve_la_diferencia_de_cantidad():
    c = _comparar(
        recomendacion(("2L2", "FURGON")),
        json_ia(vehiculos=[vehiculo_ia("C2", "FURGON", ((0, 30),)), vehiculo_ia("C2", "FURGON", ((0, 20),))]))

    assert (c.coincide_flota, c.coincide_configuracion) == (False, False)
    assert (c.cantidad_motor, c.cantidad_ia) == (1, 2)


def test_el_orden_de_los_vehiculos_no_importa():
    motor = recomendacion(("C2", "FURGON"), ("2L2", "FURGON"))
    ia = json_ia(vehiculos=[vehiculo_ia("2L2", "FURGON", ((0, 20),)), vehiculo_ia("C2", "FURGON", ((0, 30),))])

    assert _comparar(motor, ia).coincide_flota is True


def test_el_modelo_que_sobrecarga_un_vehiculo_se_detecta_aunque_el_diga_que_cabe():
    # 50 bultos de 20 kg = 1.000 kg en un Turbo de 4.000: cabe. 250 bultos = 5.000 kg: no.
    pesado = [linea(cantidad=250)]
    c = _comparar(recomendacion(("C2", "FURGON")), json_ia(vehiculos=[vehiculo_ia("2L2", "FURGON", ((0, 250),))]), pesado)

    assert c.ia_respeta_peso is False
    assert any("5000 kg superan los 4000 kg" in v for v in c.violaciones)


def test_el_exceso_de_volumen_se_mide_con_el_volumen_fisico():
    # 300 bultos de 0,08 m3 = 24 m3 > 22,77 m3 del Turbo, y 6.000 kg > 4.000 kg tambien.
    grande = [linea(cantidad=300, peso=1)]
    c = _comparar(recomendacion(("C2", "FURGON")), json_ia(vehiculos=[vehiculo_ia("2L2", "FURGON", ((0, 300),))]), grande)

    assert (c.ia_respeta_peso, c.ia_respeta_volumen) == (True, False)


def test_unidades_perdidas_o_repetidas_se_detectan():
    motor = recomendacion(("2L2", "FURGON"))

    perdidas = _comparar(motor, json_ia(vehiculos=[vehiculo_ia(carga=((0, 40),))]))
    repetidas = _comparar(motor, json_ia(vehiculos=[vehiculo_ia(carga=((0, 50),)), vehiculo_ia("C2", carga=((0, 5),))]))

    assert perdidas.ia_conserva_unidades is False
    assert repetidas.ia_conserva_unidades is False


def test_si_el_modelo_no_asigna_no_hay_nada_que_verificar():
    c = _comparar(recomendacion(("2L2", "FURGON")), json_ia("SIN_SELECCION_AUTOMATICA", motivo="OTRO"))

    assert c.coincide_resultado is False
    assert (c.ia_respeta_peso, c.ia_respeta_volumen, c.ia_conserva_unidades) == (None, None, None)
    assert c.flota_ia == ()


def test_los_dos_sin_seleccion_coinciden_y_el_motivo_ninguno_del_motor_es_nulo():
    c = _comparar(
        recomendacion(resultado="SIN_SELECCION_AUTOMATICA", motivo="EXTRADIMENSIONADO"),
        json_ia("SIN_SELECCION_AUTOMATICA", motivo="EXTRADIMENSIONADO"))
    asignado = _comparar(recomendacion(("2L2", "FURGON"), motivo="NINGUNO"), json_ia(vehiculos=[vehiculo_ia()]))

    assert c.coincide_resultado and c.coincide_flota
    assert (c.motivo_motor, c.motivo_ia) == ("EXTRADIMENSIONADO", "EXTRADIMENSIONADO")
    assert asignado.motivo_motor is None
