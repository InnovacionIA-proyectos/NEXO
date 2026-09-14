# Verificación QA — S2-HU-10.3 (issue #37, PR #91)

Rama: `37-s2-hu-103-consolidación-del-set-diccionario-linaje-y-control-de-calidad`
(scalderon-bs). Verificación hecha antes de aprobar/mergear.

**Conexión con otros PR (importante para el orden de merge)**: esta rama está
construida literalmente encima de la rama del PR #87 (mismo historial de commits:
`7ead1bf`, `ed0af72`, `50d7e7e` son commits de #87). PR #91 mueve/consolida el
contenido de `docs/extractos-fuentes-corporativas/` (PR #87) y
`docs/analitica/DICCIONARIO_DATOS.md` (PR #82, ya en `QA`) dentro de una carpeta
raíz nueva `analitica/`, y le suma el diccionario unificado + linaje + control de
calidad cruzado. **Por eso el orden correcto de merge es #87 primero, #91 después**
— si #91 se mergea antes que #87, `QA` se queda igual (#91 ya trae todo lo de #87
dentro), pero entonces #87 se vuelve un PR redundante que solo re-agregaría la
versión vieja (sin reorganizar) de esos mismos archivos.

Merge real de #91 contra `origin/QA` (con #87 ya mergeado primero): sin conflictos.

## Qué trae el PR

- `analitica/` (carpeta nueva, reemplaza `docs/analitica/` y
  `docs/extractos-fuentes-corporativas/`): estructura por etapa
  `00-fuentes/{bd-propia,fuentes-corporativas}` → `01-set-final/v1.0.0-piloto/`
  (copia congelada, autocontenida) → `02-eda/`/`03-modelo/`/`pipeline/`
  (reservadas para S2-HU-11 y siguientes).
- Exportó las 13 vistas `analitica.*` (HU-10.1) a CSV usando la migración ya
  existente, aplicada por primera vez en este entorno.
- `diccionario-y-linaje.md`: diccionario unificado de las 13 tablas propias + las 4
  externas, con linaje (de qué tabla real viene cada columna) y llaves de cruce.
- `reporte-inconsistencias.md`: 5 hallazgos documentados, incluida una verificación
  cruzada (14/15 líneas Sicetac/Tarifario coinciden exacto contra la fuente
  externa) que reveló que el piso SICE-TAC se cotiza con `VALORSICECONHORA`, no
  `VALOR` — corrección aplicada retroactivamente al README de HU-10.2.

## Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | 121/121 |
| `Api.Tests` | 103/105 (+2 saltadas por diseño) |

Build `--no-cache`: 0 errores. 353 pruebas, cero fallos — este PR es documentación/datos, no toca código.

## Verificación de las afirmaciones concretas del reporte — más allá de leerlas

| Afirmación | Verificado |
|---|---|
| Caso real de `Deadline` anterior a `CreatedAt` (`SolicitudId 3EAB0AB6...`) | ✅ Exacto — confirmado en `solicitudes.csv`: `Deadline=2026-08-26 16:44`, `CreatedAt=2026-08-31 16:44` |
| Ruta malformada "b"→"j" (`LineaCotizacionId CA0FC8A8...`, $5.783.697, Fuente Sicetac) | ✅ Exacto — confirmado en `cotizacion_lineas.csv`/`catalogo_rutas.csv` |
| 13 solicitudes, 18 cotizaciones, 48 líneas de cotización | ✅ Exacto, una vez descontada la fila separadora de guiones que trae cada CSV (ver hallazgo abajo) — sin descontarla, los conteos parecen "14/19/49", un error de interpretación fácil de cometer |
| 14/15 líneas Sicetac/Tarifario coinciden contra la fuente externa (`VALORSICECONHORA`) | ⚠️ No reproducible con los archivos versionados — ver nota abajo |

### Nota sobre la verificación cruzada (14/15) — no reproducible aquí, no es una contradicción

Intenté reproducir este hallazgo cruzando `cotizacion_lineas` → `lineas_solicitud`
→ `catalogo_rutas` contra `piso-sicetac-...csv`
([`verify-crosscheck-sicetac.mjs`](verify-crosscheck-sicetac.mjs)): **0 de 13**
líneas `Fuente=Sicetac` encontraron su ruta en el archivo. Esto **no contradice**
el hallazgo del autor — `piso-sicetac-...csv` es la muestra estratificada de 151
filas de HU-10.2 (126 núcleo + 25 casos borde), no la tabla completa de ~2
millones de filas. Las rutas reales de las 13 solicitudes de prueba (Medellín↔
Bogotá, Bogotá↔Barranquilla, etc., rutas comunes) no tienen por qué caer en esa
muestra. El autor validó esto contra la tabla completa, con acceso que esta
verificación no tiene (issue #3, sin cambios). El script queda listo para
correrse contra la tabla completa el día que haya acceso.

## ⚠️ Hallazgo menor, no bloqueante — fila de separador de guiones en las 13 CSV de `bd-propia`

Los 13 archivos de `datos/bd-propia/` (en `00-fuentes/` y en la copia congelada de
`01-set-final/`) traen una **fila literal de guiones** (`--,---,---,...`) como
segunda línea del archivo — el separador visual que `sqlcmd` agrega en su modo de
salida por columnas, exportado sin quitar. Un consumidor que no sepa que debe
saltarla (Excel, `pandas.read_csv` sin `skiprows`) la trataría como una fila de
datos real con valores basura.

**Impacto real, ya verificado**: es la causa de que un conteo ingenuo de filas
(`wc -l` menos el encabezado) dé 14/19/49 en vez de los 13/18/48 reales que
documenta el reporte — un desajuste de 1, fácil de no notar. Los 4 archivos de
`fuentes-corporativas/` (ya corregidos en el PR #87) **no** tienen este problema,
usan un modo de exportación distinto sin la fila de separador.

**Corrección sugerida**: re-exportar las 13 tablas de `bd-propia` con el mismo
método ya usado para las 4 de `fuentes-corporativas` (sin el separador de
guiones), o al menos documentar explícitamente en el README de `analitica/` que
hay que saltar la segunda línea de esos 13 archivos.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 353 pruebas, sin filtros, cero fallos |
| Contenido | ✅ Los 2 casos puntuales del reporte (deadline, ruta malformada) y los 3 conteos de filas verificados exactos |
| Calidad de datos | ⚠️ Fila de separador de guiones en 13/13 archivos de `bd-propia` (menor, no bloqueante) |
| Verificación cruzada externa | ℹ️ No reproducible con los artefactos versionados (muestra vs. tabla completa) — no es una contradicción del hallazgo del autor |

## Conclusión

El diccionario, el linaje y el reporte de inconsistencias son honestos y
consistentes con los datos reales — los 2 casos puntuales que cita el reporte y
los conteos de filas se verificaron exactos. Se encontró un hallazgo menor (fila
de separador en 13 archivos de `bd-propia`) que vale la pena limpiar pero no
bloquea el PR. La verificación cruzada central (14/15) no se pudo reproducir con
los artefactos disponibles en este entorno, por la misma limitación de acceso
corporativo de siempre — no hay motivo para dudar de ella. Sin objeciones para
mergear, después de #87.
