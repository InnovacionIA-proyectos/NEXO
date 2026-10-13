"""Motor de seleccion de flota (S5-HU-17.3).

El catalogo de estas pruebas replica el REAL de `dbo_cubicador`: los 5
vehiculos con sus medidas y capacidades de QA, el override
`3S3 + FURGON = 30.000 kg`, y la regla de incompatibilidad entre fertilizante
y alimentos. Si el catalogo real cambia, `scripts/verificar_mapeo_catalogo.py`
y la lectura contra QA lo detectan; estas pruebas fijan el COMPORTAMIENTO del
algoritmo, no los datos.
"""

from __future__ import annotations

from decimal import Decimal as D

import pytest

from domain.entidades.catalogo import (
    Carroceria,
    CarroceriaDeVehiculo,
    Catalogo,
    CategoriaMercancia,
    ParametrosMotor,
    ReglaIncompatibilidad,
    TipoMercancia,
    Vehiculo,
)
from domain.motor import seleccion_flota as sf

# --- catalogo de prueba, calcado del real -----------------------------------

FURGON = Carroceria("c-furgon", "FURGON", "Furgon")
FURGON_REFRIGERADO = Carroceria("c-furgon-ref", "FURGON_REFRIGERADO", "Furgon refrigerado")
ESTACAS = Carroceria("c-estacas", "ESTACAS", "Estacas")
PLATAFORMA = Carroceria("c-plataforma", "PLATAFORMA", "Plataforma")
CONTENEDOR_SECO = Carroceria("c-cnt-seco", "CONTENEDOR_SECO", "Contenedor seco")
PORTAVIDRIOS = Carroceria("c-portavidrios", "PORTAVIDRIOS", "Estructura portavidrios")

CARROCERIAS = (FURGON, FURGON_REFRIGERADO, ESTACAS, PLATAFORMA, CONTENEDOR_SECO, PORTAVIDRIOS)


def _vehiculo(codigo, nombre, largo, ancho, alto, capacidad, carrocerias, overrides=None):
    """Las medidas son las de QA. `overrides` son capacidades por combinacion."""
    overrides = overrides or {}
    propias = tuple(
        CarroceriaDeVehiculo(c, D(overrides.get(c.codigo, capacidad)))
        for c in carrocerias
    )
    return Vehiculo(f"v-{codigo}", codigo, nombre, D(largo), D(ancho), D(alto), D(capacidad), True, propias)


FURGONEABLES = (FURGON, FURGON_REFRIGERADO, ESTACAS, PLATAFORMA)

TURBO = _vehiculo("2L2", "Turbo 2L2", 450, 220, 230, 4000, FURGONEABLES)
SENCILLO = _vehiculo("C2", "Sencillo C2", 650, 240, 240, 8500, FURGONEABLES)
# Como en QA: el C3 NO admite furgon, solo estacas y plataforma.
DOBLETROQUE = _vehiculo("C3", "Dobletroque C3", 800, 240, 260, 17000, (ESTACAS, PLATAFORMA))
MINIMULA = _vehiculo("2S2", "Minimula 2S2", 1220, 240, 260, 18000, FURGONEABLES)
TRACTOMULA = _vehiculo("3S3", "Tractomula 3S3", 1220, 240, 260, 34000, FURGONEABLES,
                       overrides={"FURGON": 30000, "FURGON_REFRIGERADO": 30000})

GENERAL = CategoriaMercancia("cat-general", "GENERAL")
ALIMENTOS = CategoriaMercancia("cat-alimentos", "ALIMENTOS")
FERTILIZANTE = CategoriaMercancia("cat-fertilizante", "FERTILIZANTE")
METALICO = CategoriaMercancia("cat-metalico", "METALICO")
VIDRIO = CategoriaMercancia("cat-vidrio", "VIDRIO")


def _mercancia(codigo, categoria, principal, secundaria=None, *, condicionada=False,
               secundaria_condicionada=False, apilable=True, fragil=False, rotacion_libre=True,
               control="NINGUNO"):
    return TipoMercancia(
        id=f"m-{codigo}", codigo=codigo, nombre=codigo.title(), categoria=categoria,
        cubicable=True, condicionada=condicionada, carroceria_principal=principal,
        carroceria_secundaria=secundaria, secundaria_condicionada=secundaria_condicionada,
        control_ambiental=control, apilable=apilable, fragil=fragil, rotacion_libre=rotacion_libre,
    )


BEBIDAS = _mercancia("BEBIDAS", ALIMENTOS, FURGON, ESTACAS, secundaria_condicionada=True)
CAJAS = _mercancia("CAJAS_CARTON", GENERAL, FURGON, ESTACAS, secundaria_condicionada=True)
ALIMENTOS_SECOS = _mercancia("ALIMENTOS_SECOS", ALIMENTOS, FURGON, ESTACAS, secundaria_condicionada=True)
FERTILIZANTES = _mercancia("FERTILIZANTES_ENSACADOS", FERTILIZANTE, FURGON, ESTACAS,
                           secundaria_condicionada=True)
PAPEL_ROLLOS = _mercancia("PAPEL_ROLLOS", GENERAL, FURGON, PLATAFORMA, condicionada=True)
CARNICOS = _mercancia("CARNICOS", ALIMENTOS, FURGON_REFRIGERADO, control="REFRIGERADO")
BOBINAS = _mercancia("BOBINAS_ACERO", METALICO, PLATAFORMA, condicionada=True, apilable=False)
# Como en QA: su principal no tiene ningun vehiculo asignado.
CONTENERIZADA = _mercancia("CARGA_CONTENERIZADA", GENERAL, CONTENEDOR_SECO, FURGON,
                           secundaria_condicionada=True)
VIDRIO_PLANO = _mercancia("VIDRIO_PLANO", VIDRIO, PORTAVIDRIOS, PLATAFORMA, condicionada=True)

MERCANCIAS = (BEBIDAS, CAJAS, ALIMENTOS_SECOS, FERTILIZANTES, PAPEL_ROLLOS, CARNICOS,
              BOBINAS, CONTENERIZADA, VIDRIO_PLANO)

REGLAS = (
    ReglaIncompatibilidad("r1", FERTILIZANTE.id, ALIMENTOS.id,
                          "Riesgo de contaminacion de alimentos por fertilizante ensacado."),
)

PARAMETROS = ParametrosMotor({
    "k_max": "10", "umbral_alerta_llenado": "0.75", "usar_alternativa": "solo_respaldo",
    "factor_vol_util": "0.90", "margen_cg": "0.30",
})

MEDIDAS = sf.MedidasEstandar(D(1220), D(260), D(300), D(34000))


@pytest.fixture
def catalogo() -> Catalogo:
    return Catalogo(
        vehiculos=(TURBO, SENCILLO, DOBLETROQUE, MINIMULA, TRACTOMULA),
        carrocerias=CARROCERIAS, mercancias=MERCANCIAS, reglas=REGLAS, parametros=PARAMETROS,
    )


def _pedir(catalogo, *lineas, clase=None):
    return sf.recomendar(catalogo, sf.SolicitudRecomendacion(tuple(lineas), MEDIDAS, clase))


def _linea(tipo, cantidad, largo, ancho, alto, peso):
    return sf.MercanciaSolicitada(tipo, cantidad, D(largo), D(ancho), D(alto), D(peso))


# --- 1. dominancia por carroceria -------------------------------------------


def test_la_dominancia_se_cumple_en_el_catalogo_real(catalogo):
    sf.validar_dominancia(catalogo)  # no levanta


def test_la_dominancia_rota_falla_con_error_explicito(catalogo):
    """Mas volumen con menos capacidad: la poda del minimo descartaria la
    respuesta correcta y devolveria un camion de mas sin que nada lo avise."""
    gigante_debil = _vehiculo("XX", "Trailer liviano", 1400, 250, 270, 5000, (FURGON,))
    roto = Catalogo(
        vehiculos=(*catalogo.vehiculos, gigante_debil), carrocerias=CARROCERIAS,
        mercancias=MERCANCIAS, reglas=REGLAS, parametros=PARAMETROS,
    )
    with pytest.raises(sf.CatalogoInconsistente) as error:
        sf.validar_dominancia(roto)
    assert "FURGON" in str(error.value)
    assert "XX" in str(error.value)


# --- 2. flota minima y mixta ------------------------------------------------


def test_lo_que_cabe_en_uno_devuelve_uno(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 50, 50, 40, 40, 20))
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert len(r.vehiculos) == 1


def test_una_carga_grande_devuelve_flota_mixta(catalogo):
    """1.200 bultos: 96 m3 y 24.000 kg. Dos Minimulas tambien alcanzarian,
    pero un Sencillo mas una Minimula sobra menos - y esa es la respuesta."""
    r = _pedir(catalogo, _linea(BEBIDAS, 1200, 50, 40, 40, 20))
    assert r.resultado == sf.RESULTADO_ASIGNADO
    codigos = sorted(v.codigo_vehiculo for v in r.vehiculos)
    assert codigos == ["2S2", "C2"], "la flota deberia ser mixta, no dos vehiculos iguales"
    assert sum(m.cantidad for v in r.vehiculos for m in v.mercancias) == 1200


def test_la_flota_reparte_toda_la_carga_sin_perder_unidades(catalogo):
    r = _pedir(catalogo, _linea(CAJAS, 400, 60, 40, 50, 8))
    colocadas = sum(m.cantidad for v in r.vehiculos for m in v.mercancias)
    assert colocadas == 400


# --- 3. los dos porcentajes -------------------------------------------------


def test_reporta_los_dos_porcentajes_en_cada_vehiculo(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 100, 50, 40, 40, 20))
    for v in r.vehiculos:
        assert v.porcentaje_llenado_volumen > 0
        assert v.porcentaje_llenado_peso > 0


def test_una_carga_pesada_y_compacta_decide_por_peso(catalogo):
    """20 bultos de 500 kg: 2 m3 (nada) y 10.000 kg. Por volumen cabrian en un
    Turbo; manda el peso, que exige al menos una Minimula."""
    r = _pedir(catalogo, _linea(BEBIDAS, 20, 50, 40, 50, 500))
    assert r.resultado == sf.RESULTADO_ASIGNADO
    elegido = r.vehiculos[0]
    assert elegido.porcentaje_llenado_peso > elegido.porcentaje_llenado_volumen
    assert elegido.porcentaje_decisorio == elegido.porcentaje_llenado_peso
    assert elegido.codigo_vehiculo in ("2S2", "3S3")


def test_el_porcentaje_de_peso_usa_la_capacidad_de_la_combinacion(catalogo):
    """`3S3 + FURGON` son 30.000 kg, no los 34.000 del chasis: 27.000 kg dan
    90% contra la combinacion y darian 79,4% contra el chasis.

    48 cubos de 1 m es lo maximo que entra geometricamente en una tractomula
    (12 de largo x 2 de ancho x 2 de alto), asi que la flota es un solo
    vehiculo y el porcentaje es directo.
    """
    r = _pedir(catalogo, _linea(BEBIDAS, 48, 100, 100, 100, D("562.5")))
    assert len(r.vehiculos) == 1
    tractomula = r.vehiculos[0]
    assert tractomula.codigo_vehiculo == "3S3"
    assert tractomula.peso_total_kg == D("27000.00")
    assert tractomula.porcentaje_llenado_peso == D("90.0")


# --- 4. carga heterogenea ---------------------------------------------------


def test_un_vehiculo_con_dos_mercancias_queda_marcado_heterogeneo(catalogo):
    r = _pedir(
        catalogo,
        _linea(BEBIDAS, 60, 50, 40, 40, 20),
        _linea(CAJAS, 60, 60, 40, 50, 8),
    )
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert any(v.heterogenea for v in r.vehiculos)
    cargado = {m.codigo_mercancia for v in r.vehiculos for m in v.mercancias}
    assert cargado == {"BEBIDAS", "CAJAS_CARTON"}


# --- 5. incompatibilidades --------------------------------------------------


def test_dos_mercancias_incompatibles_no_comparten_vehiculo(catalogo):
    r = _pedir(
        catalogo,
        _linea(FERTILIZANTES, 100, 80, 50, 30, 50),
        _linea(ALIMENTOS_SECOS, 100, 60, 40, 40, 25),
    )
    assert r.resultado == sf.RESULTADO_ASIGNADO
    for vehiculo in r.vehiculos:
        codigos = {m.codigo_mercancia for m in vehiculo.mercancias}
        assert not {"FERTILIZANTES_ENSACADOS", "ALIMENTOS_SECOS"} <= codigos
    assert any("no pueden viajar juntas" in a for a in r.advertencias)


def test_mercancias_compatibles_de_categorias_distintas_si_comparten(catalogo):
    """La regla es por PARES concretos: bebidas y cajas son categorias
    distintas y viajan juntas. El validador de grupos del motor 3D las habria
    separado, por eso no se usa."""
    r = _pedir(
        catalogo,
        _linea(BEBIDAS, 30, 50, 40, 40, 20),
        _linea(CAJAS, 30, 60, 40, 50, 8),
    )
    assert len(r.vehiculos) == 1


# --- 6. condicionada --------------------------------------------------------


def test_una_mercancia_condicionada_sale_normal_y_marcada(catalogo):
    r = _pedir(catalogo, _linea(PAPEL_ROLLOS, 40, 120, 120, 100, 400))
    assert r.resultado == sf.RESULTADO_ASIGNADO, "condicionada no bloquea"
    assert r.vehiculos, "tiene que traer vehiculo igual"
    assert r.condicionada is True
    assert "PAPEL_ROLLOS" in r.mercancias_condicionadas


def test_la_carroceria_de_respaldo_entra_solo_si_la_principal_no_sirve(catalogo):
    """`CARGA_CONTENERIZADA` tiene `CONTENEDOR_SECO` como principal, que no
    tiene ningun vehiculo asignado: cae a `FURGON` y queda condicionada."""
    r = _pedir(catalogo, _linea(CONTENERIZADA, 50, 60, 40, 40, 25))
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert all(v.codigo_carroceria == "FURGON" for v in r.vehiculos)
    assert "CARGA_CONTENERIZADA" in r.mercancias_condicionadas


# --- 7. sin seleccion automatica --------------------------------------------


def test_una_pieza_fuera_de_medida_devuelve_extradimensionado(catalogo):
    r = _pedir(catalogo, _linea(BOBINAS, 2, 1400, 60, 60, 3000))
    assert r.resultado == sf.RESULTADO_SIN_SELECCION
    assert r.motivo_sin_seleccion == sf.MOTIVO_EXTRADIMENSIONADO
    assert r.clase_viaje == sf.CLASE_EXTRADIMENSIONADO
    assert r.vehiculos == ()
    assert r.extradimension is not None and r.extradimension.hay_extradimension
    assert r.extradimension.piezas[0].codigo_mercancia == "BOBINAS_ACERO"


def test_una_carga_que_no_entra_en_k_max_vehiculos(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 40000, 50, 40, 40, 20))
    assert r.resultado == sf.RESULTADO_SIN_SELECCION
    assert r.motivo_sin_seleccion == sf.MOTIVO_SUPERA_K_MAX


def test_mercancias_que_no_comparten_carroceria(catalogo):
    """Carnicos solo van en furgon refrigerado; bobinas solo en plataforma."""
    r = _pedir(
        catalogo,
        _linea(CARNICOS, 10, 60, 40, 40, 25),
        _linea(BOBINAS, 10, 100, 100, 80, 900),
    )
    assert r.resultado == sf.RESULTADO_SIN_SELECCION
    assert r.motivo_sin_seleccion == sf.MOTIVO_SIN_CARROCERIA_COMPATIBLE


def test_una_pieza_que_no_cabe_en_ningun_vehiculo_del_catalogo(catalogo):
    """Dentro de las medidas estandar, pero mas ancha que cualquier furgon."""
    r = _pedir(catalogo, _linea(BEBIDAS, 1, 300, 255, 250, 100))
    assert r.resultado == sf.RESULTADO_SIN_SELECCION
    assert r.motivo_sin_seleccion == sf.MOTIVO_SIN_CANDIDATOS_DIMENSIONALES


# --- 8. clase de viaje ------------------------------------------------------


def test_la_clase_de_viaje_sale_de_la_carroceria(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 10, 50, 40, 40, 20))
    assert r.clase_viaje == sf.CLASE_CARGA_SUELTA


def test_contenedor_se_detecta_por_prefijo(catalogo):
    """En el catalogo no existe `CONTENEDOR` suelto: son `CONTENEDOR_SECO` y
    `CONTENEDOR_REFRIGERADO`. Comparar por igualdad dejaria la clase mal."""
    assert sf._clase_de_viaje("CONTENEDOR_SECO", False) == sf.CLASE_CONTENEDOR
    assert sf._clase_de_viaje("CONTENEDOR_REFRIGERADO", False) == sf.CLASE_CONTENEDOR
    assert sf._clase_de_viaje("FURGON", False) == sf.CLASE_CARGA_SUELTA


def test_la_clase_preferida_no_filtra_solo_advierte(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 10, 50, 40, 40, 20), clase="Contenedor")
    assert r.resultado == sf.RESULTADO_ASIGNADO, "la preferencia no puede vetar la respuesta"
    assert any("preferencia, no" in a for a in r.advertencias)


# --- 9. alerta de bajo llenado ----------------------------------------------


def test_un_vehiculo_poco_lleno_queda_marcado(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 5, 50, 40, 40, 20))
    assert r.vehiculos[0].alerta_bajo_llenado is True
    assert any("por debajo del" in a for a in r.advertencias)


def test_un_vehiculo_bien_aprovechado_no_se_marca(catalogo):
    r = _pedir(catalogo, _linea(BEBIDAS, 400, 50, 40, 40, 20))
    assert any(not v.alerta_bajo_llenado for v in r.vehiculos)
