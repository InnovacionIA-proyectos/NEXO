# PR #335 (DEPLOY) — Ajuste del CI/CD

## Qué cambia realmente

El diff de GitHub mostraba ~120 archivos porque la rama `DEPLOY` estaba muy
desactualizada respecto a `QA` (no tenía las PRs #328-#334). El contenido
REAL y único de `DEPLOY` que no estaba ya en `QA` eran estos cambios
(confirmados con `git diff origin/QA origin/DEPLOY` por archivo, no solo
leyendo el único commit nuevo — un error que cometí al principio: asumí que
`2885b51` era la única diferencia y no revisé el resto del diff con el mismo
cuidado, ver "Hallazgo" abajo):

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
4. **`clsHechoConHistoricoSapRealAttribute.cs`** (nuevo): mismo patrón que
   `clsHechoConCubicadorRealAttribute` — salta explícitamente
   `clsHistoricoSapPaginacionTests` cuando `tblSAPTarifasLog` no tiene
   suficientes filas reales, en vez de fallar. Resuelve exactamente el
   problema de datos del ambiente que diagnostiqué en las verificaciones de
   las PRs #333 y #334 (CI del 2026-10-20: 10 esperadas, 0 recibidas).

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

## Hallazgo encontrado y corregido: duplicación por error de merge

Al mergear `DEPLOY` (que traía una versión más vieja de
`clsEditarLineaCotizacionCommand.cs`, previa a la PR #332) contra `QA`
(que ya tenía la versión nueva con `clsMoneda.Pesos`), git combinó ambas en
vez de detectar que eran el mismo cambio: el bloque de validación
`PISO_SICETAC_NO_VERIFICABLE` quedó **duplicado**, dos veces seguidas,
idéntico salvo el formato del monto (`clsMoneda.Pesos(...)` vs `:C0`).

- **Efecto en runtime**: ninguno — la primera copia siempre lanza la
  excepción antes de llegar a la segunda (código muerto, no un bug
  funcional).
- **Corrección**: eliminé el segundo bloque (el de formato viejo `:C0`),
  commit `bb6130d` directo sobre `QA` (autorizado por Kevin), ya que el
  bug lo introdujo mi propio merge, no una PR de un desarrollador.
- **Verificación de la corrección**: `dotnet build` compila correcto;
  reemprendí la suite completa tras el fix → **Domain 203/203, Application
  521/521, Api 707/717, 0 errores, 10 omitidas** (incluyen los 2 nuevos
  skips explícitos de `clsHechoConHistoricoSapRealAttribute`, que antes de
  este fix de PR #335 fallaban con el error de datos ya diagnosticado).
- **Barrido de seguridad**: busqué otros bloques de comentario/código
  repetidos en los demás archivos que tocó el merge (`clsBuscarSapHistoricoQuery.cs`,
  `clsSapHistoricoAdapter.cs`, `Enums.cs`, `clsResolucionRolPortal.cs`,
  `AlertasDeNegocioCommand.cs`) — sin otras duplicaciones.

## Resultado

Sin hallazgos bloqueantes adicionales. La URL de despliegue documentada
ahora es correcta y verificada en vivo; las 2 mejoras de CI del Cubicador
son seguras y de bajo riesgo; la duplicación introducida por el merge ya
está corregida y la suite completa queda en 0 errores.
