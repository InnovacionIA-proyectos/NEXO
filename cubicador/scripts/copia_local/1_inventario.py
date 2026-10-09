"""Inventario del esquema de origen: que tablas hay y cuantas filas tiene cada una.

No escribe nada. Sirve para dimensionar la copia antes de hacerla y para
comparar despues con el destino.

    cubicador/.venv/Scripts/python.exe scripts/copia_local/1_inventario.py
"""
from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import _conexion as cx  # noqa: E402

SALIDA = os.path.join(os.path.dirname(__file__), "inventario_origen.json")

CONSULTA = """
SELECT t.name AS tabla,
       SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS filas
  FROM sys.tables t
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.partitions p ON p.object_id = t.object_id
 WHERE s.name = %s
 GROUP BY t.name
 ORDER BY t.name
"""


def main() -> int:
    cfg = cx.origen()
    print(f"Origen: {cx.describir(cfg)}\n")
    try:
        con = cx.conectar(cfg)
    except Exception as e:  # noqa: BLE001
        print(f"No pude conectar: {e}")
        return 1

    with con, con.cursor() as cur:
        cur.execute(CONSULTA, (cfg["esquema"],))
        filas = cur.fetchall()

    if not filas:
        print(f"El esquema {cfg['esquema']} no tiene tablas (o el usuario no las ve).")
        return 1

    total = 0
    print(f"{'tabla':<42}{'filas':>12}")
    print("-" * 54)
    for tabla, n in filas:
        n = int(n or 0)
        total += n
        print(f"{tabla:<42}{n:>12,}")
    print("-" * 54)
    print(f"{len(filas)} tablas{'':<33}{total:>12,} filas en total")

    with open(SALIDA, "w", encoding="utf-8") as f:
        json.dump({t: int(n or 0) for t, n in filas}, f, indent=2, ensure_ascii=False)
    print(f"\nGuardado en {os.path.relpath(SALIDA, cx.RAIZ)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
