"""Regresion de los bugs reales de S4-HU-03 encontrados probando el flujo
normal de plan de cargue (crear vehiculo/plan propio, no el escenario demo).
Un archivo aparte de `test_motor.py` porque ese es un port 1:1 de las 26
reglas de negocio originales (numeradas) - estos son casos nuevos, no parte
de ese contrato."""

from __future__ import annotations

from domain.motor import validadores as v
from domain.motor.dominio import (
    Colocacion,
    Contenedor,
    ItemCarga,
    ParametrosOptimizacion,
    PosicionPuerta,
    ResultadoCubicacion,
    orientaciones_de,
)
from domain.motor.motor import MotorCubicacion3D


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


def contenedor_demo() -> Contenedor:
    return Contenedor(13600, 2450, 2700, 28000, 7000, 2400, 2650, PosicionPuerta.TRASERA, [])


# --------------------------------------------------------- #158 (CRITICO)


def test_tolerancia_paredes_no_impide_colocar_un_item_trivial_en_vehiculo_vacio():
    # Repro real: vehiculo 6000x2400x2400mm con toleranciaParedesMm=10 y un
    # unico item trivial de 1000x1000x1000mm - antes del fix, el punto extremo
    # semilla (0,0,0) SIEMPRE fallaba `dentro_de_limites` cuando t>0 (esa
    # funcion exige x>=t / y>=t), y como ningun item llegaba a colocarse nunca
    # se generaban mas puntos: el vehiculo quedaba inutilizable por completo,
    # vacio o no, con CUALQUIER item.
    c = Contenedor(
        6000, 2400, 2400, 10000, 2000, 2100, 2100, PosicionPuerta.TRASERA, [],
        tolerancia_paredes_mm=10, tolerancia_entre_unidades_mm=10,
    )
    r = correr(c, item("a", 1000, 1000, 1000, 200))
    assert len(r.colocaciones) == 1
    assert len(r.rechazos) == 0
    assert r.colocaciones[0].x >= 10
    assert r.colocaciones[0].y >= 10


def test_tolerancia_paredes_cero_sigue_igual_que_antes():
    # No-regresion: con t=0 (todos los presets/demo) el comportamiento es
    # identico al de siempre.
    c = Contenedor(6000, 2400, 2400, 10000, 2000, 2100, 2100, PosicionPuerta.TRASERA, [])
    r = correr(c, item("a", 1000, 1000, 1000, 200))
    assert len(r.colocaciones) == 1
    assert r.colocaciones[0].x == 0
    assert r.colocaciones[0].y == 0


# --------------------------------------------------------- #152


def test_mensaje_sin_espacio_distingue_item_que_no_cabe_del_vehiculo_de_fragmentacion():
    # El item es mas largo que el vehiculo COMPLETO (7000 > 6000, sin rotar):
    # nunca cabria ni en un vehiculo vacio, asi que el mensaje no debe hablar
    # de "espacio fragmentado o restricciones de apilamiento".
    c = Contenedor(6000, 2400, 2400, 10000, 2000, 2100, 2100, PosicionPuerta.TRASERA, [])
    r = correr(c, item("a", 7000, 1000, 1000, 200, rotable=False))
    assert len(r.rechazos) == 1
    assert r.rechazos[0].codigo_razon == "SIN_ESPACIO"
    assert "fragmentado" not in r.rechazos[0].razon
    assert "excede las dimensiones internas del vehiculo" in r.rechazos[0].razon


def test_mensaje_sin_espacio_por_fragmentacion_real_no_cambia():
    # No-regresion: el mensaje de fragmentacion real (item que SI cabria en un
    # vehiculo vacio, pero no en el espacio libre actual) sigue como antes.
    c = Contenedor(2100, 1300, 5000, 10000, 0, 1300, 4900, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("peq", 1100, 1200, 1000, 900, rotable=False),
        item("grande", 2000, 1000, 600, 100, rotable=False),
    )
    assert len(r.rechazos) == 1
    assert r.rechazos[0].codigo_razon == "SIN_ESPACIO"
    assert "fragmentado" in r.rechazos[0].razon


# --------------------------------------------------------- #153


def test_secuencia_bloqueada_se_consolida_en_una_sola_incidencia_por_colocacion():
    # 'a' queda tapada por DOS unidades de entrega posterior a la vez (lado a
    # lado en el mismo corredor hacia la puerta) - antes se generaba una
    # SECUENCIA_BLOQUEADA por cada bloqueador, duplicando la incidencia para
    # el mismo colocacion_item_id.
    cont = contenedor_demo()
    temprano = item("a", 1000, 2450, 1350, 100, rotable=False, secuencia=1)
    tardio1 = item("b", 1000, 1225, 1350, 100, rotable=False, secuencia=2)
    tardio2 = item("c", 1000, 1225, 1350, 100, rotable=False, secuencia=3)
    resultado = ResultadoCubicacion(
        contenedor=cont,
        rechazos=[],
        colocaciones=[
            Colocacion(item=temprano, x=0, y=0, z=0, orientacion=orientaciones_de(temprano)[0]),
            Colocacion(item=tardio1, x=1000, y=0, z=0, orientacion=orientaciones_de(tardio1)[0]),
            Colocacion(item=tardio2, x=1000, y=1225, z=0, orientacion=orientaciones_de(tardio2)[0]),
        ],
    )
    incidencias = MotorCubicacion3D().validar(resultado, ParametrosOptimizacion())
    secuencia_bloqueada = [i for i in incidencias if i.codigo_regla == "SECUENCIA_BLOQUEADA"]
    assert len(secuencia_bloqueada) == 1
    assert secuencia_bloqueada[0].colocacion_item_id == "a"
    assert resultado.colocaciones[0].accesibilidad == "bloqueada"


# --------------------------------------------------------- #154


def test_apilamiento_valida_max_peso_encima_contra_toda_la_cadena_de_soportes():
    # A (base) admite max 250kg encima; B (sin limite propio) se apoya sobre A
    # con 200kg (pasa el chequeo DIRECTO); C se apoya sobre B con 60kg - el
    # chequeo directo contra B tambien pasa (B no declara limite), pero el
    # peso de C SI baja hasta A (200+60=260kg > 250kg permitidos). Antes de
    # este fix nadie comparaba eso: `validar_apilamiento` solo miraba el
    # soporte DIRECTO de cada item, nunca la cadena completa.
    c = Contenedor(1200, 1000, 3000, 10000, 0, 1000, 1000, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("a", 1200, 1000, 1000, 500, rotable=False, max_peso_encima=250),
        item("b", 1200, 1000, 1000, 200, rotable=False),
        item("c", 1200, 1000, 1000, 60, rotable=False),
    )
    assert {x.item.id for x in r.colocaciones} == {"a", "b"}
    assert len(r.rechazos) == 1
    assert r.rechazos[0].item.id == "c"


def test_apilamiento_dentro_del_limite_de_toda_la_cadena_se_acepta():
    # No-regresion: si la cadena completa SIGUE dentro del limite (200+40=240 <=
    # 250), C debe poder apilarse normalmente.
    c = Contenedor(1200, 1000, 3000, 10000, 0, 1000, 1000, PosicionPuerta.TRASERA, [])
    r = correr(
        c,
        item("a", 1200, 1000, 1000, 500, rotable=False, max_peso_encima=250),
        item("b", 1200, 1000, 1000, 200, rotable=False),
        item("c", 1200, 1000, 1000, 40, rotable=False),
    )
    assert {x.item.id for x in r.colocaciones} == {"a", "b", "c"}
    assert len(r.rechazos) == 0
