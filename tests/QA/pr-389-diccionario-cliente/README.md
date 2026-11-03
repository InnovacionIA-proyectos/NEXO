# QA — PR #389: Diccionario de términos por cliente (tabla y CRUD)

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/389
**Autor**: ivasco-dev
**Rama**: `358-diccionario-cliente` → `QA`
**Cierra**: #356 y #358
**Depende de**: nada. Es independiente de #387; #390 y #391 están apiladas sobre esta PR.

## Qué cambia

- Tabla `db_NEXO.tbl_diccionario_cliente` (script idempotente `docs/Database/356_diccionario_cliente.sql` y `ESQUEMA_NEXO.sql`): cómo traduce cada cliente sus columnas, unidades y valores a lo que entiende NEXO (por cliente, o general con `ClienteId` nulo). Tres tipos (`columna`, `unidad`, `valor`; el `valor` exige `Contexto`), FK a `tbl_clientes`, índice único sobre `(ClienteId, TerminoNormalizado, Tipo, Contexto)` y un índice por cliente/tipo.
- `/api/diccionario-cliente`: listar con filtros y `opciones` (cualquier rol válido); crear, editar y borrar (`AdminBiblioteca`, `AdminTarifas`, `Admin`), con **auditoría** en las tres operaciones.
- Validaciones con código estable y `409 TERMINO_DUPLICADO` sin distinguir mayúsculas ni tildes.
- Mapeo en `NexoDbContext`, DI, `Program.cs`, `CONTRATO.md` y `ROLES_Y_PERMISOS.md`.

## Conflicto de merge con #387 (resuelto por QA)

Al integrar `QA` en la rama apareció un conflicto en `src/Nexo.Api/clsProgram.cs` con #387 (registro de uso de la IA): ambas PRs agregan su propio `using` y su propio `AddXxxUseCases()` en las mismas líneas. Es **aditivo**: se conservaron los dos (`DiccionarioCliente` e `IA`). `clsDependencyInjection.cs` y `clsNexoDbContext.cs` se fusionaron solos y conservan lo de ambas PRs (comprobado por búsqueda). Validado con `dotnet build` Release: 0 errores. Quedó en el commit de merge `61d45c2`; por eso el diff final de la rama no es idéntico al que se abrió.

## Cómo se verificó

1. Checkout de la rama con `QA` integrada (ya con #384–#387) y resolución del conflicto de arriba.
2. Revisión del código: controlador (roles), casos de uso (validaciones, duplicados, auditoría), repositorio, mapeo y script SQL. Escaneo de secretos del diff: 0 coincidencias.
3. **SQL** — [`verificar-sql.ps1`](./verificar-sql.ps1): SQL Server **local y descartable** en Docker (2019 y 2022; la base de pruebas corporativa es la versión 15 = 2019), sin tocar ninguna base corporativa. **68 comprobaciones OK, 0 fallos**, sin contenedores residuales:
   - *Base existente* (esquema previo + script `356`): corre sin errores; deja 1 tabla / 11 columnas / 2 índices / 1 FK / 2 CHECK; **idempotente**; **reversa** (`DROP TABLE`) y reaplicar funcionan.
   - *Restricciones* con datos ficticios (15 casos): tipo inválido, `valor` sin contexto, `columna` con contexto y cliente inexistente → 547; término, normalizado o concepto nulos → 515; **duplicados → 2601** (un término general repetido y uno de cliente repetido, comprobando que el índice único trata los `NULL` como iguales, como dice el script); mismo texto como general vs de cliente, o con otro contexto → se acepta; no se puede borrar un cliente que tiene términos → 547. Valores por defecto correctos.
   - *Base nueva* (`ESQUEMA_NEXO.sql` completo): la PR **no agrega errores nuevos** y deja una estructura **idéntica** a la del camino anterior (columnas, defaults, CHECK, PK, FK, índices e índice único).
4. **.NET** — [`verificar.ps1`](./verificar.ps1), con `ANTHROPIC_API_KEY` y `Anthropic__ApiKey` forzadas a vacío:
   - Build Release: 0 errores.
   - Tests del diccionario: **10/10** ✅ (API contra la tabla real: roles, ciclo completo, términos generales, duplicados, validaciones, opciones).
   - `Nexo.Domain.Tests` **218/218** ✅ · `Nexo.Application.Tests` **557/557** ✅ (24 nuevos).
   - `Nexo.Api.Tests` completo contra la BD real: **824 pasados, 10 omitidos** de 834, 0 fallos ✅ (los 10 omitidos son los de siempre en este ambiente).

## Pruebas de mutación

- **Crear sin la comprobación de duplicados:** falla **exactamente 1** test (`Un_termino_repetido_es_409_...`).
- **`POST` sin la restricción de roles de escritura:** falla **exactamente 1** test (`Escribir_solo_lo_pueden_AdminBiblioteca_AdminTarifas_y_Admin`).
- Control con el código restaurado: **10/10**. `git status` limpio salvo esta carpeta.
- *Nota de proceso:* una primera pasada de la segunda mutación dio un falso resultado (2 fallos, y los 10/10 no volvían tras restaurar). No era de la PR: `Copy-Item` conserva la fecha de modificación original, así que el archivo restaurado quedaba **más viejo** que los binarios mutados y MSBuild no lo recompilaba (aquí los dos archivos están en proyectos distintos, Application y Api). Se corrigió tocando el archivo al restaurarlo y se repitió todo; el resultado de arriba es el de la corrida corregida. Las mutaciones de PRs anteriores no se vieron afectadas (cada una tocaba un archivo distinto del mismo proyecto, que se recompila entero).

## Datos de prueba en la base real

- Los tests escriben términos con una marca única (`T358<guid>`) y los borran en un `finally`. Con conteos de solo lectura: **0 términos residuales** tras la corrida completa, las mutaciones y el diagnóstico.
- **Auditoría:** cada crear/editar/borrar deja una fila en `tbl_audit_log`, que no tiene camino de borrado. Antes de empezar había **65** filas de `diccionario_cliente` (y 35 496 en total). Tras la corrida completa de verificación eran **107** (+42), y al terminar todo el trabajo de QA de esta PR son **202** (+137 respecto al inicio): esa diferencia viene de las repeticiones de los tests del diccionario que hizo QA (el diagnóstico de concurrencia, las mutaciones y el episodio de binarios obsoletos de la nota de proceso). Son registros permanentes que generó QA; por eso conviene saber que cada corrida de esos 10 tests suma unas 20 filas. Es la práctica que ya tiene el repo (la tabla acumula decenas de miles de filas de pruebas), no algo nuevo de esta PR, pero un camino de limpieza para la auditoría de pruebas ahorraría ruido.

## Hallazgo 1 — Dos creaciones simultáneas del mismo término dan `500`, no `409` (no bloquea, pero conviene corregirlo)

`TERMINO_DUPLICADO` se decide con «comprobar y luego insertar» (`BuscarDuplicadoAsync` y después `SaveChangesAsync`). Si dos peticiones pasan la comprobación a la vez, la que pierde choca con el **índice único** de la base y esa excepción no se traduce: el único `DbUpdateException` que maneja el código es el de servicios conexos.

Diagnóstico temporal (guardado como [`diagnostico-duplicado-concurrente.cs.txt`](./diagnostico-duplicado-concurrente.cs.txt), fuera de la suite porque fallaría en el CI hasta que se corrija): 8 `POST` simultáneos del mismo término como Admin contra la API y la base reales →

- **1 × 201 y 7 × `500 INTERNAL_ERROR`** (se esperaba 1 × 201 y 7 × `409 TERMINO_DUPLICADO`).
- La integridad **se conserva**: queda 1 sola fila en la base, gracias al índice único (esto además confirma que el índice existe en la base de pruebas corporativa).

Impacto: bajo en uso normal (dos administradores creando el mismo término a la vez), pero más probable en la **carga inicial del diccionario** (#357) si se hace por la API en paralelo. Sugerencia: capturar la violación del índice único (errores 2601/2627 de SQL Server) al guardar y devolver `409 TERMINO_DUPLICADO`, y volver a correr el diagnóstico (esperado: 1 × 201 y 7 × 409).

## Hallazgo 2 (ajeno a la PR, agrava el de #386) — en una base nueva, la tabla del diccionario queda sin su índice único

Con el defecto preexistente de `ESQUEMA_NEXO.sql` (ver README de #386: un error en una FK de `OutboxMessage` aborta el lote y se pierden las FK e índices posteriores), una base creada con el esquema completo deja la tabla de esta PR como `1 tabla | 11 columnas | 0 índices | 0 FK | 2 CHECK`: **sin el índice único** que garantiza que un término no se repita y sin la FK a `tbl_clientes`. En esa base, la unicidad dependería solo de la comprobación de la aplicación, que es justo la que falla bajo concurrencia (hallazgo 1). La base de pruebas corporativa no está afectada (tiene el índice, como demuestra el diagnóstico).

## Observaciones (no bloquean)

1. **Lectura abierta a cualquier rol válido**, como dice la PR; la respuesta incluye el nombre del cliente (`clienteNombre`). Coincide con la matriz de roles documentada.
2. **El borrado es duro** (sin papelera ni historial más allá de la auditoría).
3. La edición reemplaza todos los campos (el cliente no cambia); no hay edición parcial.
4. Como la PR usa «Cierra #356 y #358», ambos issues se cerrarán solos al mergear.

## Resultado

Sin hallazgos bloqueantes para el merge. Recomendación: aprobar y registrar el hallazgo 1 como issue para el autor.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-389-diccionario-cliente/verificar-sql.ps1   # Docker; sin VPN
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-389-diccionario-cliente/verificar.ps1        # .env.pruebas-reales.local + VPN
```
