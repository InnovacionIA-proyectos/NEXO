"""Reglas de negocio del motor de cubicacion 3D (ver docs/testDocumentation/REGLAS_DE_NEGOCIO.md,
reglas 1-26). Portado de cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs.
"""

from __future__ import annotations

import pytest

from domain.motor import validadores as v
from domain.motor.dominio import (
    Colocacion,
    Contenedor,
    EjeVehiculo,
    EstrategiaOptimizacion,
    ItemCarga,
    Orientacion,
    ParametrosOptimizacion,
    PosicionPuerta,
    ResultadoCubicacion,
    orientaciones_de,
)
from domain.motor.motor import MotorCubicacion3D


def contenedor_demo(capacidad_kg: float = 28000) -> Contenedor:
    return Contenedor(
        largo_mm=13600, ancho_mm=2450, alto_mm=2700,
        capacidad_kg=capacidad_kg, tara_kg=7000,
        puerta_ancho_mm=2400, puerta_alto_mm=2650, puerta=PosicionPuerta.TRASERA,
        ejes=[EjeVehiculo(1500, 8000), EjeVehiculo(11000, 22000)],
    )


def item(
    id: str, l: float, w: float, h: float, peso: float,
    apilable: bool = True, rotable: bool = True, vertical: bool = False,
    secuencia: int = 1, max_peso_encima: float = -1, max_niveles: int = 0,
) -> ItemCarga:
    return ItemCarga(
        id, f"SKU-{id}", f"Item {id}", "P1", "Cliente", "Destino", secuencia,
        l, w, h, peso, apilable, max_niveles, max_peso_encima, rotable, vertical,
    )


def correr(c: Contenedor, *items: ItemCarga) -> ResultadoCubicacion:
    return MotorCubicacion3D().optimizar(list(items), c, ParametrosOptimizacion())


# ------------------------------------------------------------- 1-5: colisiones


def test_colision_unidades_separadas_no_colisionan():
    assert v.colisiona(0, 0, 0, 100, 100, 100, 200, 0, 0, 100, 100, 100) is False


def test_colision_unidades_adyacentes_contacto_exacto_no_colisionan():
    assert v.colisiona(0, 0, 0, 100, 100, 100, 100, 0, 0, 100, 100, 100) is False


def test_colision_parcialmente_superpuestas_colisionan():
    assert v.colisiona(0, 0, 0, 100, 100, 100, 50, 50, 0, 100, 100, 100) is True


def test_colision_una_contenida_en_otra_colisionan():
    assert v.colisiona(0, 0, 0, 500, 500, 500, 100, 100, 100, 50, 50, 50) is True


def test_colision_con_tolerancia_configurable_separacion_insuficiente_colisiona():
    # A 10 mm de separacion con tolerancia de 20 mm exigida: colision.
    assert v.colisiona(0, 0, 0, 100, 100, 100, 110, 0, 0, 100, 100, 100, tolerancia=20) is True
    assert v.colisiona(0, 0, 0, 100, 100, 100, 130, 0, 0, 100, 100, 100, tolerancia=20) is False


# ------------------------------------------------------------- 6-9: orientaciones


def test_orientaciones_prisma_general_son_seis():
    assert len(orientaciones_de(item("a", 100, 200, 300, 10))) == 6


def test_orientaciones_cubo_sin_duplicadas_es_una():
    assert len(orientaciones_de(item("a", 100, 100, 100, 10))) == 1


def test_orientaciones_mantener_vertical_solo_rotacion_horizontal():
    os = orientaciones_de(item("a", 100, 200, 300, 10, vertical=True))
    assert all(o.alto_mm == 300 for o in os)
    assert len(os) == 2  # 100x200 y 200x100


def test_orientaciones_no_rotable_solo_la_original():
    os = orientaciones_de(item("a", 100, 200, 300, 10, rotable=False))
    assert len(os) == 1
    o = os[0]
    assert (o.largo_mm, o.ancho_mm, o.alto_mm) == (100, 200, 300)


# ------------------------------------------------------------- 10-14: encaje y limites


def test_unidad_que_cabe_exactamente_se_carga():
    # Puerta del tamano de la seccion completa para aislar el encaje geometrico.
    c = Contenedor(13600, 2450, 2700, 28000, 7000, 2450, 2700, PosicionPuerta.TRASERA, [])
    r = correr(c, item("a", 13600, 2450, 2700, 1000, rotable=False))
    assert len(r.colocaciones) == 1
    assert len(r.rechazos) == 0


@pytest.mark.parametrize(
    "l, w, h",
    [
        (14000, 2000, 2000),  # excede el largo
        (2000, 14000, 2000),  # excede el ancho
        (2000, 2000, 3000),  # excede el alto
    ],
)
def test_unidad_que_excede_una_dimension_sin_rotacion_se_rechaza(l, w, h):
    r = correr(contenedor_demo(), item("a", l, w, h, 100, rotable=False))
    assert len(r.colocaciones) == 0
    assert len(r.rechazos) == 1


def test_unidad_que_solo_cabe_rotada_se_carga_rotandola():
    # 2600 de "ancho" no cabe (ancho util 2450) pero rota a lo largo si.
    r = correr(contenedor_demo(), item("a", 1000, 2600, 1000, 100, rotable=True))
    assert len(r.colocaciones) == 1
    assert r.colocaciones[0].orientacion.ancho_mm <= 2450


def test_unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza():
    r = correr(contenedor_demo(), item("a", 1000, 2600, 1000, 100, rotable=False))
    assert len(r.colocaciones) == 0


def test_dos_unidades_caben_lado_a_lado_sin_colision():
    r = correr(
        contenedor_demo(),
        item("a", 1200, 1000, 1000, 100, rotable=False),
        item("b", 1200, 1000, 1000, 100, rotable=False),
    )
    assert len(r.colocaciones) == 2
    assert not any(i.codigo_regla == "COLISION" for i in r.incidencias)


# ------------------------------------------------------------- 15-18: apilamiento y soporte


def test_apilamiento_valido_segunda_encima_de_la_primera():
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("base", 1200, 1000, 1000, 500, rotable=False),
        item("arriba", 1200, 1000, 1000, 100, rotable=False),
    )
    assert len(r.colocaciones) == 2
    arriba = next(x for x in r.colocaciones if x.item.id == "arriba")
    assert arriba.z == 1000
    assert arriba.soporte_id == "base"
    assert arriba.porcentaje_soporte == 1.0


def test_apilamiento_sobre_no_apilable_se_rechaza():
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("base", 1200, 1000, 1000, 500, apilable=False, rotable=False),
        item("arriba", 1200, 1000, 1000, 100, rotable=False),
    )
    assert len(r.colocaciones) == 1
    assert any(x.item.id == "arriba" for x in r.rechazos)


def test_apilamiento_excede_peso_soportado_se_rechaza():
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("base", 1200, 1000, 1000, 500, rotable=False, max_peso_encima=50),
        item("arriba", 1200, 1000, 1000, 100, rotable=False),
    )
    assert len(r.colocaciones) == 1
    assert any(x.item.id == "arriba" for x in r.rechazos)


def test_soporte_insuficiente_no_deja_unidades_flotando():
    # 'peq' (mayor volumen) entra primero al piso; 'grande' no cabe en el piso
    # restante y encima solo apoyaria el 55% de su base (< 60% minimo):
    # rechazo explicado como espacio fragmentado, nunca una unidad flotando.
    c = Contenedor(2100, 1300, 5000, 10000, 0, 1300, 4900, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("peq", 1100, 1200, 1000, 900, rotable=False),
        item("grande", 2000, 1000, 600, 100, rotable=False),
    )
    assert len(r.colocaciones) == 1
    assert len(r.rechazos) == 1
    assert r.rechazos[0].item.id == "grande"
    assert r.rechazos[0].codigo_razon == "SIN_ESPACIO"


# ------------------------------------------------------------- 19-22: peso y balance de ejes


def test_exceso_de_peso_total_se_rechaza_con_razon():
    r = correr(
        contenedor_demo(capacidad_kg=500),
        item("a", 1000, 1000, 1000, 400, rotable=False),
        item("b", 1000, 1000, 1000, 300, rotable=False),
    )
    assert len(r.colocaciones) == 1
    assert len(r.rechazos) == 1
    assert r.rechazos[0].codigo_razon == "PESO_TOTAL"


def test_balance_de_ejes_carga_centrada_estimacion_repartida():
    c = contenedor_demo()
    r = correr(c, item("a", 2000, 2000, 1000, 10000, rotable=False))
    por_eje = v.estimar_peso_por_eje(r.colocaciones, c)
    assert round(sum(por_eje)) == 10000  # se conserva el total


def _colocacion_en(it: ItemCarga, x: float) -> Colocacion:
    return Colocacion(item=it, x=x, y=0, z=0, orientacion=Orientacion(0, it.largo_mm, it.ancho_mm, it.alto_mm))


def test_balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia():
    c = contenedor_demo()  # ejes en 1500 y 11000
    # CM en el punto medio (6250): reparto 50/50 entre ambos ejes.
    col = _colocacion_en(item("a", 2000, 2000, 1000, 10000), x=5250)
    por_eje = v.estimar_peso_por_eje([col], c)
    assert round(por_eje[0]) == 5000
    assert round(por_eje[1]) == 5000


def test_balance_de_ejes_voladizo_efecto_palanca():
    c = contenedor_demo()  # ejes en 1500 y 11000
    # CM en 0, por delante del primer eje: el delantero recibe MAS que el peso
    # y el trasero una fraccion NEGATIVA (se descarga). La suma se conserva.
    col = _colocacion_en(item("a", 2000, 2000, 1000, 10000), x=0)
    por_eje = v.estimar_peso_por_eje([col], c)
    assert por_eje[0] > 10000, "el eje delantero soporta mas que el peso por el voladizo"
    assert por_eje[1] < 0, "el eje trasero se descarga (fraccion negativa)"
    assert round(sum(por_eje)) == 10000


# ------------------------------------------------------------- 23: puerta


def test_unidad_que_no_pasa_por_la_puerta_se_rechaza():
    # Cabe adentro (2450 ancho) pero la puerta es de 2400 y no es rotable.
    contenedor = contenedor_demo()
    r = correr(contenedor, item("a", 1000, 2420, 2500, 100, rotable=False))
    assert len(r.rechazos) == 1
    assert r.rechazos[0].codigo_razon == "PUERTA"


# ------------------------------------------------------------- 24-25: secuencia de entrega


def test_secuencia_entrega_temprana_al_fondo_genera_violacion():
    # Forzamos el bloqueo validando un plan manual: entrega 1 al fondo, entrega 2 tapandola.
    cont = contenedor_demo()
    temprano = item("a", 1000, 2450, 2700, 100, rotable=False, secuencia=1)
    tardio = item("b", 1000, 2450, 2700, 100, rotable=False, secuencia=2)
    resultado = ResultadoCubicacion(
        contenedor=cont,
        rechazos=[],
        colocaciones=[
            Colocacion(item=temprano, x=0, y=0, z=0, orientacion=orientaciones_de(temprano)[0]),
            Colocacion(item=tardio, x=1000, y=0, z=0, orientacion=orientaciones_de(tardio)[0]),
        ],
    )
    incidencias = MotorCubicacion3D().validar(resultado, ParametrosOptimizacion())
    assert any(i.codigo_regla == "SECUENCIA_BLOQUEADA" for i in incidencias)
    assert resultado.colocaciones[0].accesibilidad == "bloqueada"


def test_secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta():
    cont = contenedor_demo()
    items = [
        item("primera", 2000, 2400, 2000, 500, rotable=False, secuencia=1),
        item("ultima", 2000, 2400, 2000, 500, rotable=False, secuencia=3),
    ]
    r = MotorCubicacion3D().optimizar(
        items, cont, ParametrosOptimizacion(estrategia=EstrategiaOptimizacion.PRIORIZAR_SECUENCIA_ENTREGA),
    )
    primera = next(c for c in r.colocaciones if c.item.id == "primera")
    ultima = next(c for c in r.colocaciones if c.item.id == "ultima")
    assert v.distancia_a_puerta(primera, cont) < v.distancia_a_puerta(ultima, cont)
    assert not any(i.codigo_regla == "SECUENCIA_BLOQUEADA" for i in r.incidencias)


# ------------------------------------------------------------- 26: metricas


def test_metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util():
    cont = Contenedor(2000, 1000, 1000, 10000, 0, 1000, 1000, PosicionPuerta.TRASERA, [])
    items = [item("a", 1000, 1000, 1000, 100, rotable=False)]
    motor = MotorCubicacion3D()
    r = motor.optimizar(items, cont, ParametrosOptimizacion())
    m = motor.metricas(r, items)
    assert m.porcentaje_volumen == 50  # 1 m3 de 2 m3
    assert m.unidades_cargadas == 1
    assert m.peso_cargado_kg == 100
