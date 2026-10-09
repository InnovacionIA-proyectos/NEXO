"""Entorno de alembic para el Cubicador (S5-HU-17.1).

Dos cosas que no son el template por defecto y conviene no deshacer:

1. **El esquema.** Todas las tablas del Cubicador viven en `dbo_cubicador`
   (ver infrastructure/persistence/db.py), y la propia `alembic_version`
   tambien: sin `version_table_schema` caeria en `dbo`, que se comparte con
   Cotifly.

2. **`include_object`.** El equipo creo y sembro a mano 9 tablas en ese mismo
   esquema, con otra convencion de nombres (camelCase y PascalCase mezclados,
   columnas en PascalCase). Alembic no las conoce, asi que un
   `--autogenerate` sin filtro generaria `op.drop_table` para las 9 - tablas
   con datos reales. Y cuando S5-HU-17.2 las mapee en SQLAlchemy con nombres
   idiomaticos de Python, querria renombrar todo. El filtro las deja fuera del
   autogenerado en los dos casos.
"""

from __future__ import annotations

import asyncio
import os
import sys
from logging.config import fileConfig

from sqlalchemy import pool
from sqlalchemy.engine import Connection
from sqlalchemy.ext.asyncio import async_engine_from_config

from alembic import context

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from infrastructure.persistence import modelos  # noqa: F401,E402  (registra los modelos)
from infrastructure.persistence.db import DATABASE_URL, ESQUEMA, Base  # noqa: E402

config = context.config
if config.config_file_name is not None:
    fileConfig(config.config_file_name)

# La URL sale del entorno, no de alembic.ini: es la misma que usa la app, y el
# .ini esta versionado (no debe llevar credenciales).
config.set_main_option("sqlalchemy.url", DATABASE_URL)

target_metadata = Base.metadata

# Tablas creadas y administradas FUERA de alembic: las hizo el equipo a mano en
# SQL Server, con datos ya sembrados. Alembic no las toca ni para crearlas, ni
# para borrarlas, ni para renombrarlas. Si alguna pasa a gobernarse desde aca,
# se saca de esta lista en el mismo commit que la mapea.
TABLAS_EXTERNAS = {
    "categoriaMercancia",
    "parametrosMotor",
    "recomendacionVehiculo",
    "recomendacionVehiculoMercancia",
    "reglasIncompatibilidad",
    "relacionVehiculosCarroceria",
    "TiposCarroceria",
    "tiposMercancia",
    "vehiculos",
}


def include_object(objeto, nombre, tipo, reflejado, comparado_con):
    """Deja fuera del autogenerado las tablas que alembic no gobierna."""
    if tipo == "table" and nombre in TABLAS_EXTERNAS:
        return False
    # Un indice o una FK de esas tablas tampoco debe aparecer.
    if tipo in ("index", "foreign_key_constraint", "unique_constraint"):
        tabla = getattr(objeto, "table", None)
        if tabla is not None and tabla.name in TABLAS_EXTERNAS:
            return False
    return True


def _opciones_comunes() -> dict:
    return {
        "target_metadata": target_metadata,
        "include_object": include_object,
        "include_schemas": ESQUEMA is not None,
        "version_table_schema": ESQUEMA,
        # Sin esto, un cambio de largo en un VARCHAR no se detecta.
        "compare_type": True,
        "compare_server_default": True,
    }


def run_migrations_offline() -> None:
    context.configure(
        url=config.get_main_option("sqlalchemy.url"),
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
        **_opciones_comunes(),
    )
    with context.begin_transaction():
        context.run_migrations()


def do_run_migrations(connection: Connection) -> None:
    context.configure(connection=connection, **_opciones_comunes())
    with context.begin_transaction():
        context.run_migrations()


async def run_async_migrations() -> None:
    connectable = async_engine_from_config(
        config.get_section(config.config_ini_section, {}),
        prefix="sqlalchemy.",
        poolclass=pool.NullPool,
    )
    async with connectable.connect() as connection:
        await connection.run_sync(do_run_migrations)
    await connectable.dispose()


def run_migrations_online() -> None:
    asyncio.run(run_async_migrations())


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
