# Reparaciones finales — Importación Excel de Tarifas de Plaza

Encontradas el 2026-10-05 al depurar un rechazo real al reimportar
`tarifas-plaza-20261005.xlsx` (exportado desde la pantalla, se le agregó una
fila nueva a mano, y esa fila se rechazó al reimportar — "1 rechazada(s)").

## Causa raíz del rechazo puntual (ya resuelta a mano)

La celda `VIGENCIA` de la fila nueva tenía formato de celda **General** en vez
de **Fecha** — el valor interno sí era una fecha válida (serial `46315` =
`2026-10-20`), pero al leerse como texto crudo, `DateOnly.TryParse("46315", ...)`
fallaba. Se corrigió copiando el formato de fecha de una fila existente y
reimportando.

## 1. BACKEND — el importador debe leer la fecha desde el valor real de la celda, no desde su texto de display

**Archivo:** `src/Nexo.Infrastructure/TarifasPlaza/Repositorios/clsEfTarifasPlazaRepository.cs`,
método `ImportarExcelAsync`, línea 93 (`vigenciaTxt = Str(row, cols, "VIGENCIA")`)
y línea 126-127 (`DateOnly.TryParse(vigenciaTxt, ...)`).

Hoy lee la celda VIGENCIA como **texto** y parsea ese texto como fecha. Si la
celda tiene formato General (pero el valor interno es una fecha real —
pasa fácil al copiar/pegar/escribir filas nuevas a mano en el Excel exportado),
el texto que sale es el número de serie crudo, no una fecha reconocible, y la
fila se rechaza aunque el dato sea correcto.

**Arreglo:** antes de parsear como texto, intentar leer la celda como fecha
directa (ClosedXML permite leer el valor numérico interno como fecha sin
importar el formato de display de la celda — método tipo `cell.GetDateTime()`
o equivalente). Solo si eso falla, caer al parseo de texto actual. Así una
celda en General con un valor de fecha válido por dentro ya no se rechaza.

Criterios de aceptación:
- [ ] El importador lee `VIGENCIA` como fecha real del valor interno de la
      celda primero, sin importar su formato de display.
- [ ] Una celda en formato General con un serial de fecha válido ya no se
      rechaza.
- [ ] El comportamiento actual (fechas en texto bien formateado) sigue
      funcionando igual — sin regresión.

## 2. FRONTEND — mostrar el detalle de errores que el backend ya devuelve

**Archivo:** `administracion_embarqa_nexo/src/app/cabina-fletes/pages/tarifas-plaza/tarifas-plaza-page.component.ts`,
línea 216-217.

El backend ya devuelve `resultado.errores` (`IReadOnlyList<string>`,
confirmado en `clsImportarTarifasPlazaResultDto`,
`src/Nexo.Application/CasosDeUso/TarifasPlaza/DTOs/TarifaPlazaDtos.cs` línea 16)
con el detalle **por fila** de cada rechazo (ej. `"fila 109: '46315' no es una
fecha de vigencia válida"`). El frontend hoy solo pinta el conteo
(`"N rechazada(s)"`), nunca el detalle — obliga a adivinar o depurar a mano
cuál fila falló y por qué, cuando el dato ya está en la respuesta.

**Arreglo:** mostrar `resultado.errores` al usuario cuando `rechazadas > 0`
(ej. en la misma notificación, o en un detalle expandible), no solo el
conteo.

Criterios de aceptación:
- [ ] Cuando `rechazadas > 0`, el usuario ve el detalle de `resultado.errores`
      (qué fila, qué dato, por qué se rechazó), no solo el número.
- [ ] El conteo resumen (`"N rechazada(s)"`) se conserva además del detalle.

## 3. El catálogo de "casos especiales" de tipo de vehículo está hardcodeado en el backend

**Archivo:** `src/Nexo.Infrastructure/Catalogos/Adaptadores/clsTiposVehiculoAdapter.cs`,
líneas 22-29 (`CasosEspeciales`).

Los tipos de vehículo "oficiales" (`2`, `2L2`, `2S2`, `3S3`, etc.) sí salen en
vivo de `tblSAPTiposVehiculoTarifario` (`dbSolucionesTI`). Pero los 5 "casos
especiales" que no tienen código en ningún catálogo real —
`CNT20/Consolidado`, `CNT20/Exclusivo`, `CNT40/Consolidado`,
`CNT40/Exclusivo`, `PBV 7500-8000` — están escritos **tal cual, a mano**, en
una lista estática en el código (`CasosEspeciales`), no en base de datos. El
propio comentario de la clase ya lo marca como algo pendiente de formalizar
("Se agregan aquí, no en la BD, hasta que se formalicen con código real —
issue pendiente, ver comentario de Sara en #210").

**Esto no debería ir hardcodeado.** Debería vivir en una tabla propia de Nexo
(o en `tbl_reglas_solo_sicetac`/`tbl_servicios_conexos`, donde ya se usan
estos mismos 5 valores) para poder editarlos sin tocar código ni volver a
desplegar, igual que cualquier otro catálogo.

Criterios de aceptación:
- [ ] Los 5 "casos especiales" (`CNT20/Consolidado`, `CNT20/Exclusivo`,
      `CNT40/Consolidado`, `CNT40/Exclusivo`, `PBV 7500-8000`) salen de una
      tabla editable, no de una lista fija en el código.
- [ ] Agregar/editar/quitar uno de estos casos no requiere un despliegue.

## 4. Reglas Solo-SICETAC necesita exportar a Excel, igual que Tarifas de Plaza

**Backend:** `src/Nexo.Api/ReglasSoloSicetac/clsReglasSoloSicetacController.cs`
— hoy solo tiene `POST importar-excel` (línea 59) y `GET plantilla-excel`
(línea 70, plantilla **en blanco**). No existe un `GET exportar-excel` que
descargue las reglas ya cargadas.

**Frontend:** `administracion_embarqa_nexo/src/app/cabina-fletes/services/reglas-solo-sicetac-api.service.ts`
— solo tiene `importarExcel` (línea 38), no hay método `exportarExcel`.

**Tarifas de Plaza sí lo tiene así** (`clsTarifasPlazaController.cs`, línea 84):
un solo `GET exportar-excel` que descarga el catálogo completo ya cargado, y
ese mismo archivo sirve de plantilla para reimportar (editar una fila,
agregar una nueva, subir de nuevo) — el mismo flujo que usamos para depurar
el hallazgo de arriba.

Reglas Solo-SICETAC debería tener el mismo flujo: exportar lo que ya existe
(no solo una plantilla vacía), para poder editarlo y reimportarlo igual que
Tarifas de Plaza.

Criterios de aceptación:
- [ ] Backend: nuevo endpoint `GET exportar-excel` en Reglas Solo-SICETAC que
      descargue las reglas ya cargadas (mismo formato que acepta `importar-excel`).
- [ ] Frontend: botón "Exportar Excel" en la pantalla de Reglas Solo-SICETAC,
      igual que ya existe en Tarifas de Plaza.

## 5. PENDIENTE — permitir editar cada línea rechazada directamente, sin volver al Excel

Hoy, cuando una línea nueva se rechaza al importar, la única forma de
corregirla es volver al Excel, arreglar la fila, y reimportar el archivo
completo otra vez. Debería poder **editarse esa línea rechazada directo en la
pantalla** (igual que se edita cualquier otra tarifa ya cargada), sin tener
que repetir todo el ciclo de exportar/editar Excel/reimportar por un solo
dato mal puesto.

Depende de que primero exista el detalle del motivo de rechazo (punto 2 de
este documento) — para poder editar la línea rechazada hay que saber antes
qué campo específico falló.

Criterios de aceptación:
- [ ] Una línea rechazada en la importación se puede editar directo desde la
      pantalla (no solo corrigiendo el Excel y reimportando).
- [ ] Al guardar la corrección, se valida con el mismo criterio que usa el
      importador (mismo catálogo de municipios, rangos de peso, línea de
      negocio, clase de viaje).

## 6. Mesa de costeo permite marcar Ganada/Perdida sin pasar por Entregar ni auto-costeo

**Archivos:**
- `administracion_embarqa_nexo/src/app/cabina-fletes/pages/cotizacion-resumen/cotizacion-resumen-page.component.ts`,
  líneas 67-74 (`puedeRegistrarResultado`).
- `administracion_embarqa_nexo/src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.html`,
  líneas 35-38.

La regla correcta existe y está bien aplicada en una sola pantalla
(`cotizacion-resumen-page.component.ts`): solo se puede registrar Ganada o
Perdida si la cotización está en estado **Entregada**, o si está en
**Borrador** y `esAutoCosteo = true` (comentario propio del código: *"Mismo
criterio del backend: una cotización auto-costeada se marca sin pasar por
Entregar"*).

Pero Mesa de costeo tiene sus propios botones "Marcar como ganada" / "Marcar
como perdida" (`mesa-costeo-page.component.html:35-38`), y esos **no aplican
esa misma validación** — solo revisan el permiso del usuario
(`access.puede('marcarGanadaPerdida')`) y, para Ganada, que haya al menos una
línea. No revisan ni el estado de la cotización ni `esAutoCosteo`.

**Resultado:** desde Mesa de costeo se puede marcar una cotización como
Perdida (o Ganada) sin que haya sido Entregada ni auto-costeada — saltándose
la regla que si está bien implementada en la otra pantalla.

Criterios de aceptación:
- [ ] Los botones "Marcar como ganada"/"Marcar como perdida" en Mesa de
      costeo aplican el mismo criterio que `puedeRegistrarResultado()`:
      habilitados solo si la cotización está Entregada, o está en Borrador
      con `esAutoCosteo = true`.
- [ ] Una cotización en Borrador sin auto-costeo no se puede marcar
      Ganada/Perdida desde ninguna de las dos pantallas.

## Hallazgos secundarios (no bloquean, pero vale la pena revisar)

- **Ciudades ambiguas sin aviso:** el importador resuelve `ORIGEN`/`DESTINO`
  contra `tbl_municipios` por nombre normalizado; si el nombre existe en 2+
  departamentos (ej. "ARMENIA" en Antioquia y Quindío), toma uno de los dos
  sin avisar cuál — silencioso, puede llevar al municipio equivocado.
- **`TipoVehiculo` no se valida contra `RangoPeso`:** el importador no cruza
  que el tipo de vehículo declarado corresponda a la banda de peso de la fila
  (ej. banda `30001-34000` debería ser `3S3` según la convención del seeder
  original, no otro código) — hoy inserta lo que venga en el Excel tal cual,
  sin avisar si no coincide con la convención esperada.
