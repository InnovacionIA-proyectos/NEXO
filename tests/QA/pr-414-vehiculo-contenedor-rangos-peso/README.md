# QA — PR #414: Vehículo de contenedor por la tabla de rangos y PORTACONTENEDORES por defecto (#381)

## Reemplaza a la PR #406

Esta PR **reemplaza a la #406**: la rama de #406 (`381-rangos-peso-contenedor`)
arrastraba un merge de `376-preferencias-asistente` que traía dentro todo el
módulo del asistente conversacional (89 archivos ajenos a esta historia),
hallazgo reportado al autor durante esta misma sesión de QA. El autor
reconoció el problema, cerró la #406 y abrió esta PR (#414) desde una rama
limpia (`381-rangos-peso-contenedor-limpia`) con **un solo commit sobre
`QA`**. Verificado: `git log origin/QA..origin/381-rangos-peso-contenedor-limpia`
muestra únicamente el commit propio de esta historia.

## Resumen

El vehículo de una línea de **contenedor** ahora sale de
`tbl_rangos_peso_contenedor` (tamaño del contenedor + peso → vehículo) en
vez de la regla vieja de rangos de SAP, y la carrocería por defecto de esas
líneas es `PORTACONTENEDORES` (antes se tomaba la más barata de la ruta,
sin mirar si era la que de verdad corresponde a un contenedor).

- **`clsRangosPesoContenedorAdapter`**: lee la tabla y la cachea 5 minutos
  (1 minuto si está vacía/no disponible). Si la tabla no se puede leer (no
  aplicada, caída la BD), `TablaDisponible=false` y **quien llama usa la
  regla que tenía antes** — nunca deja de costear por esto.
- **`clsRecomendadorDeVehiculoDeContenedor.Elegir`**: el vehículo del
  **menor** peso máximo que alcance para ese tamaño y peso; sin fila que
  alcance, `null` (no inventa un vehículo).
- **`clsTarifarioSapAdapter.ConfigDeContenedorAsync`**: en el camino oficial,
  intenta primero con la tabla de rangos (traducido a SICE-TAC vía el
  mapeo de vehículos de la PR #398); si la tabla no está disponible o la
  clase de contenedor no trae 20/40 pies, cae a la regla de rangos de SAP
  de antes (`ConfigPorRangoPesoAsync`) — **aditivo, no reemplaza el
  camino existente**.
- **Unidad `PORTACONTENEDORES` con fallback**: para líneas de contenedor
  (`claseViajeId == 1`, sin carrocería furgón), se busca primero con unidad
  `PORTACONTENEDORES`; si SICE-TAC no trae filas para esa ruta con esa
  unidad, se repite la búsqueda con la unidad de siempre (`ESTACAS` u otra
  por parámetro) — nunca deja de encontrar un piso por pedir una unidad
  que no está cargada para esa ruta.
- **`clsPropuestaTarifaAdapter`**: misma lógica para el viaje vacío de
  aprovisionamiento; además, cuando una línea de contenedor no trae
  vehículo, lo resuelve con la tabla de rangos antes de buscar el piso
  SICE-TAC (antes tomaba la fila más barata de cualquier vehículo).

## Qué se verificó

1. Confirmado que la rama reemplaza limpiamente a la #406 (un solo commit
   sobre `QA`, sin el merge contaminante).
2. Merge de `QA` en la rama de verificación: limpio, sin conflictos.
3. Diff revisado línea por línea: `clsRangosPesoContenedorAdapter.cs`
   (degradación agraciada), `clsTarifarioSapAdapter.cs` y
   `clsPropuestaTarifaAdapter.cs` (ambos con fallback a la regla anterior,
   nunca bloquean el costeo).
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1136/1136**.
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1048/1070**, 22 omitidas: las 13
   esperadas de siempre + 9 nuevas de `clsRangosPesoContenedorTests` que
   necesitan `CREATE ANY DATABASE` (mismo motivo ya documentado en la PR
   #405 para `clsMapeoVehiculoSicetacTests` — el usuario de la BD
   compartida de pruebas no tiene ese permiso).
8. Confirmado que `tbl_rangos_peso_contenedor` **ya existe en la BD real
   con 10 filas** (coincide con la carga inicial del seeder de la PR
   #400) — no fue necesario aplicar ningún script nuevo para esta PR. Las
   4 pruebas de `clsRangosPesoContenedorTests` que sí corrieron contra la
   BD real (sin necesitar su propia BD) pasaron en verde; esa clase de
   test además asegura sus propias filas y las borra al terminar, así que
   corre igual con la tabla vacía o con carga inicial.

## Datos de prueba

Ninguno insertado manualmente: `tbl_rangos_peso_contenedor` ya tenía su
carga inicial. Las filas que crean las pruebas de
`clsRangosPesoContenedorTests` (mapeos y rangos adicionales para sus
casos) las borra el propio test en su `Dispose()`.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes.
