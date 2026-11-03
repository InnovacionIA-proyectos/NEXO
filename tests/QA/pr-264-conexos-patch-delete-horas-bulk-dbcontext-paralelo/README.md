# PR #264 (rama `DEPLOY`) — Horas en lineas:bulk (#263), PATCH/DELETE de conexos + DbContext paralelo (#262), importación de servicios conexos sin 500 (#261)

## Qué se verificó

**Revisión de código línea por línea:**
- **#263**: `POST /api/solicitudes/{id}/lineas:bulk` ya no descarta `horasCargue`/`horasDescargue` por línea.
- **#262**: `clsConexoCotizacionValidador` (cantidad máx. 2 decimales, `Origen` "individual"/"general"); `PATCH`/`DELETE` nuevos para conexos; causa raíz de los 500 intermitentes de `/propuesta-tarifa` identificada y corregida — `_db` (el `DbContext` de la instancia, compartido entre tareas en paralelo) se reemplazó por un `DbContext` propio por tarea (`dbGrupo`, scope-local), eliminando la condición de carrera de EF Core (no es thread-safe). El mensaje de excepción crudo en `justificacion` se reemplazó por un mensaje de negocio fijo.
- **#261**: comparación de llaves de importación ahora usa `ClaveNormalizada` (case-insensitive + normalización Unicode NFC/NFD) en vez de igualdad ordinal estricta de `Dictionary<string,...>`; validación de longitud/rango por campo ANTES de guardar, con detalle de fila en `errores[]`, sin tumbar el archivo completo.

## Merge real contra `QA`

Esta rama (`DEPLOY`) y `QA` divergieron del mismo punto (`a674cbc`, antes de #253/#257). Probé el merge real (no solo diff) — sin conflictos, confirmé que las correcciones de #253 (seeders) y el revert de Cubicador de #257 sobreviven intactos.

**Nota de proceso:** no se hizo push a `origin/DEPLOY` para evitar disparar el pipeline real de despliegue (push a `DEPLOY` corre build-and-test → deploy real a Dokploy → smoke-test, ver `.github/workflows/ci-cd-api.yml`). La PR se mergeó directo a `QA` vía GitHub; esta evidencia se sube como commit aparte directo sobre `QA`.

## Suite completa contra la base real de pruebas (2026-10-08)

- `Nexo.Domain.Tests`: 147/147.
- `Nexo.Application.Tests`: 252/252 — incluye `clsCargarLineasBulkCommandTests` (horas por línea), `clsEditarEliminarConexoCotizacionCommandTests` (PATCH/DELETE + validación de cantidad).
- `Nexo.Api.Tests`: 215/217 (2 skips preexistentes). Incluye `clsPropuestaTarifaParalelaTests.Solicitud_con_muchas_rutas_distintas_propone_todas_las_lineas_sin_errores_tecnicos` (el test que reproduce la concurrencia del fix de #262), `clsPropuestaPlazaPropiaTests`, `clsPropuestaSoloSicetacPorDaneTests`.
- Coincide exacto con lo declarado en la PR (147/252/215-217).

## Verificación en vivo (API real contra BD real, no solo tests automatizados)

- **Plantilla Excel "precinto"**: confirmado 8 columnas (antes 12), fila 2 solo con las columnas verdaderamente fijas (`Unidad`, `Categoria`, `Activo`) en blanco — una subida sin editar nunca crea un registro real (código/tipo vacíos). Revisé el código del adaptador: diseño explícito e intencional.
- **PATCH `/api/cotizaciones/{id}/conexos/{conexoId}`**: cantidad 2→5, `valorAplicado` recalculado correctamente (34601.33 × 5 = 173006.65), total de la cotización actualizado.
- **DELETE `/api/cotizaciones/{id}/conexos/{conexoId}`**: conexo eliminado, total de la cotización vuelve al valor sin conexos.
- **`origen: "general"`**: guardado y devuelto correctamente, con el objeto `servicio` completo embebido en la respuesta (campo nuevo).
- **Importación con mayúsculas distintas**: fila `ALMACENAMIENTO`/Bogotá (mayúsculas) contra el registro existente `Almacenamiento`/Bogotá → `actualizadas: 1`, sin duplicar (confirmado: sigue habiendo 9 filas de Almacenamiento, no 10).
- **Importación con fila inválida**: costo negativo → `invalidas: 1`, `errores: ["Fila 3: COSTO/VALOR negativo."]`, sin 500 y sin descartar el resto del archivo.

**Dato de prueba limpiado**: la importación de arriba cambió temporalmente el costo real de Almacenamiento/Bogotá a 99999.99 — restaurado a su valor original (34601.33) vía `PATCH /api/servicios-conexos/{id}` inmediatamente después de confirmar el resultado, sin dejar el dato de prueba en la base real.

## Resultado

Sin hallazgos bloqueantes. Los 3 fixes están completos, verificados con tests automatizados y en vivo contra la BD real.
