"""Pruebas de ESCALA, con cota de tiempo (#304).

La suite de reglas llegaba a 1.200 unidades y ahi el motor responde en poco mas
de un segundo, asi que nunca entraba en la zona donde se rompia. Las dos
pruebas que el front hizo contra el despliegue -10.000 ladrillos y laminas de
1 m- fallaron con la suite en verde.

Estas pruebas cubren esa zona y **fallan si se exceden los segundos**: sin la
cota serian solo pruebas de resultado y la regresion de rendimiento volveria a
pasar sin que nada avise.

El tope es generoso a proposito. No mide la maquina: mide que no se haya vuelto
al empaque pieza por pieza, que tardaba 84 segundos en el caso del pliego.
"""

import time

import pytest

from domain.motor import seleccion_flota as sf
# Se importa solo lo que hace falta y no `*`: con el asterisco pytest vuelve a
# recolectar los 25 casos del otro modulo bajo este nombre.
from tests.domain.test_seleccion_flota import (  # noqa: F401  (catalogo es un fixture)
    BOBINAS,
    CAJAS,
    ESTACAS,
    FURGON,
    GENERAL,
    D,
    _linea,
    _mercancia,
    _pedir,
    catalogo,
)

# Margen amplio sobre lo medido (todo por debajo de 0,2 s) para que no sea
# fragil en CI, y aun asi dos ordenes de magnitud por debajo de lo que tardaba.
TOPE_SEGUNDOS = 5.0


def _medir(catalogo, *lineas):
    inicio = time.perf_counter()
    recomendacion = _pedir(catalogo, *lineas)
    return recomendacion, time.perf_counter() - inicio


def _exigir_rapido(segundos, etiqueta):
    assert segundos < TOPE_SEGUNDOS, (
        f"{etiqueta} tardo {segundos:.1f}s, por encima de {TOPE_SEGUNDOS}s. "
        "Probablemente se volvio a empacar pieza por pieza lo que la rejilla "
        "resuelve con una division (ver _repartir_homogenea).")


# --- el caso del pliego real -------------------------------------------------


@pytest.mark.parametrize("peso_unitario,unidades", [(50, 640), (25, 1280), (5, 6400)])
def test_la_linea_de_granel_del_pliego_responde_rapido(catalogo, peso_unitario, unidades):
    """L5 del pliego: 32.000 kg de granel, Cali -> Cartagena.

    El empaque del cliente decide cuantas unidades son, y nosotros no lo
    elegimos: con bultos de 5 kg son 6.400 unidades -entrada legal, por debajo
    del tope de 10.000- y tardaban 84 segundos contra un timeout de 15 en NEXO.
    """
    r, segundos = _medir(catalogo, _linea(CAJAS, unidades, 40, 40, 30, peso_unitario))
    _exigir_rapido(segundos, f"L5 con bultos de {peso_unitario} kg")
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert sum(m.cantidad for v in r.vehiculos for m in v.mercancias) == unidades


def test_el_tope_de_unidades_se_resuelve_rapido(catalogo):
    """10.000 unidades es exactamente el maximo que acepta el contrato."""
    r, segundos = _medir(catalogo, _linea(CAJAS, 10_000, 10, 10, 10, 5))
    _exigir_rapido(segundos, "10.000 unidades")
    assert r.resultado == sf.RESULTADO_ASIGNADO


# --- piezas que no teselan ---------------------------------------------------


@pytest.mark.parametrize("unidades,camiones", [(50, 2), (100, 3), (200, 5), (400, 9)])
def test_los_cubos_de_un_metro_se_asignan(catalogo, unidades, camiones):
    """Un cubo de 1 m deja 48 de los 76,1 m3 de una tractomula: el 63%.

    Con la cota por volumen, el motor pedia 3 vehiculos donde hacian falta 5,
    agotaba los intentos y devolvia BUSQUEDA_AGOTADA ante una carga que si
    sabia resolver -se comprobo subiendo el presupuesto a 500 intentos-.
    """
    r, segundos = _medir(catalogo, _linea(CAJAS, unidades, 100, 100, 100, 5))
    _exigir_rapido(segundos, f"{unidades} cubos de 1 m")
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert len(r.vehiculos) == camiones


def test_seiscientos_cubos_caben_con_el_tope_en_25(catalogo):
    """Necesitan 13 vehiculos. Con el tope en 10 se rechazaban (#304)."""
    r, segundos = _medir(catalogo, _linea(CAJAS, 600, 100, 100, 100, 5))
    _exigir_rapido(segundos, "600 cubos de 1 m")
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert len(r.vehiculos) == 13


def test_lo_que_de_verdad_no_cabe_dice_que_supera_el_tope(catalogo):
    """1.500 cubos necesitan 32 vehiculos y el tope son 25.

    Antes salia BUSQUEDA_AGOTADA, que confunde "me quede sin intentos" con "no
    cabe en el tope de camiones". Son cosas distintas para quien lo lee.
    """
    r, segundos = _medir(catalogo, _linea(CAJAS, 1500, 100, 100, 100, 5))
    _exigir_rapido(segundos, "1.500 cubos de 1 m")
    assert r.resultado == sf.RESULTADO_SIN_SELECCION
    assert r.motivo_sin_seleccion == sf.MOTIVO_SUPERA_K_MAX


def test_la_mercancia_no_apilable_se_asigna(catalogo):
    """No apilable = una sola capa, asi que el volumen aprovechable es una
    fraccion aun menor del geometrico. Mismo fallo que los cubos, agravado.

    Hoy **ninguna mercancia del catalogo real es no apilable** -se verifico
    contra `database/seeder/tiposMercancia.csv`-, pero la bandera existe y es
    editable por CRUD, asi que el caso tiene que estar cubierto antes de que
    alguien la marque.
    """
    r, segundos = _medir(catalogo, _linea(BOBINAS, 60, 120, 120, 120, 300))
    _exigir_rapido(segundos, "60 bobinas no apilables")
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert len(r.vehiculos) == 3


def test_la_mercancia_sin_rotacion_libre_se_asigna(catalogo):
    """La restriccion que SI existe hoy en el catalogo real.

    Ocho tipos no rotan: VARILLAS_PERFILES, TUBERIAS, BOBINAS_ACERO,
    POSTES_CONCRETO, PAPEL_ROLLOS, VIDRIO_PLANO, VENTANAS_FACHADAS y
    CILINDROS_GAS. Sin rotacion la rejilla solo puede usar la orientacion
    original, asi que entran menos unidades que las que el volumen sugiere -el
    mismo mecanismo que rompia los cubos-.
    """
    tubo = _mercancia("TUBERIAS", GENERAL, FURGON, ESTACAS, rotacion_libre=False)
    r, segundos = _medir(catalogo, _linea(tubo, 300, 600, 20, 20, 30))
    _exigir_rapido(segundos, "300 tubos sin rotacion")
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert sum(m.cantidad for v in r.vehiculos for m in v.mercancias) == 300


def test_la_rejilla_cuenta_menos_sin_rotacion_libre(catalogo):
    """Una pieza de 130x240x100 en una tractomula de 1220x240x260.

    En su orientacion original entran 9 x 1 x 2 = 18. Girada a 100x240x130
    entran 12 x 1 x 2 = 24, un tercio mas. Es el mismo efecto que midio
    S5-HU-17.3: la orientacion, no la fragmentacion, era lo que dejaba a DBLF
    casi 29 puntos por debajo de la rejilla en el peor caso.
    """
    tractomula = next(v for v in catalogo.vehiculos if v.codigo == "3S3")
    dims = (D(130), D(240), D(100))

    libre = _mercancia("LIBRE", GENERAL, FURGON)
    fija = _mercancia("FIJA", GENERAL, FURGON, rotacion_libre=False)

    con_rotacion = sf._caben_por_rejilla(tractomula, sf.MercanciaSolicitada(libre, 1, *dims, D(30)))
    sin_rotacion = sf._caben_por_rejilla(tractomula, sf.MercanciaSolicitada(fija, 1, *dims, D(30)))
    assert (con_rotacion, sin_rotacion) == (24, 18)


# --- la rejilla concuerda con lo que el empacador logra ----------------------


def test_la_rejilla_respeta_las_banderas_del_catalogo(catalogo):
    """Sin rotacion libre solo la orientacion original; no apilable, una capa."""
    tractomula = next(v for v in catalogo.vehiculos if v.codigo == "3S3")

    cubo = sf.MercanciaSolicitada(CAJAS, 1, D(100), D(100), D(100), D(5))
    assert sf._caben_por_rejilla(tractomula, cubo) == 48  # 12 x 2 x 2

    # BOBINAS no es apilable: la misma pieza pierde las capas de arriba.
    bobina = sf.MercanciaSolicitada(BOBINAS, 1, D(100), D(100), D(100), D(5))
    assert sf._caben_por_rejilla(tractomula, bobina) == 24  # 12 x 2 x 1


def test_una_pieza_que_no_entra_en_ninguna_orientacion_da_cero(catalogo):
    tractomula = next(v for v in catalogo.vehiculos if v.codigo == "3S3")
    gigante = sf.MercanciaSolicitada(CAJAS, 1, D(300), D(300), D(300), D(5))
    assert sf._caben_por_rejilla(tractomula, gigante) == 0


# --- el techo de tiempo ------------------------------------------------------
#
# FALTA la prueba de carga heterogenea masiva (#304). El techo de
# `_MAX_SEGUNDOS_BUSQUEDA` se revisa ENTRE intentos de empaque, no dentro de
# uno: con muchas posiciones de medidas distintas un solo intento puede tardar
# minutos, y el techo no lo interrumpe. Escribir la prueba antes de cerrar ese
# agujero deja la suite colgada, que es justo lo que paso al intentarlo.
#
# Cuando el corte funcione dentro del empaque, el caso es:
#   50 posiciones x 200 unidades, medidas distintas entre si
#   -> BUSQUEDA_AGOTADA en menos de _MAX_SEGUNDOS_BUSQUEDA * 3
