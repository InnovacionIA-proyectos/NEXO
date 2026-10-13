"""Vuelca el esquema completo de dbo_cubicador: columnas, tipos, defaults,
llaves foraneas, indices y restricciones CHECK.

Es la fuente contra la que se escriben los modelos de SQLAlchemy de S5-HU-17.2.
Requiere el permiso VIEW DEFINITION, sin el cual las definiciones vuelven NULL.
No escribe nada.

    cubicador/.venv/Scripts/python.exe scripts/copia_local/5_volcar_esquema.py
"""
from __future__ import annotations

import os
import re
import sys
from collections import defaultdict

import pymssql

sys.path.insert(0, os.path.dirname(__file__))
import _conexion as cx  # noqa: E402

ESQUEMA = "dbo_cubicador"


def conectar():
    env = cx._leer_env(os.path.join(cx.RAIZ, "cubicador", ".env"))
    m = re.match(r"mssql\+aioodbc://([^:]+):([^@]+)@([^:]+):(\d+)/([^?]+)", env["DATABASE_URL"])
    u, pw, host, puerto, base = m.groups()
    return pymssql.connect(server=host, port=int(puerto), user=u, password=pw,
                           database=base, timeout=60, login_timeout=30)


def main() -> int:
    con = conectar()
    c = con.cursor()

    c.execute("SELECT HAS_PERMS_BY_NAME(%s, 'SCHEMA', 'VIEW DEFINITION')", (ESQUEMA,))
    if not c.fetchone()[0]:
        print("Sin permiso VIEW DEFINITION: los defaults y CHECK volverian NULL.")
        return 1

    # --- columnas -----------------------------------------------------------
    c.execute("""
        SELECT t.name, col.name, ty.name, col.max_length, col.precision, col.scale,
               col.is_nullable, col.is_identity, d.definition
          FROM sys.columns col
          JOIN sys.tables t ON t.object_id = col.object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
          JOIN sys.types ty ON ty.user_type_id = col.user_type_id
          LEFT JOIN sys.default_constraints d ON d.object_id = col.default_object_id
         WHERE s.name = %s
         ORDER BY t.name, col.column_id
    """, (ESQUEMA,))
    columnas = defaultdict(list)
    for t, nom, tipo, largo, prec, esc, nulo, ident, deflt in c.fetchall():
        columnas[t].append((nom, tipo, largo, prec, esc, nulo, ident, deflt))

    # --- llaves foraneas ----------------------------------------------------
    c.execute("""
        SELECT tp.name, cp.name, tr.name, cr.name
          FROM sys.foreign_keys fk
          JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
          JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
          JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
          JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
          JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
          JOIN sys.schemas s ON s.schema_id = tp.schema_id
         WHERE s.name = %s
    """, (ESQUEMA,))
    fks = defaultdict(list)
    for tp, cp, tr, cr in c.fetchall():
        fks[tp].append((cp, tr, cr))

    # --- indices ------------------------------------------------------------
    c.execute("""
        SELECT t.name, i.name, i.is_unique, i.is_primary_key, col.name
          FROM sys.indexes i
          JOIN sys.tables t ON t.object_id = i.object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
          JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
          JOIN sys.columns col ON col.object_id = i.object_id AND col.column_id = ic.column_id
         WHERE s.name = %s AND i.type > 0
         ORDER BY t.name, i.name, ic.key_ordinal
    """, (ESQUEMA,))
    indices = defaultdict(lambda: defaultdict(lambda: {"unico": False, "pk": False, "cols": []}))
    for t, idx, uq, pk, col in c.fetchall():
        d = indices[t][idx]
        d["unico"], d["pk"] = bool(uq), bool(pk)
        d["cols"].append(col)

    # --- CHECK --------------------------------------------------------------
    c.execute("""
        SELECT t.name, cc.name, cc.definition
          FROM sys.check_constraints cc
          JOIN sys.tables t ON t.object_id = cc.parent_object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
         WHERE s.name = %s
    """, (ESQUEMA,))
    checks = defaultdict(list)
    for t, nom, defi in c.fetchall():
        checks[t].append((nom, defi))

    for tabla in sorted(columnas, key=str.lower):
        print(f"\n{'=' * 78}\n{tabla}\n{'=' * 78}")
        for nom, tipo, largo, prec, esc, nulo, ident, deflt in columnas[tabla]:
            if tipo in ("nvarchar", "nchar"):
                medida = f"({'MAX' if largo == -1 else largo // 2})"
            elif tipo in ("varchar", "char", "varbinary"):
                medida = f"({'MAX' if largo == -1 else largo})"
            elif tipo in ("decimal", "numeric"):
                medida = f"({prec},{esc})"
            else:
                medida = ""
            marcas = []
            if not nulo:
                marcas.append("NOT NULL")
            if ident:
                marcas.append("IDENTITY")
            if deflt:
                marcas.append(f"DEFAULT {deflt}")
            print(f"  {nom:<34}{tipo}{medida:<12}{'  '.join(marcas)}")
        for cp, tr, cr in fks.get(tabla, []):
            print(f"  FK   {cp} -> {tr}.{cr}")
        for idx, d in sorted(indices.get(tabla, {}).items()):
            clase = "PK" if d["pk"] else ("UNIQUE" if d["unico"] else "INDEX")
            print(f"  {clase:<6}{idx}  ({', '.join(d['cols'])})")
        for nom, defi in checks.get(tabla, []):
            print(f"  CHECK {nom}  {defi}")

    con.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
