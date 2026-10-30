"""Las metricas del estudio comparativo y el dataset semilla (S6-HU-35.1).

`evaluacion-ia/cubicador` no es parte del servicio ni del CI (se corre bajo demanda), pero lo que decide el
go/no-go de la IA se apoya en estas cuentas: si una metrica esta mal calculada,
la decision tambien. Estas pruebas fijan las cuentas y que el dataset semilla sea
coherente; no fijan el resultado del motor, que es lo que el informe mide.
"""

from __future__ import annotations

import sys
from decimal import Decimal as D
from pathlib import Path

import pytest

HERRAMIENTA = Path(__file__).resolve().parents[1]
CUBICADOR = HERRAMIENTA.parents[1] / "cubicador"
sys.path.insert(0, str(CUBICADOR))
sys.path.insert(0, str(HERRAMIENTA))

import metricas as m  # noqa: E402
from catalogo_qa import catalogo_qa  # noqa: E402
from evaluar import adaptador_motor, correr, leer_caso, leer_casos  # noqa: E402
from importar_cargas_reales import importar  # noqa: E402


def _caso(id="c", flotas=(), minima=None, esperado=m.ASIGNADO, motivo=None, mercancias=None):
    flotas = tuple(tuple(sorted(f)) for f in flotas)
    return m.Caso(
        id=id, origen="sintetico", etiqueta_validada=True, descripcion="", clase_viaje=None,
        mercancias=tuple(mercancias or [m.Posicion("BEBIDAS", 10, D(50), D(40), D(40), D(20))]),
        resultado_esperado=esperado, motivo_esperado=motivo,
        cantidad_minima=minima if minima is not None else (len(flotas[0]) if flotas else 0),
        flotas_correctas=flotas,
    )


def _resp(*vehiculos, resultado=m.ASIGNADO, motivo=None, ms=1.0, entrada=0, salida=0):
    return m.Respuesta(
        resultado, motivo,
        tuple(m.VehiculoPropuesto(v, c, tuple(carga)) for v, c, carga in vehiculos), ms, entrada, salida)


def _evaluacion(**por_caso):
    return m.Evaluacion({k: v if isinstance(v, list) else [v] for k, v in por_caso.items()})


# ----------------------------------------------------------------- percentil


def test_el_percentil_es_por_rango_mas_cercano():
    muestras = list(range(1, 101))
    assert m.percentil(muestras, 0.95) == 95
    assert m.percentil(muestras, 0.50) == 50
    assert m.percentil([7], 0.95) == 7
    assert m.percentil([], 0.95) is None


def test_el_p95_de_pocas_muestras_es_el_maximo():
    assert m.percentil([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], 0.95) == 10


# ----------------------------------------------------------------- acierto del vehiculo


def test_acierto_de_vehiculo_acepta_cualquiera_de_las_flotas_correctas():
    caso = _caso(flotas=[[("2S2", "FURGON")], [("3S3", "FURGON")]])
    for propuesto in ("2S2", "3S3"):
        ev = _evaluacion(c=_resp((propuesto, "FURGON", [(0, 10)])))
        assert m.acierto_vehiculo([caso], ev)["flota_exacta"]["aciertos"] == 1


def test_otra_carroceria_no_es_flota_exacta_pero_si_la_misma_configuracion():
    caso = _caso(flotas=[[("2L2", "FURGON")]])
    ev = _evaluacion(c=_resp(("2L2", "PLATAFORMA", [(0, 10)])))
    r = m.acierto_vehiculo([caso], ev)
    assert r["flota_exacta"]["aciertos"] == 0
    assert r["solo_configuracion"]["aciertos"] == 1


def test_el_orden_de_la_flota_no_importa():
    caso = _caso(flotas=[[("2S2", "FURGON"), ("C2", "FURGON")]])
    ev = _evaluacion(c=_resp(("C2", "FURGON", [(0, 3)]), ("2S2", "FURGON", [(0, 7)])))
    assert m.acierto_vehiculo([caso], ev)["flota_exacta"]["aciertos"] == 1


def test_no_asignar_donde_se_debia_asignar_es_un_fallo_y_no_una_excepcion():
    caso = _caso(flotas=[[("2L2", "FURGON")]])
    ev = _evaluacion(c=_resp(resultado=m.SIN_SELECCION, motivo="SUPERA_K_MAX"))
    r = m.acierto_vehiculo([caso], ev)
    assert r["flota_exacta"] == {"aciertos": 0, "total": 1, "tasa": 0.0}


def test_un_caso_sin_flotas_listadas_no_entra_al_acierto_del_vehiculo():
    caso = _caso(flotas=[], minima=2)
    ev = _evaluacion(c=_resp(("2S2", "FURGON", [(0, 10)])))
    assert m.acierto_vehiculo([caso], ev)["flota_exacta"]["total"] == 0


def test_acierto_de_decision_y_de_motivo():
    asignar = _caso("a", flotas=[[("2L2", "FURGON")]])
    extra = _caso("e", esperado=m.SIN_SELECCION, motivo="EXTRADIMENSIONADO")
    ev = _evaluacion(
        a=_resp(("2L2", "FURGON", [(0, 10)])),
        e=_resp(resultado=m.SIN_SELECCION, motivo="SUPERA_K_MAX"))
    r = m.acierto_decision([asignar, extra], ev)
    assert r["resultado"]["aciertos"] == 2
    assert r["motivo_cuando_no_asigna"]["aciertos"] == 0


# ----------------------------------------------------------------- flota minima


def test_flota_minima_separa_exceso_de_defecto():
    casos = [_caso(f"c{i}", minima=2) for i in range(3)]
    ev = _evaluacion(
        c0=_resp(("2S2", "FURGON", [(0, 5)]), ("2S2", "FURGON", [(0, 5)])),
        c1=_resp(("2S2", "FURGON", [(0, 3)]), ("2S2", "FURGON", [(0, 3)]), ("2S2", "FURGON", [(0, 4)])),
        c2=_resp(("2S2", "FURGON", [(0, 10)])))
    r = m.flota_minima(casos, ev)
    assert r["cantidad_exacta"] == {"aciertos": 1, "total": 3, "tasa": 0.3333}
    assert r["con_exceso"] == [{"caso": "c1", "de_mas": 1}]
    assert r["con_defecto"] == [{"caso": "c2", "de_menos": 1}]
    assert r["exceso_medio"] == 0.0


# ----------------------------------------------------------------- respeto de capacidad


def test_respeta_capacidad_con_una_flota_bien_cargada():
    caso = _caso()  # 10 bultos de 20 kg y 0,08 m3
    ev = _evaluacion(c=_resp(("2L2", "FURGON", [(0, 10)])))
    r = m.respeto_de_capacidad([caso], ev, catalogo_qa())
    assert (r["peso"]["tasa"], r["volumen"]["tasa"], r["unidades_conservadas"]["tasa"]) == (1.0, 1.0, 1.0)
    assert r["violaciones"] == []


def test_detecta_el_exceso_de_peso_aunque_el_candidato_diga_que_cabe():
    caso = _caso(mercancias=[m.Posicion("BEBIDAS", 1, D(50), D(40), D(40), D(5000))])
    ev = _evaluacion(c=_resp(("2L2", "FURGON", [(0, 1)])))  # el Turbo carga 4.000 kg
    r = m.respeto_de_capacidad([caso], ev, catalogo_qa())
    assert r["peso"]["aciertos"] == 0
    assert "supera la capacidad" in r["violaciones"][0]["detalle"]


def test_usa_la_capacidad_de_la_combinacion_y_no_la_del_chasis():
    # 3S3 + FURGON son 30.000 kg aunque el chasis aguante 34.000.
    caso = _caso(mercancias=[m.Posicion("BEBIDAS", 1, D(50), D(40), D(40), D(32000))])
    assert m.respeto_de_capacidad([caso], _evaluacion(c=_resp(("3S3", "FURGON", [(0, 1)]))), catalogo_qa())["peso"]["aciertos"] == 0
    assert m.respeto_de_capacidad([caso], _evaluacion(c=_resp(("3S3", "PLATAFORMA", [(0, 1)]))), catalogo_qa())["peso"]["aciertos"] == 1


def test_detecta_el_exceso_de_volumen_con_el_volumen_fisico_del_vehiculo():
    # 200 bultos de 0,2 m3 = 40 m3; el Turbo mide 22,77 m3.
    caso = _caso(mercancias=[m.Posicion("BEBIDAS", 200, D(100), D(50), D(40), D(1))])
    ev = _evaluacion(c=_resp(("2L2", "FURGON", [(0, 200)])))
    r = m.respeto_de_capacidad([caso], ev, catalogo_qa())
    assert r["volumen"]["aciertos"] == 0
    assert r["peso"]["aciertos"] == 1


def test_detecta_unidades_perdidas_y_repetidas():
    caso = _caso()
    perdidas = _evaluacion(c=_resp(("2L2", "FURGON", [(0, 9)])))
    repetidas = _evaluacion(c=_resp(("2L2", "FURGON", [(0, 10)]), ("C2", "FURGON", [(0, 1)])))
    assert m.respeto_de_capacidad([caso], perdidas, catalogo_qa())["unidades_conservadas"]["aciertos"] == 0
    assert m.respeto_de_capacidad([caso], repetidas, catalogo_qa())["unidades_conservadas"]["aciertos"] == 0


def test_un_vehiculo_inexistente_cuenta_como_violacion():
    caso = _caso()
    r = m.respeto_de_capacidad([caso], _evaluacion(c=_resp(("XX", "FURGON", [(0, 10)]))), catalogo_qa())
    assert r["peso"]["aciertos"] == 0 and r["volumen"]["aciertos"] == 0


# ----------------------------------------------------------------- latencia y costo


def test_la_latencia_junta_las_muestras_de_todas_las_repeticiones():
    casos = [_caso("a"), _caso("b")]
    ev = _evaluacion(a=[_resp(ms=1), _resp(ms=3)], b=[_resp(ms=2), _resp(ms=100)])
    assert m.latencia(casos, ev) == {"muestras": 4, "p50_ms": 2, "p95_ms": 100, "max_ms": 100}


def test_el_costo_por_cubicacion_sale_de_los_tokens_y_de_los_precios():
    caso = _caso()
    ev = _evaluacion(c=[_resp(entrada=2000, salida=500), _resp(entrada=4000, salida=1500)])
    r = m.costo_por_cubicacion([caso], ev, precio_entrada_usd_por_millon=3, precio_salida_usd_por_millon=15)
    assert r["tokens_entrada_medio"] == 3000 and r["tokens_salida_medio"] == 1000
    assert r["usd_por_cubicacion"] == pytest.approx((3000 * 3 + 1000 * 15) / 1_000_000)


def test_sin_precios_el_costo_entrega_solo_los_tokens():
    r = m.costo_por_cubicacion([_caso()], _evaluacion(c=_resp(entrada=10, salida=5)), None, None)
    assert "usd_por_cubicacion" not in r


# ----------------------------------------------------------------- dataset semilla


@pytest.fixture(scope="module")
def semilla():
    casos, _ = leer_casos(HERRAMIENTA / "casos")
    return casos


def test_la_carpeta_de_casos_ignora_los_datasets_que_no_son_de_recomendacion():
    """`casos/` tambien guarda el dataset de interpretacion de texto libre, que no trae
    decision correcta: leer la carpeta no puede fallar por el ni mezclar sus casos."""
    assert (HERRAMIENTA / "casos" / "interpretacion_sintetica.json").exists()
    casos, _ = leer_casos(HERRAMIENTA / "casos")
    solo_semilla, _ = leer_casos(HERRAMIENTA / "casos" / "semilla_sintetica.json")
    assert [c.id for c in casos] == [c.id for c in solo_semilla]


def test_el_dataset_semilla_carga_y_tiene_casos_de_cada_tipo(semilla):
    assert len(semilla) >= 20
    assert any(c.resultado_esperado == m.SIN_SELECCION for c in semilla)
    assert any(c.cantidad_minima >= 2 for c in semilla)


def test_cada_flota_etiquetada_es_fisicamente_posible(semilla):
    """Cota necesaria de cada etiqueta: el peso y el volumen totales caben en la flota que se da por buena."""
    catalogo = catalogo_qa()
    for caso in semilla:
        peso = sum(p.peso_kg * p.cantidad for p in caso.mercancias)
        volumen = sum(p.volumen_unitario_m3 * p.cantidad for p in caso.mercancias)
        for flota in caso.flotas_correctas:
            vehiculos = [(catalogo.vehiculo_por_codigo(v), c) for v, c in flota]
            assert all(v is not None and v.capacidad_con(c) is not None for v, c in vehiculos), caso.id
            assert peso <= sum(v.capacidad_con(c) for v, c in vehiculos), f"{caso.id}: la flota etiquetada no aguanta el peso"
            assert volumen <= sum(v.volumen_m3 for v, _ in vehiculos), f"{caso.id}: la flota etiquetada no tiene el volumen"


def test_la_flota_etiquetada_no_tiene_vehiculos_de_menos_que_la_cota_de_peso(semilla):
    """Ninguna etiqueta pide MENOS vehiculos de los que el peso total exige: la cantidad minima es al menos la cota de peso."""
    catalogo = catalogo_qa()
    capacidad_maxima = max(c.capacidad_kg for v in catalogo.vehiculos for c in v.carrocerias)
    for caso in semilla:
        if caso.resultado_esperado != m.ASIGNADO:
            continue
        peso = sum(p.peso_kg * p.cantidad for p in caso.mercancias)
        cota = -(-peso // capacidad_maxima)
        assert caso.cantidad_minima >= cota, f"{caso.id}: pide {caso.cantidad_minima} y el peso exige al menos {cota}"


def test_el_motor_respeta_peso_volumen_y_unidades_en_todo_el_dataset(semilla):
    """Propiedad de seguridad, no de acierto: lo que el motor asigna siempre se puede cargar."""
    catalogo = catalogo_qa()
    evaluacion = correr(semilla, adaptador_motor(catalogo), repeticiones=1)
    r = m.respeto_de_capacidad(semilla, evaluacion, catalogo)
    assert r["violaciones"] == []


# ----------------------------------------------------------------- importar cargas reales

CSV = """CargaId,NumeroLinea,ClaseViaje,VehiculoElegido,CarroceriaElegida,UsoRecomendacion,CodigoTipoMercancia,Cantidad,LargoCm,AnchoCm,AltoCm,PesoKg
-------,-----------,----------,---------------,-----------------,----------------,-------------------,--------,-------,-------,------,------
AAA,1,Carga Suelta,3S3,Plataforma,1,CAJAS_CARTON,10,120.00,100.00,150.00,400.00
AAA,2,Carga Suelta,C2,Furgón,0,BEBIDAS,3,50.00,40.00,40.00,20.00
BBB,1,,2S3,Tanque,0,CAJAS_CARTON,5,60.00,40.00,50.00,8.00
"""


def test_las_filas_de_una_misma_carga_se_juntan_y_cada_linea_es_un_vehiculo():
    casos = {c["id"]: c for c in importar(CSV)}
    carga = casos["R-AAA"]
    assert len(carga["mercancias"]) == 2
    assert carga["decisionCorrecta"]["cantidadMinima"] == 2
    assert carga["decisionCorrecta"]["flotas"] == [[
        {"vehiculo": "3S3", "carroceria": "PLATAFORMA"}, {"vehiculo": "C2", "carroceria": "FURGON"}]]
    assert carga["usoRecomendacion"] is True


def test_toda_carga_importada_nace_sin_validar_y_el_evaluador_la_ignora(tmp_path):
    casos = importar(CSV)
    assert all(c["etiquetaValidada"] is False and c["origen"] == "real" for c in casos)
    archivo = tmp_path / "reales.json"
    import json
    archivo.write_text(json.dumps({"version": "1", "casos": casos}), encoding="utf-8")
    assert leer_casos(archivo)[0] == []
    assert len(leer_casos(archivo, incluir_sin_validar=True)[0]) == 2


def test_un_vehiculo_que_el_cubicador_no_tiene_queda_con_observacion():
    casos = {c["id"]: c for c in importar(CSV)}
    assert casos["R-AAA"]["observaciones"] == []
    assert "2S3" in casos["R-BBB"]["observaciones"][0]


def test_el_caso_importado_se_puede_leer_con_el_evaluador():
    caso = leer_caso(importar(CSV)[0])
    assert caso.cantidad_minima == 2 and caso.origen == "real"
