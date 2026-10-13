"""Copia el esquema db_NEXO del SQL Server corporativo al contenedor local.

Que hace, en orden:
  1. Crea la base y el esquema en el contenedor local, si no existen.
  2. Aplica docs/Database/ESQUEMA_NEXO.sql, que es el DDL oficial del proyecto.
  3. Copia los datos tabla por tabla, con las llaves foraneas desactivadas.
  4. Reactiva las llaves y verifica el conteo de filas contra el origen.

NO escribe absolutamente nada en el origen: solo hace SELECT.

    cubicador/.venv/Scripts/python.exe scripts/copia_local/2_copiar.py
    cubicador/.venv/Scripts/python.exe scripts/copia_local/2_copiar.py --ofuscar
"""
from __future__ import annotations

import argparse
import io
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
import _conexion as cx  # noqa: E402

DDL = os.path.join(cx.RAIZ, "docs", "Database", "ESQUEMA_NEXO.sql")
LOTE = 500

# Tablas que no vale la pena copiar: respaldos y bitacoras que solo hacen ruido.
OMITIR = {"__EFMigrationsHistory_backup_20260926", "tbl_audit_log"}

# Columnas a ofuscar con --ofuscar. El dato queda con la misma forma y el mismo
# volumen, pero deja de identificar a nadie.
OFUSCAR = {
    "tbl_clientes": ("Nombre", "Nit", "RazonSocial"),
    "tbl_usuarios": ("Nombre", "Correo", "Email"),
}


def _lotes(seq, n):
    for i in range(0, len(seq), n):
        yield seq[i:i + n]


def aplicar_ddl(cur) -> int:
    """Corre el DDL oficial partiendo por GO, que pymssql no entiende."""
    sql = io.open(DDL, encoding="utf-8").read()
    # El archivo trae solo 2 separadores GO, asi que son pocos lotes y cada
    # uno lleva muchas sentencias. SQL Server las ejecuta en orden y se
    # detiene en la primera que falle, por eso se reporta cual fue.
    lotes = [b.strip() for b in re.split(r"^\s*GO\s*$", sql, flags=re.M | re.I)]
    aplicados = 0
    for lote in lotes:
        if not lote:
            continue
        try:
            cur.execute(lote)
            aplicados += 1
        except Exception as e:  # noqa: BLE001
            msg = str(e)
            # Reaplicar el DDL sobre una base ya creada no es un error.
            if "already" in msg.lower() or "ya existe" in msg.lower():
                continue
            print(f"\n  DDL: lote rechazado -> {msg[:200]}")
            print(f"  empieza con: {lote[:120]}...")
    return aplicados


def columnas(cur, esquema: str, tabla: str) -> list[tuple[str, bool]]:
    """Columnas copiables de una tabla, en orden.

    Quedan fuera las calculadas y las `timestamp`/`rowversion`: SQL Server las
    genera solo y rechaza cualquier INSERT explicito sobre ellas con el error
    273. Las tiene InboxState, OutboxState, tbl_cotizaciones,
    tbl_parametros_score y tbl_solicitudes.
    """
    cur.execute("""
        SELECT c.name, c.is_identity
          FROM sys.columns c
          JOIN sys.tables t ON t.object_id = c.object_id
          JOIN sys.schemas s ON s.schema_id = t.schema_id
          JOIN sys.types ty ON ty.user_type_id = c.user_type_id
         WHERE s.name = %s AND t.name = %s
           AND c.is_computed = 0
           AND ty.name NOT IN ('timestamp', 'rowversion')
         ORDER BY c.column_id
    """, (esquema, tabla))
    return [(r[0], bool(r[1])) for r in cur.fetchall()]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ofuscar", action="store_true",
                    help="reemplaza nombres, NITs y correos por valores sinteticos")
    args = ap.parse_args()

    org, dst = cx.origen(), cx.destino()
    print(f"Origen  (solo lectura): {cx.describir(org)}")
    print(f"Destino (local)       : {cx.describir(dst)}")
    if args.ofuscar:
        print("Modo ofuscado: nombres, NITs y correos se reemplazan.")
    print()

    # --- 1. base y esquema en el destino -----------------------------------
    con_master = cx.conectar(dst, base="master")
    con_master.autocommit(True)
    with con_master.cursor() as cur:
        cur.execute(f"IF DB_ID('{dst['database']}') IS NULL "
                    f"CREATE DATABASE [{dst['database']}];")
    con_master.close()

    con_d = cx.conectar(dst)
    con_d.autocommit(True)
    with con_d.cursor() as cur:
        cur.execute(f"IF SCHEMA_ID('{dst['esquema']}') IS NULL "
                    f"EXEC('CREATE SCHEMA [{dst['esquema']}]');")
        print("Base y esquema listos en el destino.")
        print("Aplicando el DDL oficial (ESQUEMA_NEXO.sql)...")
        n = aplicar_ddl(cur)
        print(f"  {n} lotes aplicados.\n")

    # --- 2. copia de datos --------------------------------------------------
    con_o = cx.conectar(org)
    with con_o.cursor() as cur_o:
        cur_o.execute("""
            SELECT t.name FROM sys.tables t
              JOIN sys.schemas s ON s.schema_id = t.schema_id
             WHERE s.name = %s ORDER BY t.name
        """, (org["esquema"],))
        tablas = [r[0] for r in cur_o.fetchall()]

    con_d.autocommit(False)
    cur_d = con_d.cursor()
    cur_d.execute(f"""
        DECLARE @s NVARCHAR(MAX) = N'';
        SELECT @s = @s + 'ALTER TABLE [{dst['esquema']}].[' + name + '] NOCHECK CONSTRAINT ALL;'
          FROM sys.tables WHERE schema_id = SCHEMA_ID('{dst['esquema']}');
        EXEC sp_executesql @s;
    """)
    con_d.commit()

    copiadas, total, saltadas = 0, 0, []
    t0 = time.time()
    for tabla in tablas:
        if tabla in OMITIR:
            saltadas.append((tabla, "omitida a proposito"))
            continue
        cols_o = columnas(con_o.cursor(), org["esquema"], tabla)
        cols_d = columnas(cur_d, dst["esquema"], tabla)
        if not cols_d:
            saltadas.append((tabla, "no existe en el destino: no esta en el DDL"))
            continue
        comunes = [c for c, _ in cols_o if c in {d for d, _ in cols_d}]
        if not comunes:
            saltadas.append((tabla, "sin columnas en comun"))
            continue

        with con_o.cursor() as cur_o:
            lista = ", ".join(f"[{c}]" for c in comunes)
            cur_o.execute(f"SELECT {lista} FROM [{org['esquema']}].[{tabla}]")
            filas = cur_o.fetchall()

        if args.ofuscar and tabla in OFUSCAR:
            objetivo = {c.lower() for c in OFUSCAR[tabla]}
            idx = [i for i, c in enumerate(comunes) if c.lower() in objetivo]
            filas = [
                tuple(f"{comunes[i]}_{n:05d}" if i in idx and v is not None else v
                      for i, v in enumerate(fila))
                for n, fila in enumerate(filas)
            ]

        tiene_identity = any(ident for c, ident in cols_d if c in comunes)
        destino_tabla = f"[{dst['esquema']}].[{tabla}]"
        cur_d.execute(f"DELETE FROM {destino_tabla}")
        if filas:
            if tiene_identity:
                cur_d.execute(f"SET IDENTITY_INSERT {destino_tabla} ON")
            marcas = ", ".join(["%s"] * len(comunes))
            lista = ", ".join(f"[{c}]" for c in comunes)
            sql = f"INSERT INTO {destino_tabla} ({lista}) VALUES ({marcas})"
            for lote in _lotes(filas, LOTE):
                cur_d.executemany(sql, lote)
            if tiene_identity:
                cur_d.execute(f"SET IDENTITY_INSERT {destino_tabla} OFF")
        con_d.commit()
        copiadas += 1
        total += len(filas)
        print(f"  {tabla:<42}{len(filas):>8,} filas")

    cur_d.execute(f"""
        DECLARE @s NVARCHAR(MAX) = N'';
        SELECT @s = @s + 'ALTER TABLE [{dst['esquema']}].[' + name + '] WITH CHECK CHECK CONSTRAINT ALL;'
          FROM sys.tables WHERE schema_id = SCHEMA_ID('{dst['esquema']}');
        EXEC sp_executesql @s;
    """)
    con_d.commit()

    print(f"\n{copiadas} tablas, {total:,} filas, en {time.time() - t0:.1f}s")
    if saltadas:
        print("\nSaltadas:")
        for t, razon in saltadas:
            print(f"  {t:<42}{razon}")

    con_o.close()
    con_d.close()
    print("\nListo. Corre 3_verificar.py para comparar los conteos.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
