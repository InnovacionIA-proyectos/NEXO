# Verificación QA — S2-HU-09.2 (issue #32, PR #74)

Rama: `32-s2-hu-092-set-de-datos-provisionales-realistas-validado-por-el-negocio`
(scalderon-bs). Verificación hecha antes de aprobar/mergear el PR #74.

## ⚠️ Bug real encontrado — ya corregido por el autor

El PR original afirmaba `108/108` en verde, cierto **en local** pero no en el CI
real del proyecto: el nuevo test `TarifasPlazaProvisionalesDatasetTests.cs` lee el
dataset directo de `docs/tarifas-plaza-provisionales/...json` (a propósito, sin
duplicarlo), pero `build/Dockerfile.ci` solo copiaba `src/` y `tests/` a la imagen —
confirmé entrando al contenedor que `docs/` no existía ahí. Corriendo la suite
dentro del CI real (`docker-compose.ci.yml`), las 13 pruebas nuevas fallaban con
`DirectoryNotFoundException`.

Se lo comuniqué al autor (con la corrección sugerida) y el commit `452a624` ya lo
resuelve: agrega `COPY docs/ docs/` a `build/Dockerfile.ci`. **Verificado tras el
fix**: `docs/` ya existe en la imagen y las 3 suites corren limpias — ver abajo.

De paso, ese mismo commit sacó `.claude/settings.json` (que había quedado
commiteado por accidente, con el hostname de un servidor interno) y lo agregó a
`.gitignore`.

## Verificación — reproducida de forma independiente, no solo leída

### Regresión, tras el fix del CI

```
docker compose -f docker-compose.ci.yml build --no-cache
docker compose -f docker-compose.ci.yml run --rm build     # 0 errores
docker compose -f docker-compose.ci.yml run --rm pruebas
```

| Suite | Resultado |
|---|---|
| `Domain.Tests` | **116/116** (103 preexistentes + 13 nuevas, todas en verde) |
| `Application.Tests` | 113/113 |
| `Api.Tests` | 89/89 (+2 saltadas por diseño, de otra HU) |

Confirmado además, entrando al contenedor construido: `docs/` sí existe dentro de
la imagen (`contrato-integracion`, `mensajeria`, `tarifas-plaza-provisionales`).

### Verificación de contenido del dataset — más allá de lo que ya prueba el autor

`TarifasPlazaProvisionalesDatasetTests.cs` confirma la FORMA del JSON (conteos por
categoría, que el archivo existe, que valores máximo>mínimo). Escribí
[`verify-tarifas-dataset.mjs`](verify-tarifas-dataset.mjs) para cruzar afirmaciones
CONCRETAS del README contra el contenido real del archivo — números y hechos
específicos que el autor documentó en prosa, no solo la estructura:

```
OK: núcleo = 150 filas
OK: casos borde = 60 filas (suma de las 6 categorías)
OK: 195 filas con sourceId, todas con Id real único (sin duplicados ni filas inventadas repetidas)
OK: las 30 filas de vigenciaVencida tienen Vigencia < 2026-08-27 (realmente vencidas)
OK: subCaso de vigenciaVencida reparte exacto 15 recien_vencida / 15 vencida_hace_mucho
OK: las 2 filas de vigenciaAbsurda son año 3999 exacto (coincide con el README)
OK: las 3 filas de valorVacio son de línea 'Devolución Contenedor' (coincide con el README)
OK: valor máximo real = $18.000.000 (coincide con el README)
OK: valor mínimo real = $100.000 (coincide con el README)
OK: las 15 filas de sinPlaza no traen campo 'valor' (ausencia real de tarifa, no un null)
OK: la ruta 'sin plaza' más frecuente es Cúcuta->Buenaventura con 59 apariciones (coincide con el README)

TODO OK.
```

Confirma en particular que **no hay ningún `sourceId` inventado o repetido** entre
las 195 filas que lo traen (núcleo + 5 de las 6 categorías de casos borde;
`sinPlaza` no trae `sourceId` porque es ausencia real de fila, no una fila con id) —
el riesgo más obvio de un dataset "curado a mano" a partir de datos reales.

### Excel de revisión para negocio

Verificado como contenedor ZIP válido (firma `PK`), **7 hojas** dentro
(`sheet1.xml`..`sheet7.xml`), coincide con las 7 que documenta el README (Leame,
Núcleo, Vigencia vencida, Valores extremos, Sin plaza, Hallazgo vehículo vacío,
Aprobación).

### Escenario Gherkin nuevo

`docs/contrato-integracion/escenarios.feature`: el escenario nuevo está
correctamente etiquetado `@pendiente-issue-33` y su comentario deja explícito que
el mecanismo de consumo real todavía no existe — consistente con el alcance
declarado del PR. Nota menor de estilo: falta la línea en blanco entre este
escenario y el siguiente (`@hu-051`) que sí separa a los demás escenarios del
archivo — cosmético, no afecta el contenido ni ningún parser (este `.feature` es
documentación, no se ejecuta con un runner de Gherkin en este proyecto).

## Fuera de alcance — correctamente, según lo que declara el PR

- No hay ningún flujo real end-to-end que ejercitar: `ReglaDeCosteo`/
  `PropuestaTarifaAdapter` no consumen todavía este dataset (issue #33, no
  construida). Por la misma razón, no aplica una prueba de carga/estrés — no hay
  ningún endpoint ni componente en ejecución que reciba este dataset hoy.
- No se pudo re-verificar el hallazgo poblacional (95,8% de tarifas vencidas sobre
  las 22.965 filas de `tblSAPTarifas`) contra la base corporativa real — mismo
  bloqueo de acceso que la issue #3. Sí se verificó, con mi propio script, que las
  210 filas del extracto son internamente consistentes con lo que el README afirma
  sobre ellas.

## Conclusión

El bug de CI encontrado ya está corregido y verificado (13/13 en verde dentro del
contenedor real). El contenido del dataset se sostiene bajo mi propia verificación
independiente — ninguna de las afirmaciones concretas del README resultó
incorrecta. Sin objeciones para mergear.
