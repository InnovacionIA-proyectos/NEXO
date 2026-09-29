"""S4-HU-12.2 - cuantos camiones hacen falta y cuanto lleva cada uno, por PESO.

Antes de esta issue el reparto era VORAZ: el empaque 3D llenaba un vehiculo
hasta donde cupiera -por volumen o por peso, lo que se agotara primero- y el
sobrante pasaba al siguiente. El ultimo camion quedaba casi vacio. La HU pide
lo contrario: "una distribucion equilibrada", "no solo la primera combinacion
que quepa".

Hay tres situaciones distintas y los tests cubren las tres, porque durante el
desarrollo la segunda se rompio sin que nada lo avisara:

  1. Manda el PESO      - carga pesada y compacta.
  2. Manda el VOLUMEN   - carga liviana y voluminosa. El peso da el MINIMO de
                          camiones, no el numero final.
  3. No hay peso        - dato faltante, no carga de cero kilos.

Lo que NO cambia en esta issue: que VEHICULO se elige. Se mantiene el criterio
que ya existia, por decision explicita de no inventar una regla de negocio que
negocio no dio. El texto de la issue dice "solo peso (no dimensiones)" y se
interpreta que eso aplica a CUANTOS camiones, no a cual.
"""

from __future__ import annotations

from domain.motor import cubicacion_rapida as r


def _unidad(capacidad_kg: float = 10_000, largo_m: float = 20.0, tara_kg: float = 4_000) -> r.UnidadCubicacion:
    """Vehiculo de laboratorio. `largo_m` holgado por defecto para que el unico
    limite sea el peso; se acorta cuando se quiere que mande el volumen."""
    return r.UnidadCubicacion(
        "prueba", "Camion de prueba", "cerrada", "ninguno",
        largo_m, 3.0, 3.0,
        capacidad_kg + tara_kg, tara_kg, [7_000, 7_000],
        "3S3", "FURGON", "Carga Suelta", None,
    )


def _carga(peso_total: float, bultos: int, lado_cm: float = 100) -> list[r.ItemCubicacion]:
    return [r.ItemCubicacion(descripcion="bulto", cantidad=bultos, largo_cm=lado_cm,
                             ancho_cm=lado_cm, alto_cm=lado_cm, peso_kg=peso_total / bultos)]


def _flota(items, unidad):
    respuesta = r.cubicar(r.CubicacionRequest(items=items), r.MedidasEstandar.por_defecto(), [unidad])
    return respuesta.sugerencia_flota, respuesta


# ------------------------------------------------------- 1. manda el peso


def test_la_cantidad_de_camiones_sale_del_peso():
    unidad = _unidad(capacidad_kg=10_000)

    flota, _ = _flota(_carga(18_000, bultos=18), unidad)

    assert flota is not None
    assert flota.cantidad_vehiculos == 2  # techo(18000 / 10000)
    assert flota.peso_total_kg == 18_000


def test_el_reparto_es_equilibrado_no_voraz():
    """El corazon de la issue. Con reparto voraz daria 10.000 + 8.000: el primer
    camion al tope y el ultimo con lo que sobre. Equilibrado da 9.000 + 9.000."""
    unidad = _unidad(capacidad_kg=10_000)

    flota, _ = _flota(_carga(18_000, bultos=18), unidad)

    assert flota.peso_por_vehiculo_kg == 9_000
    pesos_reales = [d.peso_total_kg for d in flota.disposiciones]
    assert pesos_reales == [9_000, 9_000], "las disposiciones tienen que decir lo mismo que el reparto"
    assert max(pesos_reales) - min(pesos_reales) == 0


def test_el_reparto_reportado_coincide_con_lo_que_de_verdad_se_empaca():
    """Si el empaque usara la capacidad plena en vez del objetivo equilibrado,
    la recomendacion y las disposiciones se contradirian. El usuario veria un
    numero y el plan de cargue otro."""
    unidad = _unidad(capacidad_kg=10_000)

    flota, _ = _flota(_carga(25_000, bultos=25), unidad)

    assert flota.cantidad_vehiculos == 3
    pesos_reales = [d.peso_total_kg for d in flota.disposiciones]
    assert sum(pesos_reales) == 25_000, "no se pierde ni se inventa carga"
    # 25.000 / 3 = 8.333,33 y los bultos son de 1.000: el reparto exacto es
    # imposible, pero el desvio no puede pasar de un bulto.
    assert max(pesos_reales) - min(pesos_reales) <= 1_000


def test_si_cabe_en_un_solo_camion_no_se_propone_flota():
    unidad = _unidad(capacidad_kg=10_000)

    flota, respuesta = _flota(_carga(9_000, bultos=9), unidad)

    assert flota is None
    assert respuesta.sugerencia is not None, "lo resuelve la sugerencia simple"


# ---------------------------------------------------- 2. manda el volumen


def test_el_peso_da_el_minimo_de_camiones_no_el_numero_final():
    """Carga liviana y voluminosa: 3.000 kg entran de sobra en un camion de
    10.000, pero 60 bultos de 1,2 m no caben en 34,6 m3.

    Esta es la regresion que se introdujo durante el desarrollo: al tomar el
    peso como numero FINAL, este vehiculo se descartaba entero y el usuario se
    quedaba sin ninguna sugerencia, con una advertencia que ademas decia que
    ningun vehiculo era compatible - cuando el vehiculo servia y solo hacian
    falta tres."""
    unidad = _unidad(capacidad_kg=10_000, largo_m=6.0)
    items = _carga(3_000, bultos=60, lado_cm=120)

    flota, _ = _flota(items, unidad)

    assert flota is not None, "por peso basta un camion, pero por volumen no: hay que proponer flota igual"
    assert flota.cantidad_vehiculos == 3
    assert flota.peso_por_vehiculo_kg == 1_000, "el reparto sigue siendo equilibrado sobre los que de verdad hacen falta"


def test_la_advertencia_dice_cuantos_camiones_hacen_falta():
    unidad = _unidad(capacidad_kg=10_000, largo_m=6.0)

    _, respuesta = _flota(_carga(3_000, bultos=60, lado_cm=120), unidad)

    assert any("se requieren 3" in a for a in respuesta.advertencias)


# -------------------------------------------------------- 3. falta el peso


def test_un_item_sin_peso_se_avisa_en_vez_de_quedarse_callado():
    """`_expandir` convierte un peso nulo en 0.0, asi que sin este aviso la
    flota devolveria "ninguna" y el usuario no sabria que el problema es un dato
    que falta -y que puede completar el mismo- y no que la carga quepa.

    El DTO del API ya exige el peso, pero el puente `crear_desde_linea` arma los
    items desde PosicionCarga, cuyo peso SI puede venir nulo desde NEXO."""
    unidad = _unidad()
    items = [r.ItemCubicacion(descripcion="tuberia", cantidad=10, largo_cm=100,
                              ancho_cm=100, alto_cm=100, peso_kg=None)]

    flota, respuesta = _flota(items, unidad)

    assert flota is None
    assert any("sin peso" in a and "tuberia" in a for a in respuesta.advertencias)


def test_el_aviso_no_enumera_una_lista_interminable():
    unidad = _unidad()
    items = [r.ItemCubicacion(descripcion=f"item {i}", cantidad=1, largo_cm=100,
                              ancho_cm=100, alto_cm=100, peso_kg=None) for i in range(10)]

    _, respuesta = _flota(items, unidad)

    aviso = next(a for a in respuesta.advertencias if "sin peso" in a)
    assert "10 item(s)" in aviso
    assert "y 7 mas" in aviso, "nombra los primeros y cuenta el resto"


# ------------------------------------------ lo que esta issue NO cambia


def test_la_eleccion_de_vehiculo_sigue_prefiriendo_la_clase_de_viaje():
    """Se mantiene el criterio anterior a proposito. Si alguien lo cambia sin
    una regla de negocio detras, este test lo detiene."""
    contenedor = r.UnidadCubicacion(
        "contenedor", "Contenedor", "cerrada", "ninguno", 20.0, 3.0, 3.0,
        14_000, 4_000, [7_000, 7_000], "3S3", "PORTACONTENEDORES", "Contenedor", "CNT40.ST")
    suelta = _unidad(capacidad_kg=10_000)

    respuesta = r.cubicar(
        r.CubicacionRequest(items=_carga(18_000, bultos=18), clase_viaje="Contenedor"),
        r.MedidasEstandar.por_defecto(), [suelta, contenedor])

    assert respuesta.sugerencia_flota.unidad_id == "contenedor"
