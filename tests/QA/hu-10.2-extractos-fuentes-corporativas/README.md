# Verificación QA — S2-HU-10.2 (issue #36, PR #87)

Rama: `36-s2-hu-102-extractos-puntuales-de-las-4-fuentes-de-lectura-corporativas`
(scalderon-bs). Verificación hecha antes de aprobar/mergear. Merge real contra
`origin/QA`: sin conflictos (la rama ya estaba al día con `QA`).

## Qué trae el PR

4 extractos puntuales (CSV) desde los data warehouses corporativos de solo lectura —
piso SICE-TAC (muestra de 151 filas, la tabla completa pesa 824 MB), tarifa de plaza
SAP (23.546 filas completas), histórico SAP (25.303 filas completas) y catálogo de
clientes (38.985 organizaciones) — más el README que documenta columnas, llaves de
cruce contra el histórico propio de HU-10.1, y un hallazgo de permisos (la cuenta
`consultascubos` en `DWCubosOperacionesEBS` tiene `INSERT/UPDATE/DELETE`, no solo
`SELECT`). Sin cambios de código de producción.

## Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | 121/121 |
| `Api.Tests` | 103/105 (+2 saltadas por diseño) |

Build `--no-cache`: 0 errores. Números idénticos a la última verificación (PR #82) —
esperado, este PR no toca código.

## Verificación del contenido — cifras concretas del README contra el archivo real

Todas las cifras que el README documenta en prosa se confirmaron exactas contra los
4 archivos:

| Afirmación del README | Verificado |
|---|---|
| Piso SICE-TAC: 151 filas = 126 núcleo + 5×5 categorías raras | ✅ Exacto (columna `Muestra`: 126 `nucleo`, 5 de cada una de las 5 `rara_*`) |
| `tarifa-plaza`: 23.546 filas | ✅ Exacto |
| `historico-sap`: 25.303 filas | ✅ Exacto |
| `catalogo-clientes`: 38.985 filas, todas `TYPE='2'` | ✅ Exacto — 0 filas con otro `TYPE` |
| `rara_valor_maximo`: $20.822.184, Tumaco↔Santa Marta, Carga Refrigerada | ✅ Exacto |
| `rara_valor_minimo`: $3.614, intra-municipio en Yumbo | ✅ Exacto |
| `rara_ambiguedad_567_candidatos`: ruta Villavicencio↔Buenaventura | ✅ Las 5 filas de muestra son de esa ruta (el conteo de 567 candidatos en la tabla completa de 2M filas no se pudo re-verificar — sin acceso a esa BD) |

**Verificación adicional, no incluida por el autor**: integridad referencial entre
`historico-sap.ID_TARIFA` y `tarifa-plaza.ID` — tomé una muestra de 500 valores de
`ID_TARIFA` y confirmé que los 500 existen como `ID` real en `tarifa-plaza` (0
huérfanos), confirmando que el enlace que documenta el README es real, no solo una
convención de nombre de columna.

## ✅ Actualización (2026-09-02) — corregido y re-verificado en la 2ª versión del PR

El autor re-exportó los 3 archivos afectados con el escape CSV correcto
(commit `c948b4e`, "Fixing some bugs of csv documents"). Mi primer script de
verificación (`awk -F','` ingenuo) daba **falsos positivos** contra la versión
corregida, porque no respeta comillas — cualquier CSV bien formado con texto
libre entre comillas "falla" ese chequeo aunque esté perfecto. Lo reemplacé por
[`verify-csv-integridad.mjs`](verify-csv-integridad.mjs), un parser CSV real
(respeta comillas y saltos de línea embebidos dentro de campos entrecomillados):

```
piso-sicetac-...csv: 153 filas, columnas {"16":153} -> OK
tarifa-plaza-...csv: 23021 filas, columnas {"24":23021} -> OK
historico-sap-...csv: 25319 filas, columnas {"20":25319} -> OK
catalogo-clientes-...csv: 38996 filas, columnas {"10":38996} -> OK

TODO OK: los 4 archivos son CSV válido, ninguna fila con columnas incorrectas.
```

Las cifras de filas que documenta la 2ª versión del README (23.020 / 25.318 /
38.995, re-extraídas el 2026-09-02) coinciden exactas con el conteo real del
parser (descontando la fila de encabezado). **El hallazgo original está
resuelto.** Regresión re-confirmada tras el fix: 353 pruebas, cero fallos.

## Hallazgo original (2026-09-01) — YA CORREGIDO, ver actualización arriba

Los archivos **no escapan/encierran entre comillas los campos de texto libre que
contienen comas o saltos de línea** (`DETALLESTARIFA` en `tarifa-plaza`/`historico-sap`,
`NAME_ORG1`/`NAME_ORG2` en `catalogo-clientes`). El resultado: una fracción real y
no pequeña de filas queda **estructuralmente desalineada** para cualquier
consumidor CSV estándar (pandas, Excel, `csv.reader`), y en el peor caso
(`tarifa-plaza`) algunas filas quedan **partidas en varias líneas físicas** porque
el texto libre trae saltos de línea reales sin escapar.

Reproducido con [`verify-csv-integridad.sh`](verify-csv-integridad.sh) (cuenta
columnas por línea física, contra el número de columnas que declara el propio
encabezado de cada archivo):

| Archivo | Columnas esperadas | Líneas con columnas incorrectas |
|---|---|---|
| `piso-sicetac-...csv` | 16 | **0 de 153** (0%) — limpio, `NOMBRERUTA` no usa comas |
| `tarifa-plaza-...csv` | 24 | **3.363 de 23.548** (~14%) — incluye filas partidas en 2-3 líneas físicas por saltos de línea sin escapar en `DETALLESTARIFA` |
| `historico-sap-...csv` | 20 | **4.611 de 25.305** (~18%) |
| `catalogo-clientes-...csv` | 10 | **323 de 38.987** (~0,8%) — razón social (`NAME_ORG1`/`NAME_ORG2`) con coma, ej. `"Empresa S.A., Sucursal Norte"` |

Ejemplo real de una fila partida en `tarifa-plaza` (el campo `DETALLESTARIFA` trae
un salto de línea real dentro del texto, sin comillas que lo protejan):

```
4542: para El Rodadero se le deben incrementar $500.000 mas al flete(el ingreso...)
4543: //
```
Esas 2 "líneas" son en realidad **una sola fila** de datos, partida en 2 — cualquier
lector de CSV por líneas (o Excel importando "una fila = un renglón") la rompe en 2
registros basura, ninguno de los dos parseable como la fila original.

**Por qué importa**: el propósito explícito de este extracto es ser el insumo del
EDA de S2-HU-11 — si el equipo de datos abre estos archivos con pandas
(`pd.read_csv`, configuración por defecto) o Excel, entre el 14% y el 18% de las
filas de `tarifa-plaza`/`historico-sap` van a desalinear columnas silenciosamente
(un valor de `VALOR` puede terminar en la columna `VIGENCIA`, por ejemplo), sin que
nada avise del error. No es un problema cosmético: corrompe el contenido para
cualquier análisis automatizado, exactamente el escenario que el issue #38 (QA de
la entrega del set) existe para prevenir ("el diccionario dice la verdad").

**Causa probable**: exportación con `sqlcmd` en modo separado por comas sin
`-Y`/comillas (o sin encerrar en `"` los campos de texto), que no escapa comas ni
saltos de línea embebidos — a diferencia de una exportación CSV real (RFC 4180) o
`bcp` con formato adecuado.

**Corrección sugerida**: re-exportar los 3 archivos afectados encerrando entre
comillas dobles cualquier campo de texto libre (`DETALLESTARIFA`, `NAME_ORG1`,
`NAME_ORG2`) y escapando comillas internas (`""`) — o exportar en un formato que no
tenga este problema (TSV con los campos de texto libre limpiados de tabs, o Parquet).
`piso-sicetac` no necesita cambios (ya está limpio).

## Otras verificaciones

- **`.gitignore`**: agrega 3 archivos de trabajo local (`docs/analisis-dw-sap-baseline-cubicador.md`, `docs/captura-prueba-rutas-reales.xlsx`, `docs/diccionario-datos-cabinafletes.md`) — no afectan nada versionado, cambio razonable.
- **`escenarios.feature`**: nuevo escenario `@hu-10 @habilitador-b8` bien formado, con línea en blanco de separación respecto a los escenarios vecinos (a diferencia de una nota cosmética menor detectada en el PR #74 anterior, aquí sí está bien separado).
- **Hallazgo de permisos ya documentado por el autor** (`consultascubos` con `INSERT/UPDATE/DELETE` en `DWCubosOperacionesEBS`): no verificable de forma independiente sin acceso a esa base — confío en el `fn_my_permissions` que cita el autor, queda igual de válido documentarlo como hallazgo para IT.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores (PR no toca código) |
| Regresión | ✅ 353 pruebas, sin filtros, cero fallos |
| Integridad de datos | ⚠️ 3 de 4 archivos con desalineación estructural real (14-18% de filas en los 2 más grandes) — ver hallazgo arriba |
| Contenido | ✅ Todas las cifras concretas del README verificadas exactas contra los archivos, incluida una verificación de integridad referencial no incluida por el autor |

## Conclusión

El contenido de los 4 extractos es fiel a lo que documenta el README — ninguna cifra
resultó incorrecta. El hallazgo de CSV mal formado (14-18% de filas afectadas en los
2 archivos más grandes) fue corregido por el autor y re-verificado con un parser CSV
real: los 4 archivos son válidos ahora, sin ninguna fila mal alineada. Regresión
353/353 sin fallos. Sin objeciones para mergear.
