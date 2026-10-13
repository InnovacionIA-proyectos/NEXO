"""S5-HU-17.1/17.2: el esquema del `MetaData`, que si se rompe no falla —
hace algo peor en silencio.

Tiene que estar puesto contra SQL Server (si no, las tablas caen en `dbo`,
compartido con Cotifly) y tiene que estar en None contra SQLite, que no
soporta esquemas: declararlo ahi rompe toda la suite.

Y los modelos del catalogo tienen que mantener su `MetaData` propio, separado
del de `modelos.py`: son dos juegos de tablas con dueños distintos, y
compartirlo haria que un `create_all` las tratara igual.
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
    """El esquema de SQL Server lo gobiernan los scripts de `docs/Database/`:
    `create_all` ahi crearia tablas fuera de esa fuente de verdad, en un
    esquema que ademas se comparte con Cotifly."""
    import asyncio

    db = _recargar_db(
        monkeypatch,
        "mssql+aioodbc://u:p@host:1433/base?driver=ODBC+Driver+18+for+SQL+Server",
        "dbo_cubicador")
    # Si intentara conectar, esto explotaria: no hay servidor en `host`.
    asyncio.run(db.crear_tablas())


# --- 2. Los modelos del catalogo (S5-HU-17.2) ------------------------------

def test_los_modelos_del_catalogo_tienen_metadata_propio():
    """Si compartieran el `MetaData` de `modelos.py`, un `create_all` trataria
    igual a las 5 tablas del motor 3D y a las 10 del catalogo, que tienen
    dueños distintos: las del catalogo las administra el equipo de Datos."""
    from infrastructure.persistence import modelos_catalogo as mc
    from infrastructure.persistence.db import Base

    assert mc.BaseCatalogo.metadata is not Base.metadata
    assert len(mc.BaseCatalogo.metadata.tables) == 10


def test_el_catalogo_mapea_las_diez_tablas_con_sus_nombres_reales():
    """El esquema usa camelCase para las tablas, con `TiposCarroceria` como
    unica PascalCase. Renombrarlas en el modelo las desconectaria de la base."""
    from infrastructure.persistence import modelos_catalogo as mc

    nombres = {t.split(".")[-1] for t in mc.BaseCatalogo.metadata.tables}
    assert nombres == {
        "vehiculos", "TiposCarroceria", "relacionVehiculosCarroceria",
        "categoriaMercancia", "tiposMercancia", "reglasIncompatibilidad",
        "parametrosMotor", "recomendaciones", "recomendacionVehiculo",
        "recomendacionVehiculoMercancia",
    }


def test_el_alto_util_apunta_a_la_columna_con_nombre_enganoso():
    """`AlturaMaximaReglamentariaCm` dice "maxima reglamentaria" y su default es
    440 (el tope legal), pero guarda el alto UTIL: 230 para el Turbo, 260 para
    la tractomula. El atributo de Python se llama como lo que contiene."""
    from infrastructure.persistence import modelos_catalogo as mc

    col = mc.Vehiculo.__table__.columns["AlturaMaximaReglamentariaCm"]
    assert mc.Vehiculo.alto_util_cm.property.columns[0] is col
