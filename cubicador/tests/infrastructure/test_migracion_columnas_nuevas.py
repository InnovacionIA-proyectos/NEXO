"""S4-HU-12.1 - las columnas nuevas tienen que llegar a una base YA creada.

`Base.metadata.create_all` crea tablas que no existen, pero NUNCA altera una
existente. Sin la migracion de `db.py`, las 7 columnas que esta issue agrega a
`vehiculos_cubicacion` aparecerian en desarrollo -donde la base nace del
modelo- y no en el Postgres desplegado, en silencio y sin error.

No se metio Alembic: habria sido una dependencia y un flujo nuevos dentro de
una issue que es de datos. La contrapartida es que la migracion es codigo
propio, y por eso se prueba.

Estos tests recrean a mano la tabla como estaba ANTES de la issue, con una fila
dentro, y verifican que actualizar no pierda nada.
"""

from __future__ import annotations

from sqlalchemy import text

from infrastructure.persistence.db import crear_tablas, engine

# La tabla tal como estaba antes de S4-HU-12.1: 17 columnas, sin `codigo` ni la
# clasificacion, y con la puerta obligatoria.
_TABLA_ANTERIOR = """
CREATE TABLE vehiculos_cubicacion (
    id VARCHAR(36) PRIMARY KEY, nombre VARCHAR(100), descripcion VARCHAR(300),
    largo_interno_mm FLOAT, ancho_interno_mm FLOAT, alto_interno_mm FLOAT,
    capacidad_kg FLOAT, tara_kg FLOAT,
    puerta_ancho_mm FLOAT NOT NULL, puerta_alto_mm FLOAT NOT NULL,
    puerta_posicion VARCHAR(20), ejes_json TEXT,
    tolerancia_paredes_mm FLOAT, tolerancia_entre_unidades_mm FLOAT, es_preset BOOLEAN,
    created_at DATETIME, updated_at DATETIME)
"""

_COLUMNAS_NUEVAS = (
    "codigo", "config_vehiculo", "carroceria", "clase_viaje",
    "clase_contenedor", "geometria", "control_ambiental",
)


async def _volver_al_esquema_anterior() -> None:
    """Deshace lo que el fixture `base_limpia` dejo, para esta tabla."""
    async with engine.begin() as conn:
        await conn.execute(text("DROP TABLE IF EXISTS vehiculos_cubicacion"))
        await conn.execute(text(_TABLA_ANTERIOR))
        await conn.execute(text(
            "INSERT INTO vehiculos_cubicacion "
            "(id, nombre, largo_interno_mm, ancho_interno_mm, alto_interno_mm, capacidad_kg, tara_kg, "
            " puerta_ancho_mm, puerta_alto_mm, puerta_posicion, ejes_json, "
            " tolerancia_paredes_mm, tolerancia_entre_unidades_mm, es_preset, created_at, updated_at) "
            "VALUES ('editada-por-el-usuario', 'Mi turbo', 6200, 2200, 2000, 9999, 4200, "
            "2200, 1950, 'trasera', '[]', 0, 0, 1, '2026-01-01 00:00:00', '2026-01-01 00:00:00')"
        ))


async def _columnas() -> list[str]:
    async with engine.begin() as conn:
        filas = (await conn.execute(text("PRAGMA table_info(vehiculos_cubicacion)"))).all()
    return [f[1] for f in filas]


async def test_actualizar_agrega_las_columnas_nuevas_a_una_tabla_existente():
    await _volver_al_esquema_anterior()
    assert not set(_COLUMNAS_NUEVAS) & set(await _columnas()), "el esquema de partida no las tiene"

    await crear_tablas()

    assert set(_COLUMNAS_NUEVAS) <= set(await _columnas())


async def test_actualizar_no_toca_los_datos_que_ya_estaban():
    """Lo que el usuario edito desde la interfaz tiene que sobrevivir intacto, y
    las columnas nuevas nacer vacias en vez de con un valor inventado."""
    await _volver_al_esquema_anterior()

    await crear_tablas()

    async with engine.begin() as conn:
        fila = (await conn.execute(text(
            "SELECT nombre, capacidad_kg, codigo, geometria FROM vehiculos_cubicacion "
            "WHERE id = 'editada-por-el-usuario'"
        ))).first()

    assert fila.nombre == "Mi turbo"
    assert fila.capacidad_kg == 9999, "el valor que el usuario habia editado sigue ahi"
    assert fila.codigo is None
    assert fila.geometria is None


async def test_actualizar_dos_veces_no_falla():
    """Corre en cada arranque de la API y del worker, asi que tiene que ser
    idempotente: la segunda pasada no debe encontrar nada que hacer ni romper."""
    await _volver_al_esquema_anterior()

    await crear_tablas()
    await crear_tablas()

    assert set(_COLUMNAS_NUEVAS) <= set(await _columnas())


async def test_actualizar_crea_el_indice_unico_de_codigo():
    """`create_all` tampoco crea indices sobre una tabla preexistente. Sin este,
    dos vehiculos podrian compartir codigo y la identidad estable dejaria de
    serlo."""
    await _volver_al_esquema_anterior()

    await crear_tablas()

    async with engine.begin() as conn:
        indices = [f[1] for f in (await conn.execute(text("PRAGMA index_list(vehiculos_cubicacion)"))).all()]

    assert "ix_vehiculos_cubicacion_codigo" in indices
