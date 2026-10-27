"""El servicio del modo sombra: cuando corre, cuando no, lo que registra y que nunca afecta a quien llama."""

from __future__ import annotations

import asyncio
import json
import time
from datetime import datetime, timedelta, timezone

import pytest
from sqlalchemy import inspect as sa_inspect, select

from application.ia_sombra.servicio import ServicioSombra
from application.ports.ia import IaNoDisponible
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.modelos import IaSombraModel
from infrastructure.persistence.repositorios.ia_sombra_repo import SqlIaSombraRepository
from tests.ia_sombra.ayudas import (
    MEDIDAS, ClienteIaFalso, catalogo, config, json_ia, linea, recomendacion, vehiculo_ia,
)


class _CatalogoFalso:
    async def cargar(self):
        return catalogo()


class _ParametrosFalso:
    async def obtener(self):
        return MEDIDAS


def _servicio(cliente, cfg=None, azar=lambda: 0.0, ahora=lambda: datetime.now(timezone.utc), margen=5.0) -> ServicioSombra:
    return ServicioSombra(
        fabrica_sesion=SessionLocal,
        fabrica_repos=lambda s: (_CatalogoFalso(), _ParametrosFalso(), SqlIaSombraRepository(s)),
        fabrica_cliente=lambda _: cliente,
        config=lambda: cfg or config(), azar=azar, ahora=ahora, margen_timeout=margen)


async def _filas():
    async with SessionLocal() as s:
        return list((await s.execute(select(IaSombraModel).order_by(IaSombraModel.created_at))).scalars())


async def _correr(servicio, motor=None, lineas=None):
    agendada = servicio.programar(motor or recomendacion(("2L2", "FURGON")), lineas or [linea()], None)
    await servicio.esperar_pendientes(10)
    return agendada


# ----------------------------------------------------------------- cuando corre y cuando no


async def test_apagado_no_llama_al_modelo_ni_registra_nada():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]))

    assert await _correr(_servicio(cliente, config(habilitada=False))) is False

    assert cliente.llamadas == [] and await _filas() == []


async def test_sin_llave_no_llama_al_modelo():
    cliente = ClienteIaFalso()

    assert await _correr(_servicio(cliente, config(api_key=None))) is False

    assert cliente.llamadas == []


async def test_el_muestreo_salta_las_recomendaciones_que_no_le_tocan():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]))

    assert await _correr(_servicio(cliente, config(muestreo=0.3), azar=lambda: 0.9)) is False
    assert await _correr(_servicio(cliente, config(muestreo=0.3), azar=lambda: 0.1)) is True

    assert len(cliente.llamadas) == 1


async def test_con_demasiadas_comparaciones_esperando_se_descarta_la_nueva():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]), demora=0.3)
    servicio = _servicio(cliente, config(max_concurrentes=1, max_pendientes=2))

    resultados = [servicio.programar(recomendacion(("2L2", "FURGON"), id=f"r{i}"), [linea()], None) for i in range(4)]
    await servicio.esperar_pendientes(10)

    assert resultados == [True, True, False, False]
    assert len(await _filas()) == 2


async def test_el_tope_de_tokens_del_dia_detiene_las_comparaciones():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]), tokens=(600, 500))
    servicio = _servicio(cliente, config(tope_tokens_dia=1000))

    await _correr(servicio)   # usa 1.100 tokens: alcanza el tope
    await _correr(servicio)   # ya no se llama

    assert len(cliente.llamadas) == 1
    assert len(await _filas()) == 1


async def test_el_tope_cuenta_desde_la_medianoche_de_colombia_y_se_renueva_al_dia_siguiente():
    ahora = datetime.now(timezone.utc)
    async with SessionLocal() as s:  # un registro de ayer que gasto mucho
        from application.ia_sombra.registro import RegistroSombra
        await SqlIaSombraRepository(s).guardar(RegistroSombra(
            "r-ayer", "L", "NEXO", "OK", None, None, "ASIGNADO", 1, (), tokens_entrada=900_000, tokens_salida=900_000))
        fila = (await s.execute(select(IaSombraModel))).scalars().one()
        fila.created_at = ahora - timedelta(days=2)
        await s.commit()
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]))

    await _correr(_servicio(cliente, config(tope_tokens_dia=1000)))

    assert len(cliente.llamadas) == 1, "los tokens de ayer no cuentan para el tope de hoy"


# ----------------------------------------------------------------- lo que registra


async def test_una_comparacion_correcta_deja_la_diferencia_y_el_costo():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia("C2", "FURGON", ((0, 50),))], explicacion="un Sencillo alcanza"), tokens=(1234, 321))

    await _correr(_servicio(cliente, config(modelo="claude-sonnet-5-5", esfuerzo="high")), recomendacion(("2L2", "FURGON"), id="rec-77"))

    (f,) = await _filas()
    assert (f.estado, f.id_recomendacion, f.origen_sistema, f.modelo, f.esfuerzo) == ("OK", "rec-77", "NEXO", "modelo-falso", "high")
    assert (f.resultado_motor, f.resultado_ia, f.cantidad_motor, f.cantidad_ia) == ("ASIGNADO", "ASIGNADO", 1, 1)
    assert (json.loads(f.flota_motor), json.loads(f.flota_ia)) == (["2L2/FURGON"], ["C2/FURGON"])
    assert (f.coincide_resultado, f.coincide_flota, f.coincide_configuracion) == (True, False, False)
    assert (f.ia_respeta_peso, f.ia_respeta_volumen, f.ia_conserva_unidades) == (True, True, True)
    assert (f.tokens_entrada, f.tokens_salida, f.latencia_ia_ms, f.explicacion) == (1234, 321, 12, "un Sencillo alcanza")
    assert f.violaciones is None


async def test_el_registro_no_guarda_lo_que_se_envio_al_modelo():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]))

    await _correr(_servicio(cliente), lineas=[linea("BEBIDAS", 50, 50, 40, 40, 20)])

    (f,) = await _filas()
    todo = " ".join(str(getattr(f, a.key)) for a in sa_inspect(IaSombraModel).column_attrs)
    assert "BEBIDAS" not in todo and '"largoCm"' not in todo


async def test_el_modelo_recibe_el_problema_y_no_la_respuesta_del_motor():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]))

    await _correr(_servicio(cliente), recomendacion(("C2", "FURGON"), id="rec-secreta"))

    instrucciones, mensaje, esquema = cliente.llamadas[0]
    assert "rec-secreta" not in mensaje and "LINEA-1" not in mensaje
    assert json.loads(mensaje)["mercancias"][0]["codigo"] == "BEBIDAS"
    assert esquema["additionalProperties"] is False


@pytest.mark.parametrize("error,estado", [
    (IaNoDisponible("IA_LIMITE_PROVEEDOR", "x"), "IA_LIMITE_PROVEEDOR"),
    (IaNoDisponible("IA_CREDENCIAL_INVALIDA", "x"), "IA_CREDENCIAL_INVALIDA"),
    (IaNoDisponible("IA_RECHAZADA", "x"), "IA_RECHAZADA"),
])
async def test_si_el_proveedor_falla_queda_el_intento_con_su_codigo_y_sin_comparacion(error, estado):
    await _correr(_servicio(ClienteIaFalso(error)))

    (f,) = await _filas()
    assert (f.estado, f.resultado_ia, f.coincide_flota, f.tokens_entrada) == (estado, None, None, 0)
    assert f.resultado_motor == "ASIGNADO", "lo que recomendo el motor queda igual"


async def test_una_respuesta_que_no_cumple_el_contrato_se_registra_como_invalida_con_sus_tokens():
    await _correr(_servicio(ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia(codigo="C2", carga=((9, 5),))]), tokens=(70, 30))))

    (f,) = await _filas()
    assert (f.estado, f.resultado_ia, f.tokens_entrada, f.tokens_salida) == ("IA_RESPUESTA_INVALIDA", None, 70, 30)


async def test_un_modelo_que_no_responde_a_tiempo_queda_como_tiempo_agotado():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]), demora=30)

    await _correr(_servicio(cliente, config(timeout_segundos=0.05), margen=0))

    (f,) = await _filas()
    assert (f.estado, f.resultado_ia, f.resultado_motor) == ("IA_TIEMPO_AGOTADO", None, "ASIGNADO")


async def test_las_violaciones_del_modelo_quedan_registradas():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia("2L2", "FURGON", ((0, 250),))]))

    await _correr(_servicio(cliente), lineas=[linea(cantidad=250)])

    (f,) = await _filas()
    assert (f.ia_respeta_peso, f.ia_respeta_volumen) == (False, True)
    assert "5000 kg" in json.loads(f.violaciones)[0]


# ----------------------------------------------------------------- nunca afecta a quien llama


async def test_programar_regresa_de_inmediato_aunque_el_modelo_tarde():
    cliente = ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]), demora=2.0)
    servicio = _servicio(cliente)

    inicio = time.perf_counter()
    agendada = servicio.programar(recomendacion(("2L2", "FURGON")), [linea()], None)
    espera = time.perf_counter() - inicio

    assert agendada is True and espera < 0.05
    await servicio.cerrar(timeout=0.01)


async def test_un_error_inesperado_dentro_de_la_comparacion_no_sale_hacia_quien_llama():
    class _Roto(ClienteIaFalso):
        async def completar(self, *a):
            raise RuntimeError("fallo interno")

    servicio = _servicio(_Roto())

    assert servicio.programar(recomendacion(("2L2", "FURGON")), [linea()], None) is True
    await servicio.esperar_pendientes(10)   # no levanta
    assert servicio.pendientes == 0


async def test_un_error_al_agendar_tampoco_sale_hacia_quien_llama():
    def _configuracion_rota():
        raise RuntimeError("entorno ilegible")

    servicio = ServicioSombra(SessionLocal, lambda s: (), lambda c: ClienteIaFalso(), config=_configuracion_rota)

    assert servicio.programar(recomendacion(("2L2", "FURGON")), [linea()], None) is False


async def test_cerrar_espera_las_comparaciones_en_curso_y_cancela_las_que_no_terminan():
    servicio = _servicio(ClienteIaFalso(json_ia(vehiculos=[vehiculo_ia()]), demora=30))
    servicio.programar(recomendacion(("2L2", "FURGON")), [linea()], None)

    await servicio.cerrar(timeout=0.05)
    await asyncio.sleep(0.05)

    assert servicio.pendientes == 0
