"""Configuracion de SQLAlchemy async. `DATABASE_URL` default a SQLite local
para desarrollo; en produccion se apunta a PostgreSQL (ver docs/DECISIONES_TECNICAS.md
#10 - motor concreto pendiente de decidir con el equipo, esto ya funciona con
cualquiera de los dos sin cambiar codigo, solo la URL)."""

from __future__ import annotations

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


class Base(DeclarativeBase):
    pass


async def crear_tablas() -> None:
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


async def obtener_sesion() -> AsyncIterator[AsyncSession]:
    async with SessionLocal() as sesion:
        yield sesion
