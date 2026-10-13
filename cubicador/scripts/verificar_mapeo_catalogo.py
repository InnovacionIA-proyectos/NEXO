"""Compara los modelos de `modelos_catalogo.py` contra el esquema REAL (S5-HU-17.2).

Las 10 tablas de `dbo_cubicador` las administra el equipo de Datos, no este
repo: nada en el codigo las crea ni las altera, asi que un cambio suyo en la
base no rompe ninguna prueba hasta que algo falla en produccion. Este script es
el que lo detecta.

Compara, columna por columna, contra `sys.columns`:

  - que la columna exista en la base con el nombre que declara el modelo,
  - el tipo SQL,
  - el largo de los NVARCHAR y la precision/escala de los DECIMAL,
  - si admite NULL,
  - y al reves: columnas que esten en la base y no en el modelo.

Las pruebas de pytest corren contra SQLite y no pueden ver esto; por eso vive
aparte y se corre a mano contra QA:

    ./.venv/Scripts/python.exe cubicador/scripts/verificar_mapeo_catalogo.py

Lee la conexion de `cubicador/.env` (DATABASE_URL) y usa pymssql, que no
necesita el driver ODBC en Windows. Sale 0 si todo cuadra, 1 si hay diferencias.
"""
from __future__ import annotations

import io
import os
import sys
import urllib.parse

import pymssql

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, RAIZ)

from sqlalchemy import Boolean, DateTime, Integer, Numeric, String, Text  # noqa: E402

from infrastructure.persistence import modelos_catalogo as mc  # noqa: E402


def _conexion_desde_env() -> dict[str, object]:
    ruta = os.path.join(RAIZ, ".env")
    if not os.path.exists(ruta):
        raise SystemExit("Falta cubicador/.env (copiar de .env.example).")
    url = ""
    with io.open(ruta, encoding="utf-8") as f:
        for linea in f:
            if linea.strip().startswith("DATABASE_URL="):
                url = linea.strip().split("=", 1)[1]
    if not url.startswith("mssql"):
        raise SystemExit("DATABASE_URL no apunta a SQL Server.")
    p = urllib.parse.urlsplit(url)
    host, _, puerto = (p.hostname or ""), None, (p.port or 1433)
    return {
        "server": host,
        "port": int(puerto),
        "user": urllib.parse.unquote(p.username or ""),
        "password": urllib.parse.unquote(p.password or ""),
        "database": (p.path or "/").lstrip("/"),
        "esquema": os.environ.get("CUBICADOR_DB_ESQUEMA", "dbo_cubicador"),
    }


# Tipo de SQLAlchemy -> tipos de SQL Server que lo satisfacen. Varios a uno
# porque el modelo no distingue NVARCHAR de VARCHAR ni DATETIMEOFFSET de
# DATETIME2: eso lo decide el DDL del equipo de Datos.
_EQUIVALENCIAS = {
    String: {"nvarchar", "varchar", "char", "nchar", "uniqueidentifier"},
    Text: {"nvarchar", "varchar", "text", "ntext"},
    Integer: {"int", "smallint", "tinyint", "bigint"},
    Numeric: {"decimal", "numeric", "money", "float"},
    Boolean: {"bit"},
    DateTime: {"datetimeoffset", "datetime2", "datetime", "smalldatetime"},
}


def _tipos_aceptados(tipo) -> set[str]:
    for clase, nombres in _EQUIVALENCIAS.items():
        if isinstance(tipo, clase):
            return nombres
    return set()


def main() -> int:
    cfg = _conexion_desde_env()
    esquema = str(cfg["esquema"])
    print(f"Base {cfg['server']}:{cfg['port']}/{cfg['database']} esquema {esquema}\n")

    con = pymssql.connect(
        server=str(cfg["server"]), port=int(cfg["port"]), user=str(cfg["user"]),
        password=str(cfg["password"]), database=str(cfg["database"]),
        timeout=60, login_timeout=30)
    cur = con.cursor()
    cur.execute("""
        SELECT t.name, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE s.name = %s
    """, (esquema,))
    real: dict[str, dict[str, tuple]] = {}
    for tabla, col, tipo, largo, prec, esc, nulo in cur.fetchall():
        real.setdefault(tabla, {})[col] = (tipo, largo, prec, esc, bool(nulo))
    con.close()

    problemas: list[str] = []
    modelos = [m.class_ for m in mc.BaseCatalogo.registry.mappers]

    for modelo in sorted(modelos, key=lambda m: m.__tablename__):
        tabla = modelo.__tablename__
        cols_reales = real.get(tabla)
        if cols_reales is None:
            problemas.append(f"{tabla}: no existe en {esquema}")
            continue

        vistas: set[str] = set()
        for col in modelo.__table__.columns:
            vistas.add(col.name)
            info = cols_reales.get(col.name)
            if info is None:
                problemas.append(f"{tabla}.{col.name}: el modelo la declara, la base no la tiene")
                continue
            tipo_sql, largo, prec, esc, nulo = info

            aceptados = _tipos_aceptados(col.type)
            if aceptados and tipo_sql not in aceptados:
                problemas.append(
                    f"{tabla}.{col.name}: modelo {type(col.type).__name__}, base {tipo_sql}")

            if isinstance(col.type, String) and col.type.length and tipo_sql in ("nvarchar", "varchar"):
                # max_length de sys.columns viene en BYTES: nvarchar ocupa 2 por
                # caracter, y -1 significa MAX.
                reales = largo if tipo_sql == "varchar" else largo // 2
                if largo != -1 and reales != col.type.length:
                    problemas.append(
                        f"{tabla}.{col.name}: largo modelo {col.type.length}, base {reales}")

            if isinstance(col.type, Numeric) and col.type.precision and tipo_sql in ("decimal", "numeric"):
                if (prec, esc) != (col.type.precision, col.type.scale or 0):
                    problemas.append(
                        f"{tabla}.{col.name}: modelo DECIMAL({col.type.precision},{col.type.scale}), "
                        f"base DECIMAL({prec},{esc})")

            if col.nullable != nulo:
                problemas.append(
                    f"{tabla}.{col.name}: modelo {'NULL' if col.nullable else 'NOT NULL'}, "
                    f"base {'NULL' if nulo else 'NOT NULL'}")

        for col in sorted(set(cols_reales) - vistas):
            problemas.append(f"{tabla}.{col}: esta en la base y el modelo no la mapea")

        print(f"  {tabla}: {len(modelo.__table__.columns)} columnas mapeadas "
              f"de {len(cols_reales)} en la base")

    print()
    if problemas:
        print(f"{len(problemas)} diferencia(s):")
        for p in problemas:
            print(f"  - {p}")
        return 1
    print(f"Los {len(modelos)} modelos corresponden al esquema real.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
