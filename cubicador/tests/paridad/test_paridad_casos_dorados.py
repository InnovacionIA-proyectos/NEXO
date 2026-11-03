"""Suite de paridad S2-HU-07.3 (hotspot H5): compara el motor Python contra
la salida REAL del motor C# de cabinaFletes para cada uno de los 26 "casos
dorados" de `cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs`.

El "esperado" de cada test NO es un valor escrito a mano releyendo el test
C# (eso es lo que hace `tests/domain/test_motor.py`, que verifica reglas de
negocio de este mismo motor) - es el JSON grabado por
`herramientas/generador_casos_dorados/` corriendo el motor C# de verdad. Si
alguno de estos 26 no coincide, es la suite de paridad la que debe fallar el
build (criterio de cierre de la HU), no una diferencia que se documenta y se
deja pasar.

Ver `herramientas/generador_casos_dorados/README.md` para regenerar los
fixtures si el motor C# o los casos de `MotorCubicacion3DTests.cs` cambian
(no debería pasar - ese motor está congelado, es el que este servicio
reemplaza).
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from domain.motor import validadores as v
from domain.motor.dominio import (
    Contenedor,
    Colocacion,
    EstrategiaOptimizacion,
    Orientacion,
    ParametrosOptimizacion,
    PosicionPuerta,
    ResultadoCubicacion,
    orientaciones_de,
)
from domain.motor.motor import MotorCubicacion3D
from tests.domain.test_motor import contenedor_demo, correr, item

FIXTURES_DIR = Path(__file__).parent / "fixtures_dorados"


def _cargar(nombre_archivo: str) -> dict:
    return json.loads((FIXTURES_DIR / nombre_archivo).read_text(encoding="utf-8"))


def _redondear(obj, decimales: int = 6):
    """Redondea todo float anidado (dict/list) - evita falsos negativos por
    ruido de punto flotante entre `decimal` (C#) y `float` (Python) en
    valores que matematicamente deberian coincidir exacto."""
    if isinstance(obj, float):
        return round(obj, decimales)
    if isinstance(obj, dict):
        return {k: _redondear(v_, decimales) for k, v_ in obj.items()}
    if isinstance(obj, list):
        return [_redondear(v_, decimales) for v_ in obj]
    return obj


def _assert_paridad(caso: str, esperado: dict, obtenido: dict) -> None:
    esperado_r, obtenido_r = _redondear(esperado), _redondear(obtenido)
    assert obtenido_r == esperado_r, (
        f"\nParidad rota en '{caso}' contra el motor C# real de cabinaFletes:\n"
        f"  esperado (motor C#):    {esperado_r}\n"
        f"  obtenido (motor Python): {obtenido_r}\n"
    )


def _proyectar_resultado(r: ResultadoCubicacion) -> dict:
    """Mismo shape que `ProyectarResultado` del harness C# - ver
    herramientas/generador_casos_dorados/Program.cs."""
    return {
        "colocaciones": [
            {
                "itemId": c.item.id,
                "x": c.x, "y": c.y, "z": c.z,
                "orientacion": {
                    "indice": c.orientacion.indice, "largoMm": c.orientacion.largo_mm,
                    "anchoMm": c.orientacion.ancho_mm, "altoMm": c.orientacion.alto_mm,
                },
                "soporteId": c.soporte_id,
                "porcentajeSoporte": c.porcentaje_soporte,
                "estadoEstabilidad": c.estado_estabilidad,
                "accesibilidad": c.accesibilidad,
            }
            for c in r.colocaciones
        ],
        "rechazos": [{"itemId": x.item.id, "codigoRazon": x.codigo_razon} for x in r.rechazos],
        "incidenciasCodigos": sorted({i.codigo_regla for i in r.incidencias}),
    }


# ------------------------------------------------------------- 1-5: colisiones (genericos, via fixture)

@pytest.mark.parametrize("numero", ["01", "02", "03", "04", "05"])
def test_paridad_colisiones(numero: str) -> None:
    archivo = next(FIXTURES_DIR.glob(f"caso_{numero}_*.json"))
    fixture = _cargar(archivo.name)
    assert fixture["tipo"] == "colisiona"
    for sub in fixture["casos"]:
        i, e = sub["input"], sub["esperado"]
        obtenido = v.colisiona(
            i["ax"], i["ay"], i["az"], i["al"], i["aw"], i["ah"],
            i["bx"], i["by"], i["bz"], i["bl"], i["bw"], i["bh"],
            tolerancia=i["tolerancia"],
        )
        _assert_paridad(fixture["caso"], e, {"resultado": obtenido})


# ------------------------------------------------------------- 6-9: orientaciones (genericos, via fixture)

@pytest.mark.parametrize("numero", ["06", "07", "08", "09"])
def test_paridad_orientaciones(numero: str) -> None:
    archivo = next(FIXTURES_DIR.glob(f"caso_{numero}_*.json"))
    fixture = _cargar(archivo.name)
    assert fixture["tipo"] == "orientaciones"
    for sub in fixture["casos"]:
        i, e = sub["input"], sub["esperado"]
        it = item("a", i["largoMm"], i["anchoMm"], i["altoMm"], 10, rotable=i["rotable"], vertical=i["mantenerVertical"])
        obtenidas = [{"largoMm": o.largo_mm, "anchoMm": o.ancho_mm, "altoMm": o.alto_mm} for o in orientaciones_de(it)]
        _assert_paridad(fixture["caso"], e, {"orientaciones": obtenidas})


# ------------------------------------------------------------- 10-14: encaje y limites

def test_paridad_caso_10_unidad_que_cabe_exactamente_se_carga() -> None:
    fixture = _cargar("caso_10_unidad_que_cabe_exactamente_se_carga.json")
    c = Contenedor(13600, 2450, 2700, 28000, 7000, 2450, 2700, PosicionPuerta.TRASERA, [])
    r = correr(c, item("a", 13600, 2450, 2700, 1000, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_11_unidad_que_excede_una_dimension_sin_rotacion_se_rechaza() -> None:
    fixture = _cargar("caso_11_unidad_que_excede_una_dimension_sin_rotacion_se_rechaza.json")
    for sub in fixture["casos"]:
        i = sub["input"]
        r = correr(contenedor_demo(), item("a", i["largoMm"], i["anchoMm"], i["altoMm"], 100, rotable=False))
        _assert_paridad(fixture["caso"], sub["esperado"], _proyectar_resultado(r))


def test_paridad_caso_12_unidad_que_solo_cabe_rotada_se_carga_rotandola() -> None:
    fixture = _cargar("caso_12_unidad_que_solo_cabe_rotada_se_carga_rotandola.json")
    r = correr(contenedor_demo(), item("a", 1000, 2600, 1000, 100, rotable=True))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_13_unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza() -> None:
    fixture = _cargar("caso_13_unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza.json")
    r = correr(contenedor_demo(), item("a", 1000, 2600, 1000, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_14_dos_unidades_caben_lado_a_lado_sin_colision() -> None:
    fixture = _cargar("caso_14_dos_unidades_caben_lado_a_lado_sin_colision.json")
    r = correr(
        contenedor_demo(),
        item("a", 1200, 1000, 1000, 100, rotable=False),
        item("b", 1200, 1000, 1000, 100, rotable=False),
    )
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


# ------------------------------------------------------------- 15-18: apilamiento y soporte

def test_paridad_caso_15_apilamiento_valido_segunda_encima_de_la_primera() -> None:
    fixture = _cargar("caso_15_apilamiento_valido_segunda_encima_de_la_primera.json")
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(c, item("base", 1200, 1000, 1000, 500, rotable=False), item("arriba", 1200, 1000, 1000, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_16_apilamiento_sobre_no_apilable_se_rechaza() -> None:
    fixture = _cargar("caso_16_apilamiento_sobre_no_apilable_se_rechaza.json")
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(c, item("base", 1200, 1000, 1000, 500, apilable=False, rotable=False), item("arriba", 1200, 1000, 1000, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_17_apilamiento_excede_peso_soportado_se_rechaza() -> None:
    fixture = _cargar("caso_17_apilamiento_excede_peso_soportado_se_rechaza.json")
    c = Contenedor(1300, 1100, 5000, 10000, 0, 1100, 4900, PosicionPuerta.TRASERA, [])
    r = correr(c, item("base", 1200, 1000, 1000, 500, rotable=False, max_peso_encima=50), item("arriba", 1200, 1000, 1000, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


def test_paridad_caso_18_soporte_insuficiente_no_deja_unidades_flotando() -> None:
    fixture = _cargar("caso_18_soporte_insuficiente_no_deja_unidades_flotando.json")
    c = Contenedor(2100, 1300, 5000, 10000, 0, 1300, 4900, PosicionPuerta.TRASERA, [])
    r = correr(c, item("peq", 1100, 1200, 1000, 900, rotable=False), item("grande", 2000, 1000, 600, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


# ------------------------------------------------------------- 19: peso total

def test_paridad_caso_19_exceso_de_peso_total_se_rechaza_con_razon() -> None:
    fixture = _cargar("caso_19_exceso_de_peso_total_se_rechaza_con_razon.json")
    r = correr(contenedor_demo(capacidad_kg=500), item("a", 1000, 1000, 1000, 400, rotable=False), item("b", 1000, 1000, 1000, 300, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


# ------------------------------------------------------------- 20-22: balance de ejes

def test_paridad_caso_20_balance_de_ejes_carga_centrada_estimacion_repartida() -> None:
    fixture = _cargar("caso_20_balance_de_ejes_carga_centrada_estimacion_repartida.json")
    c = contenedor_demo()
    r = correr(c, item("a", 2000, 2000, 1000, 10000, rotable=False))
    por_eje = v.estimar_peso_por_eje(r.colocaciones, c)
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], {"sumaRedondeada": round(sum(por_eje))})


def _colocacion_en(it, x: float) -> Colocacion:
    return Colocacion(item=it, x=x, y=0, z=0, orientacion=Orientacion(0, it.largo_mm, it.ancho_mm, it.alto_mm))


def test_paridad_caso_21_balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia() -> None:
    fixture = _cargar("caso_21_balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia.json")
    c = contenedor_demo()
    col = _colocacion_en(item("a", 2000, 2000, 1000, 10000), x=5250)
    por_eje = v.estimar_peso_por_eje([col], c)
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], {"porEjeRedondeado": [round(x) for x in por_eje]})


def test_paridad_caso_22_balance_de_ejes_voladizo_efecto_palanca() -> None:
    fixture = _cargar("caso_22_balance_de_ejes_voladizo_efecto_palanca.json")
    c = contenedor_demo()
    col = _colocacion_en(item("a", 2000, 2000, 1000, 10000), x=0)
    por_eje = v.estimar_peso_por_eje([col], c)
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], {"porEjeRedondeado": [round(x) for x in por_eje]})


# ------------------------------------------------------------- 23: puerta

def test_paridad_caso_23_unidad_que_no_pasa_por_la_puerta_se_rechaza() -> None:
    fixture = _cargar("caso_23_unidad_que_no_pasa_por_la_puerta_se_rechaza.json")
    r = correr(contenedor_demo(), item("a", 1000, 2420, 2500, 100, rotable=False))
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], _proyectar_resultado(r))


# ------------------------------------------------------------- 24-25: secuencia de entrega

def test_paridad_caso_24_secuencia_entrega_temprana_al_fondo_genera_violacion() -> None:
    fixture = _cargar("caso_24_secuencia_entrega_temprana_al_fondo_genera_violacion.json")
    cont = contenedor_demo()
    temprano = item("a", 1000, 2450, 2700, 100, rotable=False, secuencia=1)
    tardio = item("b", 1000, 2450, 2700, 100, rotable=False, secuencia=2)
    resultado = ResultadoCubicacion(
        contenedor=cont, rechazos=[],
        colocaciones=[
            Colocacion(item=temprano, x=0, y=0, z=0, orientacion=orientaciones_de(temprano)[0]),
            Colocacion(item=tardio, x=1000, y=0, z=0, orientacion=orientaciones_de(tardio)[0]),
        ],
    )
    incidencias = MotorCubicacion3D().validar(resultado, ParametrosOptimizacion())
    obtenido = {
        "tieneSecuenciaBloqueada": any(i.codigo_regla == "SECUENCIA_BLOQUEADA" for i in incidencias),
        "accesibilidadPrimeraColocacion": resultado.colocaciones[0].accesibilidad,
    }
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], obtenido)


def test_paridad_caso_25_secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta() -> None:
    fixture = _cargar("caso_25_secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta.json")
    cont = contenedor_demo()
    items = [
        item("primera", 2000, 2400, 2000, 500, rotable=False, secuencia=1),
        item("ultima", 2000, 2400, 2000, 500, rotable=False, secuencia=3),
    ]
    r = MotorCubicacion3D().optimizar(items, cont, ParametrosOptimizacion(estrategia=EstrategiaOptimizacion.PRIORIZAR_SECUENCIA_ENTREGA))
    primera = next(c for c in r.colocaciones if c.item.id == "primera")
    ultima = next(c for c in r.colocaciones if c.item.id == "ultima")
    obtenido = {
        "distanciaPrimeraMenorQueUltima": v.distancia_a_puerta(primera, cont) < v.distancia_a_puerta(ultima, cont),
        "tieneSecuenciaBloqueada": any(i.codigo_regla == "SECUENCIA_BLOQUEADA" for i in r.incidencias),
    }
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], obtenido)


# ------------------------------------------------------------- 26: metricas

def test_paridad_caso_26_metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util() -> None:
    fixture = _cargar("caso_26_metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util.json")
    cont = Contenedor(2000, 1000, 1000, 10000, 0, 1000, 1000, PosicionPuerta.TRASERA, [])
    items = [item("a", 1000, 1000, 1000, 100, rotable=False)]
    motor = MotorCubicacion3D()
    r = motor.optimizar(items, cont, ParametrosOptimizacion())
    m = motor.metricas(r, items)
    obtenido = {"porcentajeVolumen": m.porcentaje_volumen, "unidadesCargadas": m.unidades_cargadas, "pesoCargadoKg": m.peso_cargado_kg}
    _assert_paridad(fixture["caso"], fixture["casos"][0]["esperado"], obtenido)


def test_hay_exactamente_26_fixtures_dorados() -> None:
    """Guarda de que nadie borre/agregue un caso sin querer - el criterio de
    cierre de la HU es exactamente 26/26, ni mas ni menos."""
    assert len(list(FIXTURES_DIR.glob("caso_*.json"))) == 26
