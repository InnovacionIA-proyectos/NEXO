# QA — S2-HU-11.1: EDA sobre el set histórico de viajes Buenaventura

**PR verificado:** #140. **Autor:** scalderon-bs.
**Fecha:** 2026-09-11. **Verificado por:** Kevin Cano (QA).

## Antecedente: migración bloqueante, ya corregida

La primera revisión de esta rama encontró que traía una migración de EF
(`QuitarNolockVistasAnalitica`, de un commit temprano y no relacionado con el
EDA) que todavía apuntaba al esquema `dbo` de antes del rename a
`dbo_NEXO_prueba` (PR #90/#93/#95) — la rama tenía 59 commits de atraso. Verificado
empíricamente que **reventaba** contra el `QA` actual con
`Invalid object name 'dbo.clientes'`. Reportado al responsable (comentario en el
PR), quien renombró y corrigió la migración
(`20260910231344_QuitarNolockVistasAnaliticaDboNexoPrueba`, apunta correctamente a
`dbo_NEXO_prueba.*`).

**Verificación de la corrección:** se hizo un merge real de `QA` sobre la rama (sin
conflictos, sin archivos perdidos — confirmado con `git diff --diff-filter=D`), y se
aplicaron TODAS las migraciones (las de `QA` + esta nueva) contra una base de
pruebas limpia con `dotnet ef database update`. Aplicaron las 29 sin error. La
vista `analitica.clientes` quedó correcta (`FROM dbo_NEXO_prueba.clientes`, sin
`WITH (NOLOCK)`) y es consultable sin error. Script de verificación, ya probado
contra una base real:
[`verify-migracion-analitica-sin-nolock.sql`](verify-migracion-analitica-sin-nolock.sql)
(confirma las 13 vistas, el esquema correcto, la ausencia de `NOLOCK`, y una
consulta de humo real contra `analitica.clientes`).

## Suites automatizadas (tras el merge de QA)

| Suite | Resultado |
|---|---|
| `dotnet build` (Release) | ✅ 0 errores |
| Domain.Tests | ✅ 127/127 |
| Application.Tests | ✅ 132/132 |
| Api.Tests (unitarias) | ⚠️ 152/159 — mismas 5 fallas preexistentes de `ContratoMessageTypeTests` (hueco de CI ya documentado en PR #128, no relacionado con esta rama) |
| Api.Tests (integración) | ✅ 16/16 |

## Verificación del EDA en sí

No se limitó a revisar los reportes — se ejecutaron los 8 notebooks de verdad, con
el script [`verify-notebooks-desde-cero.ps1`](verify-notebooks-desde-cero.ps1)
(probado end-to-end, salida real incluida abajo):

1. El script borra `datos/interim/*.parquet` y `reportes/figures/*.png` para forzar
   una corrida **desde cero** (la misma afirmación que hace el propio README: "los 8
   notebooks corren limpios con Restart & Run All, sin errores, desde cero").
2. Ejecuta los 8 notebooks en orden (`00` a `07`) vía `jupyter nbconvert --execute`.
3. **Resultado real de la corrida:**
   ```
   OK: 00_setup_y_carga.ipynb
   OK: 01_perfilado_estructural.ipynb
   OK: 02_calidad_del_dato.ipynb
   OK: 03_univariado.ipynb
   OK: 04_bivariado_multivariado.ipynb
   OK: 05_temporal_y_geografico.ipynb
   OK: 06_outliers_y_sesgos.ipynb
   OK: 07_sintesis_y_limitaciones.ipynb

   TODO OK: los 8 notebooks corrieron sin errores, desde cero.
   ```
   Confirma la afirmación del README de la HU.
4. Al final, el script restaura el árbol de trabajo al estado commiteado.

Uso: `./tests/QA/s2-hu-111-eda-viajes-buenaventura/verify-notebooks-desde-cero.ps1
-PythonExe <ruta al python del venv con requirements.txt instalado>`.

### Hallazgo menor (no bloqueante): una figura huérfana

`reportes/figures/03_ecdf_numericas.png` está commiteada pero el notebook `03`
actual ya no la genera (el paso quedó marcado explícitamente como
"Paso 4 — ECDF (omitido)"). No está referenciada en ningún reporte — es un archivo
sobrante de una versión anterior del notebook que nadie borró. Cosmético, no afecta
ninguna conclusión.

### Nota aparte, fuera del EDA

Se encontró un archivo (`docs/sprint3_mvp_e2e_nexo.md`, 309 líneas) mezclado en el
commit "Bivariate and multivariate analysis" de esta misma rama, cuyo contenido no
tiene relación con el EDA — parece una nota de planeación de sprint que terminó
comiteada por accidente. Reportado directamente a Kevin fuera de este README; no es
un hallazgo de calidad del EDA en sí.

## Decisión

Con la migración corregida y verificada, y el EDA confirmado ejecutable de punta a
punta, se aprueba el merge — pendiente de que se resuelva aparte el archivo fuera de
lugar mencionado arriba.
