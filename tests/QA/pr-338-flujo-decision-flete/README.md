# QA — PR #338: Flujo de decisión del flete (guardado automático, tramos vacíos, vehículo relajado)

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/338
**Autor**: ivasco-dev
**Head → Base**: `DEPLOY` → `QA` (particularidad: la PR trae como head la propia rama `DEPLOY`, no una rama de feature)

## Qué cambia

- El flete se guarda solo cuando hay valor y ningún motivo de pendiente, también en proceso completo.
- Retorno del contenedor y levante se calculan en el backend (SICE-TAC vacío, 1h + 1h, misma unidad de transporte); sin piso, la línea queda pendiente.
- Devolución de contenedor: viaje vacío y plaza filtrada por consolidado, exclusivo y clase de contenedor.
- Candidatos con vehículo relajado: informativos, no entran al recomendado.
- `GET /aprovisionamiento` acepta `carroceria`; la propuesta trae `aprovisionamientoContenedor`, `autocosteable` y `vehiculoRelajado`.
- Cambios incompatibles: una línea de proceso completo con valor ya no queda pendiente (se guarda sola); `valorAprovisionamiento` de la captura se ignora; con retorno o levante, la carrocería es obligatoria fuera de contenedor.
- Sin cambios de esquema de BD.

## Por qué se verificó así

El propio autor dejó pendiente en la descripción de la PR "revalidar la suite completa de API con la red a la BD estable" — es decir, no había corrido `Nexo.Api.Tests` completo contra la base de datos real antes de abrir la PR. Esa era la pieza que faltaba verificar.

Como el head de la PR es la rama `DEPLOY` (no una rama de feature), antes de revisar el código se comparó qué archivos toca cada rama desde el merge-base real (`cd10700`) contra los commits que `QA` ya tiene de más (el fix de duplicación de código de la PR #335): no hay archivos en común, así que el riesgo de duplicación por merge de historias divergentes (lo que pasó en la PR #335) era bajo, y se confirmó que no se repitió.

## Cómo se verificó

1. `git checkout -B pr338 origin/DEPLOY` + `git merge origin/QA --no-ff` → limpio, sin conflictos.
2. Revisión de código línea por línea contra el diff real (`git diff cd10700 origin/DEPLOY`) de los 30 archivos tocados, con foco en:
   - `clsReglaDeCosteo.DecidirFlujo` (dominio del guardado automático).
   - `clsCostearLineaCommand.cs` / `clsRecostearLineaCommand.cs` (cálculo SICE-TAC vacío 1h+1h).
   - `clsPropuestaTarifaAdapter.cs` (vehículo relajado, filtro de plaza por consolidado/exclusivo/clase de contenedor).
   - `clsCapturarSolicitudCommand.cs` / `clsResolverCatalogosDeLineasCommand.cs` (que `valorAprovisionamiento` de captura se ignore).
3. `dotnet build Nexo.Clean.sln -c Release` → 0 errores.
4. `dotnet test tests/Nexo.Domain.Tests` → **218/218** ✅ (igual a lo reportado por el autor).
5. `dotnet test tests/Nexo.Application.Tests` → **524/524** ✅ (igual a lo reportado por el autor).
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real (`.env.pruebas-reales.local`, `TEST_DB_CONNECTION` + `Database__Esquema` + el resto de connection strings exportadas) → **728/738**, 0 con error, **10 omitidos**, todos esperados:
   - 6 E2E de Cubicador (`clsCatalogoCubicadorPassthroughE2ETests`, `clsCubicacionControllerE2ETests`) — esta PR no toca el Cubicador, no se levantó local para esta verificación.
   - 2 de `clsMailSenderRealTests` — necesitan relay SMTP real.
   - 2 de `clsHistoricoSapPaginacionTests` — `tblSAPTarifasLog` está en 0 filas en este ambiente desde la PR #335 (dato, no bug).
7. Barrido de duplicación de código post-merge (`awk` buscando líneas repetidas >40 caracteres en los 4 archivos de producción más grandes tocados) → sin hallazgos preocupantes (solo coincidencias cortas de patrones reutilizados entre métodos helper similares, no bloques completos repetidos).

## Verificación de valores exactos

Los propios tests nuevos de `clsFlujoDeDecisionRealTests.cs`, corridos contra la BD real, ya hacen la doble verificación de valor exacto en vez de solo "responde bien":

- `El_retorno_del_contenedor_sale_de_la_unidad_de_la_linea_y_se_guarda_con_el_flete`: cuando hay piso, compara `retorno` contra `r.FilaCotizacion.GetProperty("valorRetornoContenedor").GetDecimal()` cifra por cifra.
- `Un_levante_sin_piso_del_tramo_vacio_deja_la_linea_pendiente_y_no_guarda_nada`: con una ciudad de levante inexistente, confirma que la línea queda `porCostear`, con el motivo de pendiente correcto, y que **no se guardó nada** (`Assert.Null(r.FilaCotizacion)`), no un valor en cero o nulo silencioso.

## Resultado

Sin hallazgos bloqueantes. PR aprobada y mergeada a `QA` el 2026-10-23.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-338-flujo-decision-flete/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo y VPN corporativa conectada (acceso a `10.10.40.85`).
