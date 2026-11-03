"""Catalogo del Cubicador calcado del de QA, para correr el motor sin base de datos.

Es el mismo que usan `tests/domain/test_seleccion_flota.py`: los 5 vehiculos con
las medidas y capacidades de QA, el override `3S3 + FURGON = 30.000 kg` y la regla
de incompatibilidad entre fertilizante y alimentos. Si el catalogo real cambia, la
linea base medida con este catalogo deja de ser comparable: hay que volver a medirla.
"""

from __future__ import annotations

from decimal import Decimal as D

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

FURGON = Carroceria("c-furgon", "FURGON", "Furgon")
FURGON_REFRIGERADO = Carroceria("c-furgon-ref", "FURGON_REFRIGERADO", "Furgon refrigerado")
ESTACAS = Carroceria("c-estacas", "ESTACAS", "Estacas")
PLATAFORMA = Carroceria("c-plataforma", "PLATAFORMA", "Plataforma")
CONTENEDOR_SECO = Carroceria("c-cnt-seco", "CONTENEDOR_SECO", "Contenedor seco")
PORTAVIDRIOS = Carroceria("c-portavidrios", "PORTAVIDRIOS", "Estructura portavidrios")

CARROCERIAS = (FURGON, FURGON_REFRIGERADO, ESTACAS, PLATAFORMA, CONTENEDOR_SECO, PORTAVIDRIOS)


def _vehiculo(codigo, nombre, largo, ancho, alto, capacidad, carrocerias, overrides=None):
    overrides = overrides or {}
    propias = tuple(
        CarroceriaDeVehiculo(c, D(overrides.get(c.codigo, capacidad))) for c in carrocerias
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


MERCANCIAS = (
    _mercancia("BEBIDAS", ALIMENTOS, FURGON, ESTACAS, secundaria_condicionada=True),
    _mercancia("CAJAS_CARTON", GENERAL, FURGON, ESTACAS, secundaria_condicionada=True),
    _mercancia("ALIMENTOS_SECOS", ALIMENTOS, FURGON, ESTACAS, secundaria_condicionada=True),
    _mercancia("FERTILIZANTES_ENSACADOS", FERTILIZANTE, FURGON, ESTACAS, secundaria_condicionada=True),
    _mercancia("PAPEL_ROLLOS", GENERAL, FURGON, PLATAFORMA, condicionada=True),
    _mercancia("CARNICOS", ALIMENTOS, FURGON_REFRIGERADO, control="REFRIGERADO"),
    _mercancia("BOBINAS_ACERO", METALICO, PLATAFORMA, condicionada=True, apilable=False),
    _mercancia("CARGA_CONTENERIZADA", GENERAL, CONTENEDOR_SECO, FURGON, secundaria_condicionada=True),
    _mercancia("VIDRIO_PLANO", VIDRIO, PORTAVIDRIOS, PLATAFORMA, condicionada=True),
)

REGLAS = (
    ReglaIncompatibilidad("r1", FERTILIZANTE.id, ALIMENTOS.id,
                          "Riesgo de contaminacion de alimentos por fertilizante ensacado."),
)

PARAMETROS = {
    "k_max": "25", "umbral_alerta_llenado": "0.75", "usar_alternativa": "solo_respaldo",
    "factor_vol_util": "0.90", "margen_cg": "0.30",
}

# Mismo tope por pieza que las pruebas: el del vehiculo mas grande que no es extradimensionado.
MEDIDAS_ESTANDAR = sf.MedidasEstandar(D(1220), D(240), D(260), D(34000))


def catalogo_qa() -> Catalogo:
    return Catalogo(
        vehiculos=(TURBO, SENCILLO, DOBLETROQUE, MINIMULA, TRACTOMULA),
        carrocerias=CARROCERIAS, mercancias=MERCANCIAS, reglas=REGLAS,
        parametros=ParametrosMotor(dict(PARAMETROS)),
    )
