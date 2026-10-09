"""S5-HU-17.1: las dos piezas de la migracion a SQL Server que, si se rompen,
no fallan — hacen algo peor en silencio.

1. El esquema del `MetaData`. Tiene que estar puesto contra SQL Server (si no,
   las tablas caen en `dbo`, compartido con Cotifly) y tiene que estar en None
   contra SQLite (que no soporta esquemas: declararlo rompe toda la suite).

2. El `include_object` de alembic. Las 9 tablas que el equipo creo y sembro a
   mano viven en el mismo esquema pero alembic no las conoce. Sin el filtro, un
   `--autogenerate` propone `op.drop_table` para las 9 — con datos reales
   adentro. Esta prueba existe para que sacar una de la lista, o romper el
   filtro, se note aca y no en produccion.
"""

from __future__ import annotations

import importlib
import os
import sys

import pytest

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
if RAIZ not in sys.path:
    sys.path.insert(0, RAIZ)


def _recargar_db(monkeypatch, url: str, esquema: str | None = None):
    """Reimporta db.py con otra DATABASE_URL, que se lee en tiempo de import."""
    monkeypatch.setenv("DATABASE_URL", url)
    if esquema is None:
        monkeypatch.delenv("CUBICADOR_DB_ESQUEMA", raising=False)
    else:
        monkeypatch.setenv("CUBICADOR_DB_ESQUEMA", esquema)
    import infrastructure.persistence.db as db
    return importlib.reload(db)


# --- 1. El esquema solo contra SQL Server ---------------------------------

def test_sqlite_no_declara_esquema(monkeypatch):
    """SQLite no soporta esquemas: declararlo rompe cualquier consulta."""
    db = _recargar_db(monkeypatch, "sqlite+aiosqlite:///:memory:")
    assert db.ESQUEMA is None
    assert db.Base.metadata.schema is None


def test_sqlite_ignora_la_variable_de_esquema(monkeypatch):
    """Aunque CUBICADOR_DB_ESQUEMA este puesta, contra SQLite no aplica."""
    db = _recargar_db(monkeypatch, "sqlite+aiosqlite:///:memory:", "dbo_cubicador")
    assert db.ESQUEMA is None


def test_sqlserver_declara_el_esquema(monkeypatch):
    db = _recargar_db(
        monkeypatch,
        "mssql+aioodbc://u:p@host:1433/base?driver=ODBC+Driver+18+for+SQL+Server",
        "dbo_cubicador")
    assert db.ESQUEMA == "dbo_cubicador"
    assert db.Base.metadata.schema == "dbo_cubicador"


def test_sqlserver_cae_a_dbo_cubicador_por_defecto(monkeypatch):
    """Sin la variable, el default NO debe ser `dbo`: ahi vive Cotifly."""
    db = _recargar_db(
        monkeypatch,
        "mssql+aioodbc://u:p@host:1433/base?driver=ODBC+Driver+18+for+SQL+Server")
    assert db.ESQUEMA == "dbo_cubicador"


def test_crear_tablas_no_hace_nada_contra_sqlserver(monkeypatch):
    """`create_all` contra SQL Server dejaria tablas sin sello de alembic, y el
    `upgrade head` siguiente fallaria al crear lo que ya existe."""
    import asyncio

    db = _recargar_db(
        monkeypatch,
        "mssql+aioodbc://u:p@host:1433/base?driver=ODBC+Driver+18+for+SQL+Server",
        "dbo_cubicador")
    # Si intentara conectar, esto explotaria: no hay servidor en `host`.
    asyncio.run(db.crear_tablas())


# --- 2. El filtro que protege las tablas del equipo ------------------------

# Creadas y sembradas a mano por el equipo en dbo_cubicador (ver comentario de
# la issue #266). Alembic no las gobierna y no debe tocarlas.
TABLAS_DEL_EQUIPO = [
    "categoriaMercancia",
    "parametrosMotor",
    "recomendacionVehiculo",
    "recomendacionVehiculoMercancia",
    "reglasIncompatibilidad",
    "relacionVehiculosCarroceria",
    "TiposCarroceria",
    "tiposMercancia",
    "vehiculos",
]

# Las 5 del motor 3D, que alembic SI gobierna.
TABLAS_PROPIAS = [
    "vehiculos_cubicacion",
    "planes_cargue",
    "items_plan_cargue",
    "colocaciones_cargue",
    "parametros_cubicacion",
]


def _include_object():
    sys.path.insert(0, os.path.join(RAIZ, "alembic"))
    try:
        from alembic import context  # noqa: F401
    except ImportError:  # pragma: no cover
        pytest.skip("alembic no instalado")
    # env.py corre migraciones al importarse, asi que se lee el filtro del
    # fuente en vez de importarlo.
    ruta = os.path.join(RAIZ, "alembic", "env.py")
    espacio: dict = {}
    with open(ruta, encoding="utf-8") as f:
        fuente = f.read()
    ini = fuente.index("TABLAS_EXTERNAS")
    fin = fuente.index("def _opciones_comunes")
    exec(compile(fuente[ini:fin], ruta, "exec"), espacio)  # noqa: S102
    return espacio["include_object"], espacio["TABLAS_EXTERNAS"]


@pytest.mark.parametrize("tabla", TABLAS_DEL_EQUIPO)
def test_el_autogenerado_no_toca_las_tablas_del_equipo(tabla):
    include_object, externas = _include_object()
    assert tabla in externas, f"{tabla} salio de TABLAS_EXTERNAS: alembic la borraria"
    assert include_object(None, tabla, "table", True, None) is False


@pytest.mark.parametrize("tabla", TABLAS_PROPIAS)
def test_el_autogenerado_si_gobierna_las_tablas_del_motor_3d(tabla):
    include_object, _ = _include_object()
    assert include_object(None, tabla, "table", True, None) is True


def test_la_lista_de_externas_son_exactamente_nueve():
    """Si aparece una decima tabla sembrada a mano, hay que agregarla aqui a
    conciencia, no que se cuele sin que nadie lo note."""
    _, externas = _include_object()
    assert externas == set(TABLAS_DEL_EQUIPO)
