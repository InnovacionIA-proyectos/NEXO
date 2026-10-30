# PR #253 — fix: correct vehicle-type codes in reglas_solo_sicetac and tarifas_plaza seeders

## Qué cambia (resumen de la PR)

Solo datos/documentación, sin código (`.cs`):
- `reglas_solo_sicetac.csv`: 348 → 260 filas. `C2`→`2` (Turbo/Sencillo), `"PBV 7500-8000"`→`2L3`, se quitan 88 filas de contenedor que nunca fueron un tipo de vehículo real.
- `tarifas_plaza.csv`: 271 → 107 filas. Se quitan 164 filas que resultaron ser referencia solo-SICE (no tarifa real de Plaza), mismo cambio `PBV 7500-8000`→`2L3`.
- `servicios_conexos.csv`: se quitan los 10 servicios de valor único de demo (no vienen del Excel real).
- `DECISIONES-TECNICAS.md` nuevo: mapeo nombre original → código final, con el razonamiento.

## Qué se verificó

No es un cambio de código — el tipo de test correcto es **verificación de datos contra la BD real** (no unitario), según §4 de la metodología.

**1. Consistencia CSV (antes de tocar la BD):**
- Conteos exactos: `reglas_solo_sicetac.csv` 260 filas de datos, `tarifas_plaza.csv` 107, coinciden con lo que dice la PR.
- Sin duplicados de llave `(Origen, Destino, TipoVehiculo)` en `reglas_solo_sicetac.csv`.
- Sin rastro de `C2`, el texto literal `"PBV 7500-8000"`, ni filas `CNT20/CNT40` en ninguno de los dos CSVs corregidos.

**2. Estado real de la BD (`dbInformesAutomatizados_Pruebas.db_NEXO`), no solo el CSV del repo — verificación independiente de lo que afirma la PR:**
Levanté la API real (VPN + BD de pruebas) y consulté los endpoints en vivo:
- `GET /api/reglas-solo-sicetac`: **260 filas reales**, tipos `2/2L3/2S2/2S3/3S2/3S3` — coincide exacto con el CSV corregido. La tabla sí fue vaciada y recargada, como afirma la PR.
- `GET /api/tarifas-plaza`: **107 filas reales**, todas `claseViaje=Contenedor` — coincide exacto con el CSV corregido.
- `GET /api/servicios-conexos`: 1366 filas reales vs 1336 del CSV corregido (diferencia de 30). Investigado: las 30 de más son `codigoServicio` con prefijo `COD_<guid>`/`NUEVO_<guid>` y `tipoServicio="Precinto"` genérico — confirmé que ese patrón lo genera `tests/Nexo.Api.Tests/clsServiciosConexosTests.cs` (`$"COD_{Guid.NewGuid():N}"`). Es contaminación de datos de prueba de la propia suite automatizada corriendo contra esta BD compartida (patrón ya conocido, §10 de la metodología) — **no es un defecto de esta PR**. Ninguno de los 10 servicios de demo (`Cargue`, `Descargue`, `Stand-by`, `Candado satelital`, `Escolta`, etc.) aparece en la BD real — confirmado que sí se quitaron.

**3. Regresión de código:** no aplica — la PR no toca ningún `.cs`, y los seeders CSV de `docs/Database/seeder/` son independientes del seeder de fixtures de test (`clsDataSeeder.cs`), que tiene su propio set de datos sintéticos. La suite completa (Domain/Application/Api) ya se corrió en verde sobre el mismo estado de `QA` que esta rama no modifica a nivel de código.

## Pregunta abierta para la autora (no bloqueante, pero vale la pena confirmar)

El mapeo `"PBV 7500-8000"` → `2L3` se documenta como "confirmado" en `DECISIONES-TECNICAS.md`, pero al revisar la muestra real de DWSice (`analitica/00-fuentes/fuentes-corporativas/piso-sicetac-tblRutasSicetac-muestra-202608.csv`) aparecen **las tres variantes `2L1`, `2L2` y `2L3`** como códigos de configuración reales y activos en SICE-TAC (7, 9 y 5 apariciones respectivamente) — no until solo `2L2` (el que la PR identifica como el equivalente de SAP Tarifario). No hay en el repo un diccionario que diga qué distingue a `2L1`/`2L2`/`2L3` entre sí (¿sub-bandas de peso dentro de "liviano"?), así que no pude confirmar ni descartar la elección de `2L3` con los datos disponibles. Vale la pena que Sara confirme la fuente exacta de esa equivalencia antes de dar esto por cerrado al 100%, aunque no es motivo para bloquear la PR — es un cambio que ya es una mejora objetiva sobre el estado anterior (texto literal sin código).

## Resultado

Sin hallazgos bloqueantes. PRs de datos como esta no tienen "build" que correr; la verificación fue contra la BD real y quedó en verde, salvo la pregunta abierta de arriba (no bloqueante).
