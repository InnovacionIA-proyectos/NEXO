"""DBLF - Deepest Bottom-Left-Fill (S5-HU-17.3).

Es el oraculo de empaquetado de la seleccion de flota. No es un empacador
nuevo: reusa el mecanismo de puntos extremos de `OptimizadorExtremePoints`,
con sus validadores de colision, soporte y apilamiento, y cambia una sola
cosa - el orden en que recorre los puntos candidatos.
"""

from __future__ import annotations

from domain.motor import optimizadores as opt
from domain.motor.dominio import Contenedor, ItemCarga, ParametrosOptimizacion, PosicionPuerta


def _contenedor(largo_mm=6500.0, ancho_mm=2400.0, alto_mm=2400.0, capacidad_kg=8500.0):
    return Contenedor(
        largo_mm=largo_mm, ancho_mm=ancho_mm, alto_mm=alto_mm, capacidad_kg=capacidad_kg,
        tara_kg=0.0, puerta_ancho_mm=ancho_mm, puerta_alto_mm=alto_mm,
        puerta=PosicionPuerta.TRASERA, ejes=[],
    )


def _items(cantidad, largo=500.0, ancho=400.0, alto=400.0, peso=20.0, **banderas):
    return [
        ItemCarga(
            id=f"i{n}", sku="CAJA", descripcion="Caja", pedido=None, cliente=None, destino=None,
            secuencia_entrega=1, largo_mm=largo, ancho_mm=ancho, alto_mm=alto, peso_kg=peso,
            **banderas,
        )
        for n in range(cantidad)
    ]


def test_dblf_esta_registrado_como_algoritmo():
    assert "dblf" in opt.algoritmos_disponibles()
    assert isinstance(opt.obtener_optimizador("DBLF"), opt.DeepestBottomLeftFill)


def test_dblf_llena_desde_el_fondo_hacia_la_puerta():
    """La diferencia con FFD: DBLF agota la rebanada mas profunda - de pared a
    pared y de piso a techo - antes de avanzar en X. FFD completa el piso
    entero primero, asi que a igual cantidad de cajas deja el frente desparejo.
    """
    contenedor = _contenedor()
    colocadas, _ = opt.obtener_optimizador("dblf").optimizar(
        _items(12), contenedor, ParametrosOptimizacion())

    assert len(colocadas) == 12
    # En una seccion de 2400x2400 mm caben 36 cajas de 400x400: las 12 entran
    # enteras en la rebanada del fondo, apiladas en varios niveles, sin
    # avanzar un milimetro hacia la puerta.
    assert all(c.x == 0 for c in colocadas)
    assert len({c.z for c in colocadas}) > 1, "la rebanada del fondo sube, no se queda en el piso"


def test_dblf_y_ffd_llenan_distinto_el_mismo_vehiculo():
    contenedor = _contenedor()
    por_algoritmo = {}
    for nombre in ("dblf", "ffd"):
        colocadas, _ = opt.obtener_optimizador(nombre).optimizar(
            _items(12), contenedor, ParametrosOptimizacion())
        por_algoritmo[nombre] = sorted((c.x, c.y, c.z) for c in colocadas)
    assert por_algoritmo["dblf"] != por_algoritmo["ffd"]


def test_dblf_respeta_la_capacidad_de_peso():
    """425 cajas de 20 kg son 8.500 kg exactos: la 426 sobra por peso aunque
    quede volumen."""
    colocadas, rechazos = opt.obtener_optimizador("dblf").optimizar(
        _items(430), _contenedor(), ParametrosOptimizacion())
    assert len(colocadas) == 425
    assert all(r.codigo_razon == "PESO_TOTAL" for r in rechazos)


def test_dblf_no_apila_sobre_una_unidad_no_apilable():
    contenedor = _contenedor(largo_mm=1000.0, ancho_mm=1000.0, alto_mm=2400.0)
    colocadas, _ = opt.obtener_optimizador("dblf").optimizar(
        _items(4, largo=1000.0, ancho=1000.0, alto=500.0, apilable=False),
        contenedor, ParametrosOptimizacion())
    assert all(c.z == 0 for c in colocadas)
    assert len(colocadas) == 1


def test_dblf_deja_fuera_lo_que_no_cabe_en_ninguna_orientacion():
    colocadas, rechazos = opt.obtener_optimizador("dblf").optimizar(
        _items(1, largo=9000.0, ancho=2000.0, alto=2000.0), _contenedor(), ParametrosOptimizacion())
    assert colocadas == []
    assert rechazos[0].codigo_razon == "SIN_ESPACIO"
    assert "excede las dimensiones internas" in rechazos[0].razon


def test_dblf_elige_la_orientacion_que_mejor_embaldosa():
    """Una estiba de 120x100 cm en un Sencillo (650x240): de canto entran 5
    por fila y 2 de ancho; girada entran 6 y 2. Tomar la orientacion original
    primero daba 20 estibas donde entran 24 - un camion de mas en cargas
    grandes. Medido antes de agregar la preferencia: 6 puntos porcentuales por
    debajo del optimo de rejilla en promedio, 28,8 en el peor caso.
    """
    sencillo = _contenedor(largo_mm=6500.0, ancho_mm=2400.0, alto_mm=2400.0, capacidad_kg=1e9)
    estibas = _items(40, largo=1200.0, ancho=1000.0, alto=1000.0, peso=1.0)
    colocadas, _ = opt.obtener_optimizador("dblf").optimizar(
        estibas, sencillo, ParametrosOptimizacion())
    assert len(colocadas) == 24


def test_la_preferencia_de_orientacion_es_solo_de_dblf():
    """FFD y BFD conservan el orden historico de `orientaciones_de`: los 27
    casos dorados de paridad con el motor C# dependen de el."""
    from domain.motor.dominio import orientaciones_de

    item = _items(1, largo=1200.0, ancho=1000.0, alto=1000.0)[0]
    contenedor = _contenedor()
    por_defecto = orientaciones_de(item)

    assert opt.FirstFitDecreasing()._orientaciones(item, contenedor) == por_defecto
    assert opt.BestFitDecreasing()._orientaciones(item, contenedor) == por_defecto
    assert opt.DeepestBottomLeftFill()._orientaciones(item, contenedor) != por_defecto
