# QA — PR #413: El rol Comercial puede escribir en el diccionario de cliente (#411)

## Resumen

PR pequeña y de bajo riesgo: amplía quién puede **crear, editar y borrar**
términos del diccionario de cliente (`crear_termino`/`editar_termino`/
`eliminar_termino`, ya auditados en `tbl_audit_log`). Antes solo
`AdminBiblioteca`/`AdminTarifas`/`Admin`; ahora también `Comercial` — es
quien importa los Excel de sus clientes y enseña qué significa cada
término. Leer ya era de cualquier rol válido y no cambia.

- Un solo cambio de código real: `RolesEscritura` en
  `clsDiccionarioClienteController.cs` pasa de
  `"AdminBiblioteca,AdminTarifas,Admin"` a
  `"Comercial,AdminBiblioteca,AdminTarifas,Admin"`.
- Sin esquema nuevo, sin lógica de negocio nueva: el caso de uso y la
  auditoría ya existían: solo se amplía quién puede llamarlos.

## Qué se verificó

1. Diff aislado de la PR (1 commit, 4 archivos): el controller, su
   comentario XML actualizado, y los tests.
2. Confirmado que el test de roles existente se actualizó correctamente
   (`Comercial` se quitó de la lista de "prohibidos" y se agregó a la de
   "permitidos"), y que `clsRolesPorEndpointTests.cs` ganó 4 casos nuevos
   para los 3 verbos del endpoint.
3. Revisado el test nuevo
   `El_Comercial_crea_edita_y_borra_un_termino_y_cada_cambio_queda_auditado_a_su_nombre`:
   confirma que el ciclo completo (crear/editar/borrar) con un usuario
   `Comercial` real deja las 3 filas de auditoría a su propio
   `UsuarioId` — no solo que el endpoint responde 2xx, sino que la
   trazabilidad queda correcta.
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes,
   ninguno nuevo de esta PR).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1136/1136**.
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1077/1099**, 22 omitidas (las
   mismas 13+9 ya documentadas en las PRs #405 y #414).

## Datos de prueba

Ninguno manual: el test nuevo crea su propio usuario `Comercial` y sus
filas de auditoría, y las borra en el bloque `finally`.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes.
