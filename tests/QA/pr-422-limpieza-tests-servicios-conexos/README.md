# QA — PR #422: Las pruebas de servicios conexos borran lo que crean (#421)

## Resumen

Responde directamente al hallazgo reportado en la **issue #421** durante la
verificación de la PR #418: toda la clase `clsServiciosConexosTests` creaba
servicios de prueba (tipo `Precinto`/`Precio de venta`) sin borrarlos nunca,
acumulando más de mil filas huérfanas en la BD compartida de pruebas, lo que
hacía que un test que buscaba sus propias filas por `q=Precinto` genérico
fallara de forma reproducible cuando la paginación no las incluía.

- **`clsServiciosConexosTests` ahora implementa `IDisposable`**: cada
  servicio que crea una prueba se registra con el helper `Cod(prefijo, largo)`
  (código único con un prefijo reconocible), y `Dispose()` borra, al
  terminar, la auditoría y los servicios que ella misma creó —
  **mejor esfuerzo** (atrapa `SqlException`, un bloqueo ajeno no tumba una
  prueba que ya pasó) y **protegido**: nunca borra un servicio que ya esté
  usado en una cotización (`!db.CotizacionConexos.Any(...)`).
- **El test que fallaba
  (`Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio`) ahora busca
  por su propio código exacto** (`q={codigo}`), no por `q=Precinto`
  genérico — exactamente la corrección que señalé en la issue #421: ya no
  depende del tamaño de página ni del orden del listado.
- **`docs/Database/421_limpiar_servicios_de_prueba.sql`**: script de
  limpieza **único, no para producción**, que borra lo ya acumulado antes
  de esta PR. Reconoce una fila de prueba con 3 condiciones a la vez (para
  no tocar el catálogo real): código con uno de los 16 prefijos conocidos
  seguido de un sufijo hexadecimal de 11+ caracteres (lo que genera
  `Guid.NewGuid()` en los tests), `TipoServicio` en (`Precinto`,
  `Precio de venta`), y **ninguna cotización usándolo**. Idempotente, con
  verificación de solo lectura antes y después.

## Qué se verificó

1. Diff aislado de la PR (1 commit, 4 archivos), merge limpio sin
   conflictos.
2. Revisado `Dispose()`: borra primero la auditoría de importaciones
   masivas de su propia ventana de tiempo, luego la auditoría de los
   servicios por id, luego los servicios — y nunca toca uno que ya esté
   en una cotización.
3. **Aplicado el script de limpieza contra la BD real de pruebas**
   (autorizado explícitamente por Kevin, al ser un `DELETE` sobre datos,
   no solo DDL): verificado antes con consulta directa que había **1.594
   filas** con `TipoServicio = 'Precinto'`; corrido el script completo;
   verificado después con la misma consulta directa que el conteo es
   **0**. El total de `tbl_servicios_conexos` bajó a 1.351 (solo catálogo
   real, nada de prueba).
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1182/1182** (sin
   cambios, la PR solo toca tests de Api).
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1141/1163, 0 errores**, 22
   omitidas (las esperadas de siempre). **El error de
   `Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio` ya no
   aparece** — confirmado que el hallazgo de la issue #421 quedó
   completamente resuelto.

## Datos de prueba

Se aplicó `421_limpiar_servicios_de_prueba.sql` contra la BD real de
pruebas, borrando 1.594 filas huérfanas de corridas anteriores de la
propia suite de tests (no del catálogo real). A partir de esta PR, cada
test de `clsServiciosConexosTests` limpia lo que crea en su propio
`Dispose()`.

## Resultado

Verificación técnica completa, sin hallazgos. Cierra la issue #421.
