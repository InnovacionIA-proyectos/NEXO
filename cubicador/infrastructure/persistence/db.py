"""Configuracion de SQLAlchemy async. `DATABASE_URL` default a SQLite local
para desarrollo; en produccion se apunta a PostgreSQL (ver docs/DECISIONES_TECNICAS.md
#10 - motor concreto pendiente de decidir con el equipo, esto ya funciona con
cualquiera de los dos sin cambiar codigo, solo la URL)."""

from __future__ import annotations

import logging
import os
from collections.abc import AsyncIterator

from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase
from sqlalchemy.pool import StaticPool

DATABASE_URL = os.environ.get("DATABASE_URL", "sqlite+aiosqlite:///./cubicador.db")

# SQLite ":memory:" crea una base NUEVA por conexion - con StaticPool se
# reusa una sola conexion para toda la app (solo aplica a este caso; en
# Postgres/SQLite-archivo el pool por defecto es el correcto).
_es_sqlite_memoria = "sqlite" in DATABASE_URL and ":memory:" in DATABASE_URL
engine = create_async_engine(
    DATABASE_URL, echo=False,
    poolclass=StaticPool if _es_sqlite_memoria else None,
    connect_args={"check_same_thread": False} if "sqlite" in DATABASE_URL else {},
)
SessionLocal = async_sessionmaker(engine, expire_on_commit=False)

logger = logging.getLogger(__name__)


class Base(DeclarativeBase):
    pass


def _agregar_lo_que_falte(conn) -> None:
    """Migracion minima para bases YA creadas: `create_all` crea tablas nuevas
    pero NUNCA altera una existente, asi que una columna agregada al modelo no
    llega sola al Postgres desplegado (S4-HU-12.1 agrego 7 a
    `vehiculos_cubicacion`).

    No se metio Alembic a proposito: habria sido una dependencia y un flujo
    nuevos dentro de una issue que es de datos, no de infraestructura. Queda
    como deuda explicita - el dia que haga falta renombrar, borrar o cambiar
    el tipo de una columna, esto no alcanza y hay que traerlo.

    Solo agrega columnas NULLABLE. Una NOT NULL sin server_default no se puede
    agregar a una tabla con filas, asi que se salta y se avisa en vez de
    romper el arranque.
    """
    from sqlalchemy import inspect as _inspect
    from sqlalchemy.schema import CreateIndex

    inspector = _inspect(conn)
    dialecto = conn.dialect
    tablas_existentes = set(inspector.get_table_names())

    for tabla in Base.metadata.sorted_tables:
        if tabla.name not in tablas_existentes:
            continue  # recien creada por create_all: ya nacio completa

        presentes = {c["name"] for c in inspector.get_columns(tabla.name)}
        for columna in tabla.columns:
            if columna.name in presentes:
                continue
            if not columna.nullable and columna.server_default is None:
                logger.warning(
                    "No se agrega %s.%s: es NOT NULL sin server_default y la tabla ya tiene filas. "
                    "Requiere migracion manual.", tabla.name, columna.name,
                )
                continue
            tipo = columna.type.compile(dialecto)
            conn.exec_driver_sql(f'ALTER TABLE "{tabla.name}" ADD COLUMN "{columna.name}" {tipo}')
            logger.info("Columna agregada: %s.%s (%s)", tabla.name, columna.name, tipo)

        # Una columna que en el modelo paso a ser nullable sigue siendo NOT NULL
        # en la base ya creada (S4-HU-12.1 relajo puerta_ancho_mm/puerta_alto_mm
        # para el contenedor refrigerado, que no tiene ese dato). `create_all` no
        # toca restricciones de una tabla existente.
        por_nombre = {c["name"]: c for c in inspector.get_columns(tabla.name)}
        for columna in tabla.columns:
            actual = por_nombre.get(columna.name)
            if actual is None or not columna.nullable or actual["nullable"]:
                continue
            if dialecto.name != "postgresql":
                # SQLite no soporta ALTER COLUMN; en desarrollo y tests la tabla
                # nace del modelo, asi que ya es nullable y esto no aplica.
                logger.warning(
                    "%s.%s sigue NOT NULL: el dialecto %s no permite relajarlo. "
                    "Requiere recrear la tabla.", tabla.name, columna.name, dialecto.name,
                )
                continue
            conn.exec_driver_sql(
                f'ALTER TABLE "{tabla.name}" ALTER COLUMN "{columna.name}" DROP NOT NULL'
            )
            logger.info("NOT NULL removido: %s.%s", tabla.name, columna.name)

        # Los indices de una tabla preexistente tampoco los crea `create_all`.
        # Incluye el UNIQUE de `vehiculos_cubicacion.codigo`.
        indices_existentes = {i["name"] for i in inspector.get_indexes(tabla.name)}
        for indice in tabla.indexes:
            if indice.name not in indices_existentes:
                conn.execute(CreateIndex(indice, if_not_exists=True))
                logger.info("Indice creado: %s", indice.name)


async def crear_tablas() -> None:
    # Import local (no arriba: `modelos` importa `Base` de este modulo y seria
    # circular). Sin esto, `Base.metadata` puede estar VACIO si nadie importo
    # los modelos todavia, y entonces create_all y la migracion no hacen nada
    # -en silencio-. Hoy los repositorios lo importan de rebote, pero eso es
    # una casualidad del orden de imports, no una garantia.
    from infrastructure.persistence import modelos  # noqa: F401

    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
        await conn.run_sync(_agregar_lo_que_falte)


async def obtener_sesion() -> AsyncIterator[AsyncSession]:
    async with SessionLocal() as sesion:
        yield sesion
