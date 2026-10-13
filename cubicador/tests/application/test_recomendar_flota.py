"""S5-HU-17.3: el motor leyendo el catalogo DE BASE, no uno quemado.

Las pruebas de `tests/domain/test_seleccion_flota.py` fijan el comportamiento
del algoritmo con un catalogo en memoria. Estas cierran el circuito completo -
adaptador de SQLAlchemy, puerto, caso de uso y motor - contra SQLite, como el
resto de la suite.

Reusan el catalogo sembrado de `tests/infrastructure/test_catalogo_repo.py`,
que es un recorte del real: el override `3S3 + FURGON = 30.000 kg`, una
mercancia cuya carroceria principal no tiene vehiculos, y una regla de
incompatibilidad.
"""

from __future__ import annotations

from decimal import Decimal as D

import pytest
from sqlalchemy.ext.asyncio import AsyncSession

from application.excepciones import ValidationFailedException
from application.recomendaciones.casos_de_uso import LineaMercancia, recomendar_flota
from domain.entidades.entidades import ParametrosCubicacion
from domain.motor import seleccion_flota as sf
from infrastructure.persistence.repositorios.catalogo_repo import SqlCatalogoRepository

# El fixture del catalogo vive alla; importarlo aca lo hace visible a pytest.
from tests.infrastructure.test_catalogo_repo import catalogo_sembrado, sesion  # noqa: F401


class _ParametrosFalsos:
    """Medidas estandar en memoria: la tabla real es un singleton de 4 campos
    y no aporta nada al caso de prueba."""

    def __init__(self, **ajustes):
        self._valor = ParametrosCubicacion(**ajustes)

    async def obtener(self) -> ParametrosCubicacion:
        return self._valor

    async def guardar(self, parametros: ParametrosCubicacion) -> ParametrosCubicacion:
        self._valor = parametros
        return parametros


# El fixture siembra los ids a mano, asi que el id del tipo es predecible: el
# contrato manda el ID del catalogo, no el codigo (acuerdo con Cotifly del
# 2026-10-08). `m-beb` es BEBIDAS, `m-vid` VIDRIO_PLANO, `m-fer` FERTILIZANTES.
IDS = {"BEBIDAS": "m-beb", "VIDRIO_PLANO": "m-vid", "FERTILIZANTES": "m-fer"}


def _linea(codigo, cantidad, largo, ancho, alto, peso):
    return LineaMercancia(IDS.get(codigo, codigo), cantidad, D(largo), D(ancho), D(alto), D(peso))


async def _recomendar(sesion, lineas, clase=None, parametros=None):
    return await recomendar_flota(
        SqlCatalogoRepository(sesion), parametros or _ParametrosFalsos(), lineas, clase)


# --- el circuito completo ---------------------------------------------------


@pytest.mark.asyncio
async def test_el_motor_resuelve_leyendo_el_catalogo_de_base(catalogo_sembrado: AsyncSession):
    r = await _recomendar(catalogo_sembrado, [_linea("BEBIDAS", 100, 60, 40, 40, 25)])
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert r.vehiculos[0].codigo_vehiculo == "3S3"
    assert r.vehiculos[0].codigo_carroceria == "FURGON"


@pytest.mark.asyncio
async def test_el_porcentaje_de_peso_sale_del_override_de_la_combinacion(
    catalogo_sembrado: AsyncSession,
):
    """15.000 kg son 50% de los 30.000 de `3S3 + FURGON`, no 44,1% de los
    34.000 del chasis. Si el adaptador no resolviera el override, el motor
    creeria que le caben 4 toneladas de mas."""
    r = await _recomendar(catalogo_sembrado, [_linea("BEBIDAS", 100, 60, 40, 40, 150)])
    assert r.vehiculos[0].peso_total_kg == D("15000.00")
    assert r.vehiculos[0].porcentaje_llenado_peso == D("50.0")


@pytest.mark.asyncio
async def test_la_carroceria_principal_sin_vehiculos_cae_a_la_secundaria(
    catalogo_sembrado: AsyncSession,
):
    """`VIDRIO_PLANO` tiene `PORTAVIDRIOS` como principal, que esta sembrada
    sin ningun vehiculo. Sin el respaldo se quedaria sin recomendacion."""
    r = await _recomendar(catalogo_sembrado, [_linea("VIDRIO_PLANO", 10, 100, 80, 60, 200)])
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert r.vehiculos[0].codigo_carroceria == "PLATAFORMA"
    assert r.condicionada is True
    assert "VIDRIO_PLANO" in r.mercancias_condicionadas


@pytest.mark.asyncio
async def test_las_reglas_de_incompatibilidad_de_base_separan_la_carga(
    catalogo_sembrado: AsyncSession,
):
    r = await _recomendar(catalogo_sembrado, [
        _linea("FERTILIZANTES", 20, 80, 50, 30, 50),
        _linea("BEBIDAS", 20, 60, 40, 40, 25),
    ])
    assert r.resultado == sf.RESULTADO_ASIGNADO
    assert len(r.vehiculos) == 2
    for vehiculo in r.vehiculos:
        codigos = {m.codigo_mercancia for m in vehiculo.mercancias}
        assert not {"FERTILIZANTES", "BEBIDAS"} <= codigos


@pytest.mark.asyncio
async def test_las_medidas_estandar_salen_de_su_tabla(catalogo_sembrado: AsyncSession):
    """Bajar el tope de largo convierte en extradimensionada una carga que
    antes pasaba: confirma que el valor se lee y no esta quemado."""
    normal = await _recomendar(catalogo_sembrado, [_linea("BEBIDAS", 2, 400, 40, 40, 25)])
    assert normal.resultado == sf.RESULTADO_ASIGNADO

    recortado = _ParametrosFalsos(largo_max_cm=300.0)
    estrecho = await _recomendar(
        catalogo_sembrado, [_linea("BEBIDAS", 2, 400, 40, 40, 25)], parametros=recortado)
    assert estrecho.resultado == sf.RESULTADO_SIN_SELECCION
    assert estrecho.motivo_sin_seleccion == sf.MOTIVO_EXTRADIMENSIONADO


# --- errores del llamante (nunca un default silencioso) ---------------------


@pytest.mark.asyncio
async def test_un_codigo_que_no_esta_en_el_catalogo_es_error(catalogo_sembrado: AsyncSession):
    with pytest.raises(ValidationFailedException) as error:
        await _recomendar(catalogo_sembrado, [_linea("NO_EXISTE", 1, 60, 40, 40, 25)])
    assert error.value.codigo == "MERCANCIA_DESCONOCIDA"


@pytest.mark.asyncio
async def test_sin_mercancias_es_error(catalogo_sembrado: AsyncSession):
    with pytest.raises(ValidationFailedException) as error:
        await _recomendar(catalogo_sembrado, [])
    assert error.value.codigo == "MERCANCIAS_REQUERIDAS"


@pytest.mark.asyncio
async def test_una_medida_en_cero_es_error(catalogo_sembrado: AsyncSession):
    with pytest.raises(ValidationFailedException) as error:
        await _recomendar(catalogo_sembrado, [_linea("BEBIDAS", 1, 60, 0, 40, 25)])
    assert error.value.codigo == "DIMENSIONES_INVALIDAS"


@pytest.mark.asyncio
async def test_una_carga_desmesurada_se_rechaza_antes_de_expandirla(
    catalogo_sembrado: AsyncSession,
):
    """El tope es proteccion anti-DoS: expandir cantidades a unidades fisicas
    es lo que cuesta, asi que se corta antes de tocar el catalogo."""
    with pytest.raises(ValidationFailedException) as error:
        await _recomendar(catalogo_sembrado, [_linea("BEBIDAS", 50_000, 60, 40, 40, 25)])
    assert error.value.codigo == "CUBICACION_DEMASIADO_GRANDE"
