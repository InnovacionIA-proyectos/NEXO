"""Configuracion de SQLAlchemy async.

Motor: **SQL Server** (S5-HU-17.1). El Cubicador no tiene base propia: vive en
el esquema `dbo_cubicador` de `dbTarifario_Nexo_Pruebas`, que comparte con
Cotifly (`dbo_cotifly`). NEXO no esta ahi - usa su propia base.

`DATABASE_URL` sigue cayendo a SQLite en memoria cuando no esta definida, que
es lo que usan las pruebas. El esquema SOLO se aplica contra SQL Server: SQLite
no tiene esquemas y declararlo rompe toda la suite.

El esquema para SQL Server:

    DATABASE_URL=mssql+aioodbc://usuario:clave@host:1433/base?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes

Si la clave trae `@ : / # ?`, hay que codificarla con `urllib.parse.quote_plus`
o el DSN se parte mal.
"""

from __future__ import annotations

import os
from collections.abc import AsyncIterator

from sqlalchemy import MetaData
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase
from sqlalchemy.pool import StaticPool

DATABASE_URL = os.environ.get("DATABASE_URL", "sqlite+aiosqlite:///./cubicador.db")

_es_sqlite = DATABASE_URL.startswith("sqlite")
_es_mssql = DATABASE_URL.startswith("mssql")

# Esquema de TODAS las tablas del Cubicador. Sin esto caerian en `dbo`, que
# esta compartido con Cotifly.
#
# Queda en None fuera de SQL Server: SQLite no soporta esquemas y un
# `MetaData(schema=...)` ahi hace fallar cualquier consulta de la suite.
ESQUEMA = os.environ.get("CUBICADOR_DB_ESQUEMA", "dbo_cubicador") if _es_mssql else None

# SQLite ":memory:" crea una base NUEVA por conexion - con StaticPool se
# reusa una sola conexion para toda la app (solo aplica a este caso; en
# SQL Server y SQLite-archivo el pool por defecto es el correcto).
_es_sqlite_memoria = _es_sqlite and ":memory:" in DATABASE_URL

engine = create_async_engine(
    DATABASE_URL, echo=False,
    poolclass=StaticPool if _es_sqlite_memoria else None,
    connect_args={"check_same_thread": False} if _es_sqlite else {},
)
SessionLocal = async_sessionmaker(engine, expire_on_commit=False)


class Base(DeclarativeBase):
    metadata = MetaData(schema=ESQUEMA)


async def crear_tablas() -> None:
    """Crea las tablas que falten. SOLO para SQLite (pruebas y arranque local).

    Contra SQL Server no hace nada a proposito: el esquema lo gobierna
    `database/esquema/ESQUEMA_CUBICADOR.sql`, que es la fuente de verdad y se
    aplica a mano. Dejar que `create_all`
    corra ahi al arrancar seria danino, no solo inutil:

    - `create_all` crea tablas nuevas pero NUNCA altera una existente, asi que
      una columna agregada al modelo despues del primer arranque se ignora en
      silencio.
    - El esquema `dbo_cubicador` se comparte con Cotifly dentro de la misma
      base: lo que toca la estructura se revisa y se aplica a mano, no en el
      arranque de un contenedor.

    Las pruebas corren contra SQLite en memoria, donde no hay scripts de por
    medio, asi que ahi sigue siendo la forma correcta de armar el esquema.
    """
    if _es_mssql:
        return
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


async def obtener_sesion() -> AsyncIterator[AsyncSession]:
    async with SessionLocal() as sesion:
        yield sesion
