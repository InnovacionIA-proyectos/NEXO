"""Verificacion de la PR #452: "fix para recomendacion del Cubicador ya no vuelve vacia".

QUE VERIFICA
Corrige la issue #441 (mal diagnosticada en su titulo como una variable de entorno
ausente: la llave y el workspace del Cubicador estaban bien). El bug real: la
advertencia y el motivo_sin_seleccion del motor entran al mensaje que se le da al
modelo, y las INSTRUCCIONES del prompt piden nombrar esa cifra en una frase (ej. "el
75" de "Vehiculo(s) 1 por debajo del 75% de llenado"). Como `cifras_permitidas` no
declaraba esas cifras, `verificar()` las tomaba por inventadas y descartaba la
explicacion ENTERA (no solo la frase). Con el umbral de llenado en 75%, cualquier
carga que lo cruzara se quedaba sin texto de explicacion.

Fix: `cifras_permitidas` ahora incluye tambien las cifras de `advertencias` y
`motivo_sin_seleccion`, extraidas con `valores_del_texto()` -- la MISMA funcion
`_cifras()` que usa `verificar()` internamente, para que ninguna cifra se cuele por
una diferencia de formato entre como se genera el texto y como se extraen los
numeros.

COMO SE VERIFICO EN VIVO
1. Diff linea por linea de `prompt.py`/`verificacion.py`: confirmado que antes de
   esta PR `cifras_permitidas` no tocaba `advertencias` ni `motivo_sin_seleccion`, y
   que la nueva funcion `valores_del_texto()` llama al mismo `_cifras()` privado que
   usa `verificar()`.
2. Reproduccion real con 2 worktrees (`origin/QA` vs `pr452`) y el mismo caso exacto
   de la issue #441 (advertencia "75% de llenado"):
   - OLD (origin/QA): la cifra legitima del 75% se RECHAZA (bug reproducido en
     limpio, cifras_inventadas=('75',)).
   - NEW (pr452): la misma cifra se ACEPTA (explicacion ya no se descarta).
   - En AMBAS ramas, una cifra realmente inventada (999%, que no sale de ningun
     dato del motor) se sigue RECHAZANDO -- la garantia de seguridad no se relajo.
3. Suite completa de Cubicador sobre `pr452`: `pytest` -> 424 passed, 1 skipped (el
   mismo skip preexistente, no relacionado), 0 fallos.
4. Confirmado que la PR trae su propio test
   `test_declarar_esas_cifras_no_abre_la_puerta_a_las_demas`, que vigila
   especificamente que declarar estas cifras nuevas no abre la puerta a cualquier
   otra.
5. Sin comentarios adicionales en la PR de GitHub (gh api .../issues/452/comments
   vacio).

LIMITACIONES CONOCIDAS
- No se corrio contra el modelo de IA real (no hacia falta: el bug y el fix son
  puramente de verificacion determinista de texto, no de generacion).
"""

import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
TMP_ROOT = Path(r"C:\tmp\pr452-verify-ci")


def log(msg: str) -> None:
    print(f"[pr452] {msg}", flush=True)


def preparar_worktrees() -> tuple[Path, Path]:
    if TMP_ROOT.exists():
        shutil.rmtree(TMP_ROOT, ignore_errors=True)
    old = TMP_ROOT / "old"
    new = TMP_ROOT / "new"
    subprocess.run(["git", "-C", str(REPO_ROOT), "fetch", "origin", "QA", "pull/452/head:pr452-ci"],
                   check=True, capture_output=True)
    subprocess.run(["git", "-C", str(REPO_ROOT), "worktree", "add", str(old), "origin/QA", "--detach"],
                   check=True, capture_output=True)
    subprocess.run(["git", "-C", str(REPO_ROOT), "worktree", "add", str(new), "pr452-ci", "--detach"],
                   check=True, capture_output=True)
    return old, new


def limpiar_worktrees(old: Path, new: Path) -> None:
    for wt in (old, new):
        subprocess.run(["git", "-C", str(REPO_ROOT), "worktree", "remove", "--force", str(wt)],
                        capture_output=True)
    subprocess.run(["git", "-C", str(REPO_ROOT), "branch", "-D", "pr452-ci"], capture_output=True)
    shutil.rmtree(TMP_ROOT, ignore_errors=True)


_SCRIPT_RAMA = '''
import sys, json
from datetime import datetime, timezone
from decimal import Decimal

from domain.entidades.recomendacion import RecomendacionGuardada, VehiculoDeRecomendacion
from application.explicacion.prompt import cifras_permitidas
from application.explicacion.verificacion import verificar

vehiculo = VehiculoDeRecomendacion(
    id="v1", numero=1, codigo_vehiculo="2L2", nombre_vehiculo="Turbo",
    codigo_carroceria="FURGON", peso_total_kg=Decimal("1000"),
    volumen_total_m3=Decimal("10"), porcentaje_llenado_volumen=Decimal("40"),
    porcentaje_llenado_peso=Decimal("30"), alerta_bajo_llenado=True,
    heterogenea=False, mercancias=(),
)
reco = RecomendacionGuardada(
    id="r1", id_linea_solicitud="l1", origen_sistema="nexo", resultado="ok",
    motivo_sin_seleccion=None, condicionada=False, mercancias_condicionadas=(),
    vehiculos=(vehiculo,),
    advertencias=("Vehiculo(s) 1 por debajo del 75% de llenado",),
    extradimension=None, created_at=datetime.now(timezone.utc),
)
permitidas = cifras_permitidas(reco)

legitimo = verificar(
    "El vehiculo 1 quedo por debajo del 75% de llenado, segun la advertencia del motor.",
    permitidas)
inventado = verificar(
    "El vehiculo 1 va al 999% de llenado, una cifra que nadie calculo.",
    permitidas)
print(json.dumps({"legitimo_fiel": legitimo.fiel, "inventado_fiel": inventado.fiel}))
'''


def probar_rama(cubicador_path: Path) -> dict:
    import json
    resultado = subprocess.run(
        [sys.executable, "-c", _SCRIPT_RAMA], cwd=str(cubicador_path),
        capture_output=True, text=True)
    if resultado.returncode != 0:
        raise RuntimeError(f"fallo ejecutando en {cubicador_path}: {resultado.stderr}")
    return json.loads(resultado.stdout.strip().splitlines()[-1])


def main() -> int:
    fallo = False
    log("preparando worktrees (origin/QA vs PR #452)...")
    old, new = preparar_worktrees()
    try:
        log("probando rama OLD (origin/QA) -- debe reproducir el bug de la #441...")
        r_old = probar_rama(old / "cubicador")
        log(f"  legitimo_fiel={r_old['legitimo_fiel']} (esperado False), "
            f"inventado_fiel={r_old['inventado_fiel']} (esperado False)")
        if r_old["legitimo_fiel"] is not False:
            log("  FALLO: la rama OLD no reproduce el bug (deberia rechazar la cifra legitima)")
            fallo = True

        log("probando rama NEW (pr452) -- debe arreglar el bug sin relajar la garantia...")
        r_new = probar_rama(new / "cubicador")
        log(f"  legitimo_fiel={r_new['legitimo_fiel']} (esperado True), "
            f"inventado_fiel={r_new['inventado_fiel']} (esperado False)")
        if r_new["legitimo_fiel"] is not True:
            log("  FALLO: la rama NEW sigue rechazando la cifra legitima de la advertencia")
            fallo = True
        if r_new["inventado_fiel"] is not False:
            log("  FALLO: la rama NEW acepta una cifra inventada -- se relajo la garantia")
            fallo = True

        if not fallo:
            log("  OK: bug reproducido en OLD, arreglado en NEW, garantia intacta en NEW")

        log("corriendo pytest completo de Cubicador sobre pr452...")
        resultado = subprocess.run(
            [sys.executable, "-m", "pytest", "-q"], cwd=str(new / "cubicador"),
            capture_output=True, text=True)
        print(resultado.stdout[-2000:])
        if resultado.returncode != 0:
            log("  FALLO: pytest no paso limpio en pr452")
            fallo = True
        else:
            log("  OK: pytest paso limpio en pr452")
    finally:
        log("limpiando worktrees temporales...")
        limpiar_worktrees(old, new)

    print()
    if fallo:
        print("RESULTADO: HAY FALLOS")
        return 1
    print("RESULTADO: TODO OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
