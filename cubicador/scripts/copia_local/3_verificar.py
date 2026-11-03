"""Compara el origen contra la copia local, tabla por tabla.

Es la prueba de que la copia quedo completa. No escribe en ninguno de los dos.

    cubicador/.venv/Scripts/python.exe scripts/copia_local/3_verificar.py
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import _conexion as cx  # noqa: E402

# Se omiten a proposito en 2_copiar.py: no son parte del estado funcional.
ESPERADAS_VACIAS = {"__EFMigrationsHistory_backup_20260926", "tbl_audit_log"}

# Obsoleta: era una cache local alimentada por import de Excel, retirada en el
# issue #179 y sacada a proposito de ESQUEMA_NEXO.sql. El codigo ya no la lee;
# las filas que quedan en el servidor son residuo. No debe estar en la copia.
ESPERADAS_AUSENTES = {"tbl_tarifas_sicetac"}

CONTEO = """
SELECT t.name,
       SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END)
  FROM sys.tables t
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.partitions p ON p.object_id = t.object_id
 WHERE s.name = %s
 GROUP BY t.name
"""


def conteos(cfg) -> dict[str, int]:
    con = cx.conectar(cfg)
    with con, con.cursor() as cur:
        cur.execute(CONTEO, (cfg["esquema"],))
        return {t: int(n or 0) for t, n in cur.fetchall()}


def main() -> int:
    org, dst = cx.origen(), cx.destino()
    o, d = conteos(org), conteos(dst)

    print(f"Origen : {cx.describir(org)}")
    print(f"Destino: {cx.describir(dst)}\n")
    print(f"{'tabla':<42}{'origen':>10}{'destino':>10}  estado")
    print("-" * 78)

    iguales = problemas = omitidas = 0
    for tabla in sorted(set(o) | set(d)):
        no, nd = o.get(tabla), d.get(tabla)
        if tabla in ESPERADAS_VACIAS:
            estado, omitidas = "omitida a proposito", omitidas + 1
        elif tabla in ESPERADAS_AUSENTES and nd is None:
            estado, omitidas = "obsoleta: no va en la copia", omitidas + 1
        elif no is None:
            estado, problemas = "SOLO EN DESTINO", problemas + 1
        elif nd is None:
            estado, problemas = "FALTA EN DESTINO", problemas + 1
        elif no == nd:
            estado, iguales = "ok", iguales + 1
        else:
            estado, problemas = f"DIFIERE ({nd - no:+,})", problemas + 1
        print(f"{tabla:<42}{(no if no is not None else '-'):>10}"
              f"{(nd if nd is not None else '-'):>10}  {estado}")

    print("-" * 78)
    print(f"{iguales} iguales, {omitidas} omitidas a proposito, {problemas} con diferencias")
    if problemas == 0:
        print("\nLa copia esta completa.")
    return 1 if problemas else 0


if __name__ == "__main__":
    raise SystemExit(main())
