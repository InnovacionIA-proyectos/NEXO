"""Regenera `docs/openapi.json` desde la app (S5-HU-17.4).

Hasta ahora ese archivo se actualizaba a mano, asi que podia quedar diciendo
cosas que la API ya no hacia - justo lo que no puede pasar con un contrato que
programan dos equipos de afuera.

Correr despues de cambiar rutas o DTOs:

    ./.venv/Scripts/python.exe cubicador/scripts/generar_openapi.py

Con `--verificar` no escribe: devuelve 1 si el archivo quedo desactualizado.
Sirve para CI.

No necesita base ni broker: `app.openapi()` se arma de las anotaciones, sin
levantar el `lifespan`.
"""

from __future__ import annotations

import argparse
import json
import os
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, RAIZ)

# La app lee estas variables al importarse. Valores de relleno: aca no se
# atiende ninguna peticion, solo se lee el esquema.
os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")
os.environ.setdefault("CUBICADOR_API_KEY", "generar-openapi")

DESTINO = os.path.join(RAIZ, "docs", "openapi.json")


def esquema() -> str:
    from api.main import app

    return json.dumps(app.openapi(), indent=2, ensure_ascii=False, sort_keys=True) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verificar", action="store_true",
                        help="No escribe; falla si docs/openapi.json esta desactualizado.")
    args = parser.parse_args()

    generado = esquema()

    if args.verificar:
        actual = ""
        if os.path.exists(DESTINO):
            with open(DESTINO, encoding="utf-8") as f:
                actual = f.read()
        if actual == generado:
            print("docs/openapi.json esta al dia.")
            return 0
        print("docs/openapi.json NO corresponde a la API actual. "
              "Correr scripts/generar_openapi.py.")
        return 1

    with open(DESTINO, "w", encoding="utf-8") as f:
        f.write(generado)
    rutas = json.loads(generado)["paths"]
    print(f"docs/openapi.json regenerado: {len(rutas)} rutas.")
    for ruta in sorted(rutas):
        metodos = ", ".join(sorted(m.upper() for m in rutas[ruta]))
        print(f"  {metodos:18s} {ruta}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
