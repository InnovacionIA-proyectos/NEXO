# QA — PR #394: Plantillas de mapeo por cliente y reutilización en sugerir-mapeo (#361, #365)

## Resumen

Agrega el CRUD de plantillas (`PUT/GET/DELETE /api/captura-solicitudes/plantillas-mapeo`)
sobre `tbl_plantillas_mapeo_cliente` (creada en #395) y hace que `sugerir-mapeo`
reconozca, como **paso 0** antes del diccionario y la IA, un archivo con exactamente
los mismos encabezados que una plantilla ya confirmada del cliente — devolviendo
ese mapeo con `origen = "plantilla"`, `confianza = 1` y `motivoSinIa = "PLANTILLA_RECONOCIDA"`,
sin llamar a la IA.

La identidad de la plantilla es la **firma de encabezados**
(`clsFirmaDeEncabezados`, SHA-256 de los encabezados normalizados y en orden,
más la cantidad). Depende de #395 (tabla), que ya está mergeada en `QA`.

## Qué se verificó

1. Diff aislado de la PR (`git diff origin/QA...origin/365-plantilla-mapeo-cliente`):
   controller nuevo, casos de uso (guardar/listar/eliminar), repositorio EF,
   entidad, mapeo en `clsNexoDbContext` (sin migración EF, el esquema vive en
   el script SQL de #395 — consistente), DI, y la integración en
   `clsSugerirMapeoExcelQuery` como paso 0.
2. Leído `CONTRATO.md` actualizado: contrato de los 3 endpoints, el nuevo
   `origen: "plantilla"` y `plantillaId` en `SugerenciaMapeoDto`, roles
   (`Comercial`, `Admin`), y códigos de error.
3. Revisado el cálculo de la firma (`clsFirmaDeEncabezados.Calcular`): usa
   `clsResolucionDeCatalogos.Normalizar` (que ya colapsa espacios, quita
   tildes y pasa a mayúsculas) directamente sobre los encabezados crudos de
   la consulta — **no hace falta pre-limpiarlos antes**, el propio cálculo se
   auto-normaliza igual que al guardar. No es un bug: es el mismo criterio en
   ambos lados.
4. `clsGuardarPlantillaMapeoCommandHandler.Handle` revisado: valida cliente,
   nombre, encabezados y columnas (reusa `clsAplicarMapeoExcelQueryHandler.ValidarColumnas`,
   ahora expuesto como `internal` para esto); "guardar o reemplazar" correcto
   por `(ClienteId, FirmaEncabezados)`.
5. `dotnet build -c Release` → 0 errores (solo warnings preexistentes del
   repo, no de esta PR: `NU1902`/`NU1903` de un paquete transitivo y un
   `ASPDEPR005` de `clsProgram.cs` ya presente en `QA`).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **843/843** (incluye
   `clsPlantillasMapeoTests.cs`, 295 líneas nuevas).
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **852/862**, 10 omitidas (esperadas:
   correo real sin relay, Cubicador E2E sin Docker, paginación histórico SAP
   — igual que en corridas anteriores). Incluye `clsPlantillasMapeoApiTests.cs`
   (209 líneas nuevas), que ejercita el CRUD completo contra la BD real.

## Hallazgo — no bloqueante

`clsGuardarPlantillaMapeoCommandHandler` usa el mismo patrón check-then-insert
(`ObtenerParaActualizarAsync` seguido de `SaveChangesAsync`) **sin** ningún
`catch (DbUpdateException)`, igual que el hallazgo ya reportado y aceptado
como no bloqueante en PR #389 (issue #393): bajo dos `PUT` concurrentes con
los mismos encabezados para el mismo cliente, el índice único
`(ClienteId, FirmaEncabezados)` protege la integridad de los datos, pero uno
de los dos requests devolvería `500` en vez de un `200`/`409` más correcto.
No se reprodujo con una prueba de carga puntual en esta verificación (sería
redundante con lo ya documentado en #393); se deja constancia por si se
decide atender junto con esa issue.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. Pendiente de
autorización de Kevin para comentar/aprobar/mergear en GitHub.
