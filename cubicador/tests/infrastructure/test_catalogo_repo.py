"""S5-HU-17.2: el adaptador de catalogo y las reglas de sus entidades.

Las pruebas corren contra SQLite en memoria, como el resto de la suite. Los
datos imitan lo que hay en `dbo_cubicador`: el override de capacidad del
`3S3 + FURGON`, una mercancia con carroceria secundaria, y una regla de
incompatibilidad real.
"""

from __future__ import annotations

import os
import sys
from decimal import Decimal

import pytest
import pytest_asyncio
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.pool import StaticPool

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
if RAIZ not in sys.path:
    sys.path.insert(0, RAIZ)

from domain.entidades.catalogo import (  # noqa: E402
    Carroceria,
    CarroceriaDeVehiculo,
    Catalogo,
    CategoriaMercancia,
    ParametrosMotor,
    ReglaIncompatibilidad,
    TipoMercancia,
    Vehiculo,
)
from infrastructure.persistence import modelos_catalogo as m  # noqa: E402
from infrastructure.persistence.repositorios.catalogo_repo import (  # noqa: E402
    SqlCatalogoRepository,
)

AHORA = "2026-10-09 12:00:00+00:00"


@pytest_asyncio.fixture
async def sesion() -> AsyncSession:
    engine = create_async_engine(
        "sqlite+aiosqlite:///:memory:", poolclass=StaticPool,
        connect_args={"check_same_thread": False})
    async with engine.begin() as conn:
        await conn.run_sync(m.BaseCatalogo.metadata.create_all)
    async with async_sessionmaker(engine, expire_on_commit=False)() as s:
        yield s
    await engine.dispose()


def _fechas() -> dict:
    from datetime import datetime, timezone
    ahora = datetime.now(timezone.utc)
    return {"creado_por": "prueba", "created_at": ahora, "updated_at": ahora}


@pytest_asyncio.fixture
async def catalogo_sembrado(sesion: AsyncSession) -> AsyncSession:
    """Un recorte del catalogo real, con los casos que importan."""
    f = _fechas()
    furgon = m.TipoCarroceria(id="c-furgon", codigo="FURGON", nombre="Furgon", activo=True, **f)
    plataforma = m.TipoCarroceria(id="c-plat", codigo="PLATAFORMA", nombre="Plataforma",
                                  activo=True, **f)
    # Sembrada sin ningun vehiculo, como CAMA_BAJA y PORTACONTENEDORES.
    portavidrios = m.TipoCarroceria(id="c-vidrio", codigo="PORTAVIDRIOS",
                                    nombre="Estructura portavidrios", activo=True, **f)
    sesion.add_all([furgon, plataforma, portavidrios])

    tractomula = m.Vehiculo(
        id="v-3s3", codigo="3S3", nombre="Tractomula 3S3",
        largo_cm=Decimal("1220"), ancho_cm=Decimal("240"), alto_util_cm=Decimal("260"),
        capacidad_kg=Decimal("34000"), activo=True, **f)
    sesion.add(tractomula)

    # El unico override real del catalogo: el furgon pesa, asi que la
    # combinacion carga 30 toneladas y no las 34 del chasis.
    sesion.add_all([
        m.RelacionVehiculoCarroceria(
            id="r-1", vehiculo_id="v-3s3", carroceria_id="c-furgon",
            usa_capacidad_del_vehiculo=False,
            capacidad_especifica_kg=Decimal("30000"), **f),
        m.RelacionVehiculoCarroceria(
            id="r-2", vehiculo_id="v-3s3", carroceria_id="c-plat",
            usa_capacidad_del_vehiculo=True,
            capacidad_especifica_kg=Decimal("0"), **f),
    ])

    alimentos = m.CategoriaMercancia(id="cat-ali", nombre="ALIMENTOS", **f)
    fertilizante = m.CategoriaMercancia(id="cat-fer", nombre="FERTILIZANTE", **f)
    sesion.add_all([alimentos, fertilizante])

    sesion.add_all([
        m.TipoMercancia(
            id="m-beb", codigo="BEBIDAS", nombre="Bebidas", categoria_id="cat-ali",
            cubicable=True, condicionada=False, comentario_condicion=None,
            carroceria_principal_id="c-furgon", carroceria_secundaria_id=None,
            secundaria_condicionada=False, control_ambiental="NINGUNO",
            apilable=True, fragil=False, rotacion_libre=True, activo=True, **f),
        # Su carroceria principal no tiene vehiculos: se salva por la secundaria,
        # igual que VIDRIO_PLANO en el catalogo real.
        m.TipoMercancia(
            id="m-vid", codigo="VIDRIO_PLANO", nombre="Vidrio plano", categoria_id="cat-ali",
            cubicable=True, condicionada=True, comentario_condicion="Manejo especial",
            carroceria_principal_id="c-vidrio", carroceria_secundaria_id="c-plat",
            secundaria_condicionada=True, control_ambiental="NINGUNO",
            apilable=False, fragil=True, rotacion_libre=False, activo=True, **f),
        m.TipoMercancia(
            id="m-fer", codigo="FERTILIZANTES", nombre="Fertilizantes",
            categoria_id="cat-fer", cubicable=True, condicionada=False,
            comentario_condicion=None, carroceria_principal_id="c-furgon",
            carroceria_secundaria_id=None, secundaria_condicionada=False,
            control_ambiental="NINGUNO", apilable=True, fragil=False,
            rotacion_libre=True, activo=True, **f),
    ])

    sesion.add(m.ReglaIncompatibilidad(
        id="rg-1", categoria_1_id="cat-fer", categoria_2_id="cat-ali",
        motivo="Contaminacion cruzada", **f))
    sesion.add(m.ParametroMotor(
        id="p-1", clave="k_max", valor="10", descripcion="Tope de vehiculos", **f))

    await sesion.commit()
    return sesion


# --- El adaptador ----------------------------------------------------------


@pytest.mark.asyncio
async def test_carga_el_catalogo_completo(catalogo_sembrado: AsyncSession):
    cat = await SqlCatalogoRepository(catalogo_sembrado).cargar()
    assert len(cat.vehiculos) == 1
    assert len(cat.carrocerias) == 3
    assert len(cat.mercancias) == 3
    assert len(cat.reglas) == 1
    assert cat.parametros.k_max == 10


@pytest.mark.asyncio
async def test_el_override_de_capacidad_se_resuelve_al_cargar(catalogo_sembrado: AsyncSession):
    """`3S3 + FURGON` carga 30t y no las 34 del chasis. Si el adaptador no
    resolviera esto, el motor creeria que caben 4 toneladas de mas."""
    cat = await SqlCatalogoRepository(catalogo_sembrado).cargar()
    tractomula = cat.vehiculo_por_codigo("3S3")
    assert tractomula.capacidad_con("FURGON") == Decimal("30000")
    assert tractomula.capacidad_con("PLATAFORMA") == Decimal("34000")


@pytest.mark.asyncio
async def test_una_carroceria_sin_vehiculos_no_desaparece(catalogo_sembrado: AsyncSession):
    """`PORTAVIDRIOS` existe en el catalogo aunque ningun vehiculo la admita:
    es asi a proposito hasta que el Administrador de Fletes la empareje."""
    cat = await SqlCatalogoRepository(catalogo_sembrado).cargar()
    assert any(c.codigo == "PORTAVIDRIOS" for c in cat.carrocerias)
    assert not cat.vehiculo_por_codigo("3S3").admite("PORTAVIDRIOS")


@pytest.mark.asyncio
async def test_la_mercancia_trae_sus_dos_carrocerias(catalogo_sembrado: AsyncSession):
    """Sin la secundaria, el vidrio plano no tendria ningun vehiculo."""
    cat = await SqlCatalogoRepository(catalogo_sembrado).cargar()
    vidrio = cat.mercancia_por_codigo("VIDRIO_PLANO")
    assert vidrio.carroceria_principal.codigo == "PORTAVIDRIOS"
    assert vidrio.carroceria_secundaria.codigo == "PLATAFORMA"
    assert vidrio.fragil and not vidrio.rotacion_libre


@pytest.mark.asyncio
async def test_el_adaptador_no_escribe_nada(catalogo_sembrado: AsyncSession):
    """Leer el catalogo dos veces no siembra, no corrige y no crea filas que
    falten: eso se resuelve del lado del equipo de Datos."""
    repo = SqlCatalogoRepository(catalogo_sembrado)
    antes = await repo.cargar()
    await repo.cargar()
    despues = await repo.cargar()
    assert len(antes.vehiculos) == len(despues.vehiculos) == 1
    assert len(antes.mercancias) == len(despues.mercancias) == 3


# --- Las reglas de las entidades -------------------------------------------


def _vehiculo(**extra) -> Vehiculo:
    base = dict(
        id="v", codigo="3S3", nombre="Tractomula",
        largo_cm=Decimal("1220"), ancho_cm=Decimal("240"),
        alto_util_cm=Decimal("260"), capacidad_kg=Decimal("34000"))
    base.update(extra)
    return Vehiculo(**base)


def test_el_volumen_sale_de_las_tres_medidas_en_metros_cubicos():
    # 12,20 x 2,40 x 2,60 m
    assert _vehiculo().volumen_m3 == Decimal("76.128")


def test_un_vehiculo_sin_la_carroceria_no_da_capacidad():
    assert _vehiculo().capacidad_con("TOLVA") is None
    assert _vehiculo().admite("TOLVA") is False


def test_la_incompatibilidad_aplica_en_las_dos_direcciones():
    """El par se guarda en el orden que lo haya cargado el CRUD, asi que
    comparar en un solo sentido dejaria pasar la mitad de los casos."""
    regla = ReglaIncompatibilidad(id="r", categoria_1_id="A", categoria_2_id="B",
                                  motivo="no pueden")
    assert regla.aplica_a("A", "B")
    assert regla.aplica_a("B", "A")
    assert not regla.aplica_a("A", "C")


def test_el_catalogo_encuentra_la_incompatibilidad_entre_dos_mercancias():
    ali = CategoriaMercancia(id="cat-ali", nombre="ALIMENTOS")
    fer = CategoriaMercancia(id="cat-fer", nombre="FERTILIZANTE")
    furgon = Carroceria(id="c", codigo="FURGON", nombre="Furgon")

    def mercancia(id_, codigo, categoria):
        return TipoMercancia(
            id=id_, codigo=codigo, nombre=codigo, categoria=categoria, cubicable=True,
            condicionada=False, carroceria_principal=furgon, carroceria_secundaria=None,
            secundaria_condicionada=False, control_ambiental="NINGUNO",
            apilable=True, fragil=False, rotacion_libre=True)

    bebidas, fertilizantes = mercancia("1", "BEBIDAS", ali), mercancia("2", "FERT", fer)
    cat = Catalogo(
        vehiculos=(), carrocerias=(furgon,), mercancias=(bebidas, fertilizantes),
        reglas=(ReglaIncompatibilidad(id="r", categoria_1_id="cat-fer",
                                      categoria_2_id="cat-ali", motivo="Contaminacion"),),
        parametros=ParametrosMotor())

    assert cat.son_incompatibles(bebidas, fertilizantes) == "Contaminacion"
    assert cat.son_incompatibles(bebidas, bebidas) is None


def test_los_parametros_caen_a_su_default_si_la_clave_no_esta_o_esta_rota():
    """El catalogo es editable por CRUD: un valor que no sea numero no debe
    tumbar el motor, tiene que caer al default documentado."""
    assert ParametrosMotor().k_max == 25
    assert ParametrosMotor(valores={"k_max": "no es un numero"}).k_max == 25
    assert ParametrosMotor(valores={"k_max": "4"}).k_max == 4


def test_requiere_control_ambiental_distingue_ninguno_del_resto():
    def con(control):
        return TipoMercancia(
            id="1", codigo="X", nombre="X",
            categoria=CategoriaMercancia(id="c", nombre="GENERAL"),
            cubicable=True, condicionada=False, carroceria_principal=None,
            carroceria_secundaria=None, secundaria_condicionada=False,
            control_ambiental=control, apilable=True, fragil=False, rotacion_libre=True)

    assert not con("NINGUNO").requiere_control_ambiental
    assert con("REFRIGERADO").requiere_control_ambiental
    assert con("VENTILADO_CONDICIONAL").requiere_control_ambiental
