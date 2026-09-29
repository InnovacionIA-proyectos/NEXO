"""Aislamiento de la base entre tests.

Sin esto, TODA la suite comparte una sola base: `DATABASE_URL` apunta a SQLite
`:memory:` y `db.py` usa `poolclass=StaticPool` para esa URL, precisamente para
que las multiples conexiones vean la MISMA base (sin StaticPool, SQLite crea una
nueva por conexion y nada funcionaria). El efecto colateral es que lo que un test
escribe sigue ahi para todos los que corren despues.

Eso convirtio el suite en una trampa silenciosa, y no en teoria - se piso dos
veces escribiendo los tests de S3-HU-10:

1. Colgarle un plan a un preset del catalogo lo deja "en uso", y
   `test_eliminar_vehiculo_borra_y_publica_vehiculo_eliminado` falla con
   VEHICULO_EN_USO.
2. Agregar un vehiculo rompe `test_flujo_completo_crear_optimizar`, que afirma
   `len(vehiculos) == 10` (9 hasta S4-HU-12.1).

Lo peor no es que fallen: es que fallan en OTRO archivo, con un error que no
menciona al test culpable. Se pierde mas tiempo buscando una regresion que no
existe que arreglando el problema real.

Recrear el esquema antes de cada test lo desarma: cada uno arranca en una base
vacia y siembra lo que necesite (`listar_con_siembra()` repone el catalogo, que
es como todos lo piden ya). Medido: ~1 segundo sobre la suite completa, y
verificado con un test contaminante deliberado - con este fixture no afecta a
nadie, sin el rompe un test ajeno.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import pytest

from infrastructure.persistence.db import Base, engine


@pytest.fixture(autouse=True)
async def base_limpia():
    """Esquema recreado antes de cada test. `autouse` a proposito: el aislamiento
    no debe depender de que cada autor se acuerde de pedirlo."""
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.drop_all)
        await conn.run_sync(Base.metadata.create_all)
    yield
