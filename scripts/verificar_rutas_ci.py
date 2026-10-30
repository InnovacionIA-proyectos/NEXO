"""Verifica la convencion de rutas de CI/CD.

Responde una sola pregunta, sin tener que empujar un commit a GitHub para
averiguarlo: **si cambio este archivo, que pipelines se disparan?**

Reimplementa la semantica de `paths:` de GitHub Actions (glob con `*`, `**` y
negaciones `!`, donde gana el ULTIMO patron que matchea) y la aplica sobre los
workflows reales del repo, comparando contra lo que la convencion dice que
deberia pasar.

    python scripts/verificar_rutas_ci.py

Sale con codigo 1 si alguna expectativa de la convencion de Cubicador falla.
Los huecos conocidos en los OTROS dos pipelines se reportan como avisos, no
como fallas: son limitaciones aceptadas, no errores a corregir.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

RAIZ = Path(__file__).resolve().parent.parent
WORKFLOWS = RAIZ / ".github" / "workflows"


def _a_regex(patron: str) -> re.Pattern[str]:
    """Traduce un filter pattern de GitHub a regex.

    `**` matchea cualquier cosa incluyendo `/`; `*` matchea cualquier cosa
    MENOS `/`. El resto se escapa literal.
    """
    salida, i = "", 0
    while i < len(patron):
        if patron.startswith("**", i):
            salida += ".*"
            i += 2
        elif patron[i] == "*":
            salida += "[^/]*"
            i += 1
        else:
            salida += re.escape(patron[i])
            i += 1
    return re.compile(f"^{salida}$")


def dispara(patrones: list[str], ruta: str) -> bool:
    """Gana el ultimo patron que matchea; si ninguno matchea, no dispara."""
    veredicto = False
    for patron in patrones:
        negado = patron.startswith("!")
        if _a_regex(patron[1:] if negado else patron).match(ruta):
            veredicto = not negado
    return veredicto


def paths_de(workflow: str) -> list[str]:
    # `on` es un booleano en YAML 1.1, asi que safe_load lo devuelve como True.
    datos = yaml.safe_load((WORKFLOWS / f"{workflow}.yml").read_text(encoding="utf-8"))
    return datos[True]["push"]["paths"]


API, WORKER = "ci-cd-cubicador-api", "ci-cd-cubicador-worker"

# (ruta que cambia, dispara API?, dispara WORKER?, por que)
CONVENCION = [
    ("cubicador/api/dtos/plan_cargue.py", True, True,
     "bug 2: vive en api/ pero worker/main.py lo importa"),
    ("cubicador/api/dependencias.py", True, True,
     "worker/main.py importa NEXO_BASE_URL y NEXO_CALLBACK_API_KEY de aca"),
    ("cubicador/api/routers/planes_cargue.py", True, True,
     "solo lo usa la API, pero COPY . . lo mete en las dos imagenes"),
    ("cubicador/worker/main.py", True, True,
     "bug 1; ademas tests/worker/ corre en el gate de los DOS"),
    ("cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py", True, True,
     "bug 3: compartido de manera evidente"),
    ("cubicador/domain/motor/empaquetado.py", True, True, "motor compartido"),
    ("cubicador/application/plan_cargue/optimizar.py", True, True, "caso de uso compartido"),
    ("cubicador/tests/worker/test_mensaje_malformado.py", True, True,
     "pytest corre la suite entera en los dos pipelines"),
    ("cubicador/scripts/ci_docker_smoke.py", True, True, "el smoke corre en los dos"),
    ("cubicador/pyproject.toml", True, True,
     "unica config de pytest del repo (asyncio_mode, testpaths)"),
    ("cubicador/requirements.txt", True, True, "dependencias de las dos imagenes"),
    ("cubicador/Dockerfile", True, True, "el docker-smoke construye los dos contenedores"),
    ("cubicador/Dockerfile.worker", True, True, "idem"),
    ("cubicador/docker-compose.yml", True, True, "define api y worker en el mismo archivo"),
    ("docker-compose.yml", True, True,
     "el docker-smoke levanta el rabbit compartido desde aca"),
    ("build/Dockerfile.rabbit", True, True, "ese rabbit se construye con este Dockerfile"),
    ("cubicador/herramientas/generador_casos_dorados/generar.py", True, True,
     "por defecto, lo desconocido dispara los dos"),
    ("cubicador/docs/DECISIONES_TECNICAS.md", False, False,
     "documentacion: no cambia comportamiento"),
    ("cubicador/README.md", False, False, "documentacion"),
    ("cubicador/CONTRATO.md", False, False, "documentacion"),
    ("src/Nexo.Api/Program.cs", False, False, "API .NET: ajeno a Cubicador"),
    ("tests/Nexo.Api.Tests/Infra/ApiFixture.cs", False, False, "idem"),
]

# Huecos ya identificados en los pipelines de ci-cd-api y ci-cd-rabbitmq. Se
# reportan, no se exigen: son limitaciones aceptadas.
AVISOS = [
    ("ci-cd-api", "docker-compose.yml", True,
     "define el servicio `api` (.NET) pero solo dispara ci-cd-rabbitmq"),
    ("ci-cd-api", "tests/QA/una-hu/README.md", False,
     "`tests/**` incluye la documentacion de QA: un README despliega el API"),
    ("ci-cd-rabbitmq", "scripts/rabbitmq_smoke.py", True,
     "el workflow ejecuta este script pero no lo escucha"),
]


def main() -> int:
    api, worker = paths_de(API), paths_de(WORKER)

    exclusivos = set(api) ^ set(worker)
    esperados = {f".github/workflows/{API}.yml", f".github/workflows/{WORKER}.yml"}
    print("Convencion de Cubicador")
    print(f"  Los dos pipelines comparten {len(set(api) & set(worker))} patrones y se")
    print(f"  diferencian solo en el nombre de su propio archivo: {exclusivos == esperados}")
    print()
    print(f"  {'archivo que cambia':<62}{'API':^7}{'WORKER':^8}")
    print("  " + "-" * 77)

    fallas = 0
    for ruta, esp_api, esp_wkr, motivo in CONVENCION:
        real_api, real_wkr = dispara(api, ruta), dispara(worker, ruta)
        ok = (real_api, real_wkr) == (esp_api, esp_wkr)
        fallas += not ok
        marca = lambda b: "  SI  " if b else "  --  "
        print(f"  {ruta:<62}{marca(real_api):^7}{marca(real_wkr):^8}{'' if ok else '  << FALLA'}")
        if not ok:
            print(f"    esperado: API={esp_api} WORKER={esp_wkr} — {motivo}")

    print()
    print("Avisos (otros pipelines — limitaciones conocidas, no errores)")
    for workflow, ruta, esperado, motivo in AVISOS:
        real = dispara(paths_de(workflow), ruta)
        estado = "ok" if real == esperado else "PENDIENTE"
        print(f"  [{estado:^9}] {workflow}: {ruta}")
        print(f"              {motivo}")

    print()
    if fallas:
        print(f"{fallas} de {len(CONVENCION)} expectativas de la convencion FALLAN.")
        return 1
    print(f"Las {len(CONVENCION)} expectativas de la convencion se cumplen.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
