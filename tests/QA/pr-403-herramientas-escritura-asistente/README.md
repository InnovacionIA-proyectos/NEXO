# QA — PR #403: Herramientas de escritura del asistente con confirmación (#373)

## Resumen

Tercera pieza del asistente conversacional (sobre #371/#372): el asistente
puede **proponer** cambios a una cotización, pero nunca los aplica
directamente. La persona confirma o cancela, y solo ahí se ejecutan los
mismos casos de uso de siempre, con las mismas reglas de dominio.

- **`clsHerramientaPropuestaBase`** (base de las 5 herramientas de
  escritura): valida la forma de lo pedido, comprueba que la cotización
  exista, y deja una **acción pendiente** con parámetros fijos — nunca
  escribe. Restringida a `AdminTarifas`/`Admin` (los mismos roles que editan
  cotizaciones). Límite de acciones propuestas por respuesta
  (`DEMASIADAS_ACCIONES`).
- **`ExigirOrigenDelValorAsync`**: todo valor de dinero que una herramienta
  de escritura intenta proponer (flete, retorno de contenedor, valor de
  conexo) tiene que salir de una consulta de la conversación o de lo que
  escribió la persona — reutiliza `clsVerificadorDeCifras` de la PR #402.
  Un valor sin origen se rechaza con `VALOR_SIN_ORIGEN` **antes** de crear
  la acción pendiente.
- **5 herramientas**: `proponer_agregar_flete`, `proponer_editar_flete`,
  `proponer_agregar_conexos`, `proponer_rechazar_linea`,
  `proponer_cambiar_vehiculo`. Ninguna llama a un comando de escritura; solo
  arman parámetros y un resumen legible.
- **`clsConfirmarAccionAsistenteCommand`**: solo quien tiene la conversación
  puede confirmar (`ACCION_AJENA`/403); toma la acción de forma **atómica**
  (`TomarAccionAsync`, un `ExecuteUpdateAsync` con predicado de estado —
  compare-and-swap a nivel SQL, verificado que dos confirmaciones
  simultáneas aplican el cambio una sola vez); si venció, la marca
  `vencida` y responde `ACCION_VENCIDA`. Al ejecutar, usa los **mismos
  command handlers de siempre** (`clsEditarLineaCotizacionCommand` con
  `ConfirmarSinPiso: false` fijo — el asistente nunca puede saltarse el
  piso SICE-TAC), así que las reglas de dominio (piso, `FLETE_SOLO_SICETAC`,
  cotización no editable) se evalúan igual que si lo hiciera una persona
  desde la pantalla. Un rechazo del dominio deja la acción `fallida` con el
  código exacto y lo explica en la conversación; no se aplica nada.
- **`clsCancelarAccionAsistenteCommand`**: descarta sin aplicar nada.
- Esquema nuevo y aditivo (`docs/Database/373_asistente_acciones.sql`):
  `tbl_asistente_acciones` (depende de `372_asistente_conversaciones.sql`),
  con el JSON de parámetros tal como lo validó el servidor (lo que se
  confirma es exactamente eso, no lo que diga el modelo después).

## Qué se verificó

1. Diff aislado de la PR (1 commit propio más la integración con #372, ya
   verificada en la PR #402, merge limpio sin conflictos esta vez).
2. Revisado línea por línea `clsAccionesAsistente.cs`: la ejecución al
   confirmar nunca usa el `CancellationToken` de la petición HTTP (una vez
   tomada la acción, se aplica o falla, no queda a medias por una conexión
   cerrada), y el tipo `editar_flete` fija `ConfirmarSinPiso: false` —
   confirmado que no hay forma de que el asistente salte el piso SICE-TAC.
3. Revisado `TomarAccionAsync`/`CancelarAccionAsync` en
   `clsEfConversacionesAsistenteRepository.cs`: `ExecuteUpdateAsync` con
   `Where` sobre el estado actual, devuelve `true` solo si afectó 1 fila —
   protege contra doble confirmación por condición de carrera.
4. `373_asistente_acciones.sql` revisado línea por línea: aditivo,
   idempotente, con `CHECK` de tipo/estado/JSON y verificación de solo
   lectura.
5. `dotnet build -c Release` → 0 errores (mismos 2 warnings no bloqueantes
   ya documentados en la PR #402, ninguno nuevo de esta PR).
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **1060/1060**.
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`):
   - **Primera corrida: 1011/1039, 15 errores**, todos
     `clsAsistenteAccionesApiTests` fallando por
     `Invalid object name 'db_NEXO.tbl_asistente_acciones'` — el esquema de
     esta PR no estaba aplicado en la BD real.
   - Se aplicó `373_asistente_acciones.sql` (autorizado por Kevin; solo
     DDL, sin insertar datos) y se confirmó con su propia verificación de
     solo lectura: 1 tabla, 13 columnas, 2 índices, 3 llaves foráneas, 3
     restricciones CHECK — exactamente lo esperado.
   - **Segunda corrida: 1026/1039**, 13 omitidas (las esperadas de
     siempre).

## Datos de prueba

Se aplicó el esquema (`373_asistente_acciones.sql`, solo DDL) contra la BD
real de pruebas — necesario para que la suite de Api.Tests pudiera correr.
Ningún dato de negocio se insertó manualmente; las filas que crean las
pruebas de `clsAsistenteAccionesApiTests` las borra el propio test en su
`Dispose()`.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes.
