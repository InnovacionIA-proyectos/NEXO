"""Aplica un script .sql contra dbo_cubicador, partiendo por GO.

pymssql no entiende `GO` (no es SQL, es separador de lotes de las herramientas
de Microsoft), asi que hay que partir el archivo y mandar cada lote aparte.

    cubicador/.venv/Scripts/python.exe scripts/aplicar_sql.py <archivo.sql>
    cubicador/.venv/Scripts/python.exe scripts/aplicar_sql.py <archivo.sql> --simular
"""
from __future__ import annotations

import os
import re
import sys

import pymssql

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "copia_local"))
import _conexion as cx  # noqa: E402


def conectar():
    env = cx._leer_env(os.path.join(cx.RAIZ, "cubicador", ".env"))
    m = re.match(r"mssql\+aioodbc://([^:]+):([^@]+)@([^:]+):(\d+)/([^?]+)", env["DATABASE_URL"])
    u, pw, host, puerto, base = m.groups()
    return pymssql.connect(server=host, port=int(puerto), user=u, password=pw,
                           database=base, timeout=120, login_timeout=30, autocommit=True)


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    ruta = sys.argv[1]
    simular = "--simular" in sys.argv

    with open(ruta, encoding="utf-8") as f:
        sql = f.read()

    lotes = [b.strip() for b in re.split(r"^\s*GO\s*$", sql, flags=re.M | re.I)]
    lotes = [b for b in lotes if b and not all(l.strip().startswith("--") or not l.strip()
                                               for l in b.splitlines())]

    print(f"{os.path.basename(ruta)}: {len(lotes)} lotes con sentencias")
    if simular:
        for i, lote in enumerate(lotes, 1):
            primera = next((l for l in lote.splitlines() if l.strip()
                            and not l.strip().startswith("--")), "")
            print(f"  {i}. {primera.strip()[:90]}")
        print("\n(simulacion: no se ejecuto nada)")
        return 0

    con = conectar()
    cur = con.cursor()
    for i, lote in enumerate(lotes, 1):
        primera = next((l for l in lote.splitlines() if l.strip()
                        and not l.strip().startswith("--")), "")
        try:
            cur.execute(lote)
            try:
                filas = cur.fetchall()
                if filas:
                    for f in filas:
                        print(f"     {' | '.join(str(x) for x in f)}")
            except Exception:
                pass
            print(f"  OK    {i}. {primera.strip()[:80]}")
        except Exception as e:  # noqa: BLE001
            print(f"  FALLA {i}. {primera.strip()[:80]}")
            print(f"        {str(e)[:300]}")
            con.close()
            return 1
    con.close()
    print("\nAplicado.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
