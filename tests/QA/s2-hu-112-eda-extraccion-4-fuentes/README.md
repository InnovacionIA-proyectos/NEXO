# QA — S2-HU-11.2: EDA-2, extracción que cruza las 4 fuentes

**PR verificado:** #145. **Autor:** scalderon-bs.
**Fecha:** 2026-09-15. **Verificado por:** Kevin Cano (QA).

La rama estaba muy desactualizada (venía de antes del PR #141 y del #147, incluso
antes del renombre de `analitica/02-eda` a `analitica/02-eda-viajes-btura`). Se hizo
un merge real de `QA` sobre la rama — **sin conflictos**, confirmado con
`git diff --diff-filter=D` que no se perdió ningún archivo real (el único cambio de
estructura, `03-modelo` → `04-modelo`, era un placeholder vacío en ambos lados).

Como no tocaba nada de `.NET`, se corrieron las suites igual para confirmar que
seguían en verde tras el merge: Domain 127/127, Application 140/140, Api.Tests
168/171 (2 skip + la misma condición de carrera ya documentada en el PR #147, no
nueva de esta rama), integración 16/16.

## Script de esta carpeta

[`verify-notebooks-desde-cero.ps1`](verify-notebooks-desde-cero.ps1) — mismo patrón
que el de `s2-hu-111-eda-viajes-buenaventura/` (EDA-1), adaptado a esta carpeta.
Corrido de verdad, salida real:

```
OK: 00_setup_y_carga.ipynb
OK: 01_perfilado_estructural.ipynb
OK: 02_unicidad_y_granularidad.ipynb
OK: 03_calidad_del_dato.ipynb
OK: 04_univariado.ipynb
OK: 05_bivariado_multivariado.ipynb
OK: 06_temporal_y_geografico.ipynb
OK: 07_outliers_y_sesgos.ipynb

TODO OK: los 8 notebooks corrieron sin errores, desde cero.
```

Los 18 parquets de `datos/interim/` y la figura de `reportes/figures/` se
regeneraron idénticos a los ya comiteados — a diferencia del EDA-1, esta vez no
quedó ningún archivo huérfano.

## Verificación de los hallazgos citados contra el código real

`hallazgos.md` (H-003) afirma que el mapeo `LineaNegocio`/`ClaseViaje` → código SAP
vive hardcodeado en `TarifarioSapAdapter.cs`. Lo leí directo del archivo actual:

```
[LineaNegocio.PuertoAInterior] = 1,  [LineaNegocio.InteriorAPuerto] = 2,
[LineaNegocio.Nacional] = 4,         [LineaNegocio.DevolucionContenedor] = 6,
[LineaNegocio.Andino] = 7,           [LineaNegocio.Licitaciones] = 8,
[ClaseViaje.Contenedor] = 1, [ClaseViaje.Cupo] = 2,
[ClaseViaje.CargaSuelta] = 4, [ClaseViaje.Extradimensionado] = 5,
```

Coincide exacto con lo citado en el hallazgo — el mismo nivel de rigor que en el
EDA-1 (donde también se verificaron citas de código contra el estado real).

## Hallazgo fuera del EDA: archivo suelto en la raíz del repo

`mapeo_camelcase.txt` (184 líneas, un volcado de mapeo de columnas a camelCase, sin
contenido sensible) quedó comiteado en la raíz del repo, sin relación con la
estructura de `analitica/` y sin referencia desde ningún notebook — parece un
archivo de trabajo del autor. **Se quitó de la rama** (autorizado por Kevin) antes
de continuar. Mucho menor que el hallazgo análogo del PR #140 (que sí tenía
contenido sensible) — acá no había nada que ameritara revisar el historial.

## Calidad del propio EDA

Reporte con la misma disciplina que el EDA-1: hallazgos cruzados contra
`hallazgos.md`/`limitaciones.md` del EDA-1 y contra HU-10.2/10.3, con una
retractación transparente y bien documentada (H-016/L-07 — un hallazgo sin
evidencia trazable, retirado en vez de dejado a medias).

## Decisión

Se aprueba el merge.
