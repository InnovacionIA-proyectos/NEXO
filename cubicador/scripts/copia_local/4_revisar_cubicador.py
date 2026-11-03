"""Revisa el esquema dbo_cubicador que el equipo ya creo y sembro.

Contrasta lo que hay de verdad contra lo que dice la hoja de ruta y el
comentario de la issue #266. No escribe nada.

    cubicador/.venv/Scripts/python.exe scripts/copia_local/4_revisar_cubicador.py
"""
from __future__ import annotations

import os
import re
import sys

import pymssql

sys.path.insert(0, os.path.dirname(__file__))
import _conexion as cx  # noqa: E402

ESQUEMA = "dbo_cubicador"


def conectar():
    env = cx._leer_env(os.path.join(cx.RAIZ, "cubicador", ".env"))
    m = re.match(r"mssql\+aioodbc://([^:]+):([^@]+)@([^:]+):(\d+)/([^?]+)", env["DATABASE_URL"])
    if not m:
        raise SystemExit("No pude interpretar DATABASE_URL de cubicador/.env")
    u, pw, host, puerto, base = m.groups()
    return pymssql.connect(server=host, port=int(puerto), user=u, password=pw,
                           database=base, timeout=60, login_timeout=30)


def titulo(t: str) -> None:
    print(f"\n{t}\n{'-' * len(t)}")


def main() -> int:
    con = conectar()
    cur = con.cursor()

    titulo("Tablas y filas")
    cur.execute("""
        SELECT t.name,
               (SELECT COUNT(*) FROM sys.columns WHERE object_id = t.object_id),
               SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END)
          FROM sys.tables t
          JOIN sys.schemas s ON s.schema_id = t.schema_id
          JOIN sys.partitions p ON p.object_id = t.object_id
         WHERE s.name = %s
         GROUP BY t.name, t.object_id
         ORDER BY t.name
    """, (ESQUEMA,))
    tablas = cur.fetchall()
    print(f"{'tabla':<40}{'cols':>6}{'filas':>9}")
    for n, cols, filas in tablas:
        print(f"{n:<40}{cols:>6}{int(filas or 0):>9}")
    print(f"\n{len(tablas)} tablas")

    titulo("Convencion de nombres")
    snake = [n for n, _, _ in tablas if "_" in n and n.lower() == n]
    print(f"  snake_case: {len(snake)}   PascalCase/otro: {len(tablas) - len(snake)}")

    titulo("Defaults de los Id")
    cur.execute("""
        SELECT t.name, c.name, d.definition
          FROM sys.default_constraints d
          JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
          JOIN sys.tables t ON t.object_id = d.parent_object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
         WHERE s.name = %s AND d.definition LIKE '%%id%%'
         ORDER BY t.name
    """, (ESQUEMA,))
    vistos = set()
    for t, c, d in cur.fetchall():
        if d not in vistos:
            print(f"  {d:<28} (ej. {t}.{c})")
            vistos.add(d)
    if not vistos:
        print("  sin defaults de Id")

    titulo("Columnas de version / concurrencia optimista")
    cur.execute("""
        SELECT t.name, c.name, ty.name
          FROM sys.columns c
          JOIN sys.tables t ON t.object_id = c.object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
          JOIN sys.types ty ON ty.user_type_id = c.user_type_id
         WHERE s.name = %s
           AND (ty.name IN ('timestamp', 'rowversion') OR c.name LIKE '%%version%%')
    """, (ESQUEMA,))
    filas = cur.fetchall()
    for t, c, ty in filas:
        print(f"  {t}.{c}  {ty}")
    if not filas:
        print("  NINGUNA. Sin esto los CRUD (#270) no tienen concurrencia optimista.")

    titulo("Collation de las columnas de texto")
    cur.execute("""
        SELECT DISTINCT c.collation_name
          FROM sys.columns c
          JOIN sys.tables t ON t.object_id = c.object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
         WHERE s.name = %s AND c.collation_name IS NOT NULL
    """, (ESQUEMA,))
    for (col,) in cur.fetchall():
        acentos = "sensible a tildes" if col and col.endswith("_AS") else "insensible a tildes"
        print(f"  {col}  ({acentos})")

    titulo("Conteos del catalogo sembrado")
    for tabla in ("Categorias", "CategoriaMercancia", "TiposCarroceria", "TiposMercancia",
                  "Vehiculos", "VehiculoCarroceria", "ReglasIncompatibilidad",
                  "ParametrosMotor"):
        try:
            cur.execute(f"SELECT COUNT(*) FROM [{ESQUEMA}].[{tabla}]")
            print(f"  {tabla:<28}{cur.fetchone()[0]:>6}")
        except Exception:
            con.rollback()

    titulo("Mercancias no cubicables (los 10 excluidos)")
    for col in ("Cubicable", "EsCubicable"):
        try:
            cur.execute(f"SELECT {col}, COUNT(*) FROM [{ESQUEMA}].[TiposMercancia] GROUP BY {col}")
            for v, n in cur.fetchall():
                print(f"  {col} = {v}: {n}")
            break
        except Exception:
            con.rollback()
    else:
        print("  no encontre la columna de cubicable")

    con.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
