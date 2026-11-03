# PR #295 (S5-HU-22.1) — Carga masiva de Tarifas Plaza y catálogos (cierra #288)

## Qué cambia

- `clsLectorFechasExcel`: lector compartido de fechas (fecha nativa, serial de Excel
  1970-2099, 4 formatos de texto), reemplaza el `GetString()+DateOnly.TryParse` sin
  cultura que tenían Tarifas Plaza y Servicios Conexos.
- Catálogo de vehículos (`GET /api/catalogos/tipos-vehiculo`) sin los 5 casos
  especiales inventados — solo `tblSAPTiposVehiculoTarifario`.
- Tarifas Plaza: columnas opcionales `DEPARTAMENTO ORIGEN`/`DESTINO` para resolver
  ciudades de nombre repetido (68 municipios colombianos se repiten entre
  departamentos).
- `CoordinadorFletes` ve ciudades en Servicios Logísticos; Capturar/analizar-excel
  siguen solo Comercial/Admin.
- `GET /api/reglas-solo-sicetac/exportar-excel` nuevo.
- Sin cambios de esquema.

## Qué se verificó

**Merge real contra `QA`**: sin conflictos.

**Código revisado línea por línea:** `clsLectorFechasExcel` (orden de confiabilidad:
tipo nativo → serial con rango plausible → 4 formatos de texto en InvariantCulture →
es-CO como último recurso) y `ResolverMunicipio` en `clsEfTarifasPlazaRepository`
(ordenamiento determinístico `ThenBy(Id)` para no depender del orden de la BD, mensaje
de error específico cuando el departamento no corresponde).

**Suite completa contra la base real de pruebas (2026-10-13):**
- `Nexo.Domain.Tests`: 157/157.
- `Nexo.Application.Tests`: 347/347.
- `Nexo.Api.Tests`: primera corrida sin Cubicador → 7 fallos (6 por falta del
  Cubicador local, mismo patrón que en PRs anteriores + 1 ya diagnosticado en #292,
  dato SAP vencido por calendario, ajeno a esta PR). Tests específicos de esta PR
  corridos aparte primero, sin necesitar Cubicador: **7/7** en Application
  (`clsImportarReglasSoloSicetacExcelCommandTests`, `clsListarTiposVehiculoQueryTests`)
  y **70/70** en Api (`clsLectorFechasExcelTests`, `clsImportarTarifasPlazaExcelTests`,
  `clsCatalogosYExportReglasTests`). Armé el Cubicador real localmente de nuevo →
  **351/354 superadas, 2 omitidas, solo el fallo ya conocido**. Coincide con lo
  declarado (352 OK la autora — 1 de diferencia por la misma fila SAP).

**Verificación en vivo (API real contra BD real):**
- `GET /api/catalogos/tipos-vehiculo` → solo 6 códigos reales de SAP
  (`3S3`, `2S2`, `2S3`, `C3`, `2L2`, `2`) — sin `CNT20/CNT40 Consolidado/Exclusivo` ni
  el texto literal `"PBV 7500-8000"` (ahora `2L2`, "Liviano (Capacidad 4 ton)").
- `GET /api/reglas-solo-sicetac/exportar-excel` → `200`.

## Resultado

Sin hallazgos bloqueantes. El único fallo de la suite es el ya documentado en la
verificación de la PR #292 (dato SAP vencido por calendario), no relacionado con esta
PR.
