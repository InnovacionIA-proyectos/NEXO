"""Las consultas del plan de cargue, ejecutadas contra SQL Server de verdad (#344).

La prueba hermana (`test_sql_valido_en_sqlserver.py`) compila el SQL y revisa
su forma, sin base: es rapida y corre siempre. Esta lo **ejecuta**, que es lo
unico que demuestra que el motor lo acepta.

**Se salta sola** si no hay un SQL Server a la mano. Hace falta el "ODBC Driver
18 for SQL Server", que el host normalmente no tiene y el contenedor si:

    docker compose up -d --wait db
    docker compose exec api python -m pytest tests/infrastructure/test_plan_cargue_repo_contra_sqlserver.py -q

Con `CUBICADOR_TEST_MSSQL_URL` se apunta a otra instancia. **Nunca a QA**: crea
y borra tablas en su propio esquema.
"""

from __future__ import annotations

import os
import urllib.parse
import uuid
from datetime import datetime, timezone

import pytest
import pytest_asyncio
from sqlalchemy import text
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine

from infrastructure.persistence.modelos import (
    Base,
    ColocacionCargueModel,
    ItemPlanCargueModel,
    PlanCargueModel,
    VehiculoCubicacionModel,
)
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository

_ESQUEMA = "pruebas_344"


def _url() -> str | None:
    if os.environ.get("CUBICADOR_TEST_MSSQL_URL"):
        return os.environ["CUBICADOR_TEST_MSSQL_URL"]
    clave = os.environ.get("MSSQL_SA_PASSWORD")
    host = os.environ.get("CUBICADOR_TEST_MSSQL_HOST", "db")
    if not clave:
        return None
    return (f"mssql+aioodbc://sa:{urllib.parse.quote_plus(clave)}@{host}:1433/master"
            "?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes")


@pytest_asyncio.fixture
async def sesion():
    url = _url()
    if url is None:
        pytest.skip("sin SQL Server: definir MSSQL_SA_PASSWORD o CUBICADOR_TEST_MSSQL_URL")

    motor = create_async_engine(url)
    try:
        async with motor.connect() as c:
            await c.execute(text("SELECT 1"))
    except Exception as exc:  # noqa: BLE001 - driver ausente o servidor caido
        await motor.dispose()
        pytest.skip(f"SQL Server no alcanzable: {type(exc).__name__}")

    # Esquema propio y desechable: no toca nada de dbo_cubicador.
    async with motor.begin() as c:
        await c.execute(text(
            f"IF SCHEMA_ID('{_ESQUEMA}') IS NULL EXEC('CREATE SCHEMA {_ESQUEMA}')"))

    # Por NOMBRE y no por clave: contra SQL Server el metadata lleva esquema
    # (`dbo_cubicador.planes_cargue`) y contra SQLite no, asi que la clave
    # cambia segun donde corra. Y el esquema original se guarda para dejarlo
    # como estaba: estas tablas las comparte el resto de la suite.
    # `vehiculos_cubicacion` entra porque `planes_cargue` la referencia por
    # clave foranea: sin ella, el CREATE TABLE apunta al esquema real.
    quiero = {"vehiculos_cubicacion", "planes_cargue", "items_plan_cargue",
              "colocaciones_cargue"}
    tablas = [t for t in Base.metadata.tables.values() if t.name in quiero]
    assert len(tablas) == 4, f"faltan tablas: {sorted(quiero - {t.name for t in tablas})}"
    esquemas_originales = {t.name: t.schema for t in tablas}
    for t in tablas:
        t.schema = _ESQUEMA
    try:
        async with motor.begin() as c:
            await c.run_sync(Base.metadata.create_all, tables=tablas)
        async with async_sessionmaker(motor, expire_on_commit=False)() as s:
            yield s
    finally:
        async with motor.begin() as c:
            await c.run_sync(Base.metadata.drop_all, tables=tablas)
            # Y el esquema: si no, queda vacio para siempre. Da igual en el
            # compose local, pero `CUBICADOR_TEST_MSSQL_URL` puede apuntar a
            # otra instancia y ahi seria basura ajena.
            await c.execute(text(
                f"IF SCHEMA_ID('{_ESQUEMA}') IS NOT NULL DROP SCHEMA {_ESQUEMA}"))
        for t in tablas:
            t.schema = esquemas_originales[t.name]
        await motor.dispose()


async def _sembrar(sesion) -> str:
    """Un plan con dos unidades: una cargada y una sobrante."""
    ahora = datetime.now(timezone.utc)
    plan_id, item_id = str(uuid.uuid4()), str(uuid.uuid4())
    sesion.add(VehiculoCubicacionModel(
        id="v-1", nombre="Furgon de prueba", largo_interno_mm=6000, ancho_interno_mm=2400,
        alto_interno_mm=2400, capacidad_kg=8500, tara_kg=0,
        puerta_ancho_mm=2400, puerta_alto_mm=2400, created_at=ahora, updated_at=ahora))
    await sesion.flush()
    sesion.add(PlanCargueModel(
        id=plan_id, nombre="Prueba 344", vehiculo_id="v-1", estado="listo",
        linea_solicitud_id="linea-344", numero_vehiculo=1,
        created_at=ahora, updated_at=ahora))
    # Un flush por nivel: SQL Server valida la clave foranea en el INSERT, y
    # sin esto SQLAlchemy puede mandar las colocaciones antes que su plan.
    await sesion.flush()
    sesion.add(ItemPlanCargueModel(
        id=item_id, plan_cargue_id=plan_id, sku="POS-1", descripcion="Caja",
        largo_mm=600, ancho_mm=400, alto_mm=400, peso_kg=10, cantidad=2,
        created_at=ahora, updated_at=ahora))
    await sesion.flush()
    # Una cargada y bloqueada, otra ni lo uno ni lo otro: asi los tres filtros
    # -cargada, no cargada, y cargada+bloqueada- tienen algo que descartar.
    for indice, cargada, bloqueada in ((0, True, True), (1, False, False)):
        sesion.add(ColocacionCargueModel(
            id=str(uuid.uuid4()), plan_cargue_id=plan_id, item_plan_cargue_id=item_id,
            indice_unidad=indice, cargada=cargada, bloqueada=bloqueada))
    await sesion.commit()
    return plan_id


async def test_el_recorrido_del_plan_no_revienta_contra_sqlserver(sesion):
    """Los cinco metodos que filtran por booleano, ejecutados de verdad.

    Antes de #344 el primero reventaba con el error 102 de SQL Server
    ("Incorrect syntax near '0'"), porque `.is_(False)` compila a `IS 0`. Como
    la suite corre contra SQLite -que si acepta esa forma- nada lo veia.
    """
    plan_id = await _sembrar(sesion)
    repo = SqlPlanCargueRepository(sesion)

    sobrantes = await repo.obtener_sobrantes(plan_id)
    assert sum(c for _, c in sobrantes) == 1, "una de las dos unidades quedo sin cargar"

    assert await repo.cargo_algo(plan_id) is True

    # Con una bloqueada y una que no, el filtro tiene algo que descartar: si
    # alguien quitara el `AND bloqueada`, esto lo nota. Con las dos sin
    # bloquear, esperar `[]` pasaba aunque el filtro no existiera.
    bloqueadas = await repo.obtener_colocaciones_bloqueadas(plan_id)
    assert [c.indice_unidad for c in bloqueadas] == [0], (
        "solo la unidad 0 esta cargada Y bloqueada")

    flota = await repo.flota(plan_id)
    assert len(flota) == 1
    assert flota[0]["unidadesTotales"] == 2
    assert flota[0]["unidadesCargadas"] == 1

    por_linea = await repo.listar_por_linea("linea-344")
    assert len(por_linea) == 1
    assert por_linea[0]["unidadesCargadas"] == 1
