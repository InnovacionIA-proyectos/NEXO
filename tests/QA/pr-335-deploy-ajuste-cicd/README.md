# PR #335 (DEPLOY) — Ajuste del CI/CD

## Qué cambia realmente

El diff de GitHub mostraba ~120 archivos porque la rama `DEPLOY` estaba muy
desactualizada respecto a `QA` (no tenía las PRs #328-#334). El contenido
REAL y único de esta PR (comparado contra el punto de divergencia real,
commit `2885b51`) son 4 cambios:

1. **URL de Dokploy actualizada** en `docs/archivo/VERIFICACION_NEXO_CUBICADOR_2026-09-14.md`
   y `docs/despliegue/CHECKLIST_DESPLIEGUE.md`: de
   `http://nexo-predesplieguebackend-ijaiqn-cdc194-10-10-40-17.sslip.io` a
   `http://nexodeploy-backend-lkcbu5-b5dba3-10-10-40-17.sslip.io`.
2. **`cubicador/docker-compose.yml`**: el servicio de inicialización de base
   local ahora aplica el DDL del motor 3D (`ESQUEMA_CUBICADOR.sql`) si las
   tablas no existen — corrige que el smoke test de CI nunca pasaba por
   `503 Invalid object name 'dbo_cubicador.vehiculos_cubicacion'`. Explícitamente
   acotado a los ambientes de desarrollo/CI local (en QA/producción esas
   tablas ya existen).
3. **`cubicador/scripts/ci_docker_smoke.py`**: relaja la aserción de
   `len(vehiculos) == 9` a `>= 9` — el catálogo de presets crece con el
   tiempo y un `==` fijo rompía el CI cada vez que se agregaba uno.

## Qué se verificó

**Diff real aislado**: confirmé con `git log`/`git show` que el único commit
nuevo de `DEPLOY` sobre su base real era `2885b51` (solo las 2 URLs); los
otros ~120 archivos del diff de GitHub ya estaban en `QA` desde las PRs
#328-#334, y `diff` directo entre `origin/QA` y `origin/DEPLOY` para
`docker-compose.yml`/`ci_docker_smoke.py` confirmó las 2 mejoras de CI que sí
eran exclusivas de `DEPLOY` y nunca habían llegado a `QA`.

**Verificación en vivo de la URL nueva** (no me quedé con el texto del
documento):
- `http://nexodeploy-backend-lkcbu5-b5dba3-10-10-40-17.sslip.io/health` → `200`.
- `.../swagger/v1/swagger.json` → `200`.
- La URL vieja (`nexo-predesplieguebackend-...`) → `502` (ya no es el
  despliegue activo).

**Revisión de las 2 mejoras de CI**: ambas acotadas correctamente a
ambientes locales/CI (comentarios explícitos de que no aplican a QA/producción),
bajo riesgo, no tocan código C#/Python de producción.

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR) — confirma que no hay
cambio de código .NET.

## Nota operativa importante

GitHub marcó la PR como conflictiva (`CONFLICTING`) pese a que el merge
local con `QA` era limpio (confirmado dos veces). A diferencia de las PRs
#333/#334 (ramas de feature normales), esta PR usa `DEPLOY` como rama
cabeza: empujar el merge resuelto a `origin/DEPLOY` para destrabar el
merge SÍ dispara el pipeline real de despliegue
(`ci-cd-api.yml`/`ci-cd-cubicador-*.yml` corren en push a `DEPLOY`). Antes
de hacerlo, se lo consulté explícitamente a Kevin y autorizó el push. Hecho
esto, la PR quedó mergeable y se cerró con `gh pr merge`.

## Resultado

Sin hallazgos. La URL de despliegue documentada ahora es correcta y
verificada en vivo; las 2 mejoras de CI del Cubicador son seguras y de
bajo riesgo.
