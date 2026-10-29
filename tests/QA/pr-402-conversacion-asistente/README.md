# QA — PR #402: Conversación en tiempo real del asistente de cotización (#372)

## Resumen

Segunda pieza del asistente conversacional (sobre #371/PR #401): el endpoint
de conversación en tiempo real (`text/event-stream`), con límites, auditoría
y una compuerta anti-alucinación de cifras.

- `clsConversarAsistenteCommand`: valida mensaje (requerido, ≤ `MaxCaracteresMensaje`),
  aísla por usuario (`CONVERSACION_AJENA`/403 si la conversación es de otra
  persona), corta por límite de turnos (`ASISTENTE_LIMITE_TURNOS`) y por tope
  diario de tokens (`clsTopeDiarioIa`, medianoche hora Colombia) **antes** de
  devolver el stream — todo lo que puede fallar antes de responder es un
  error HTTP normal, nunca un evento a mitad de camino.
- Streaming (`Transmitir`): solo los mensajes con estado `completo` entran al
  historial que se le repite al modelo (uno cortado o descartado no se le
  repite). Si el cliente se desconecta, lo generado hasta ahí queda guardado
  como `incompleto` (bloque `finally`).
- **`clsVerificadorDeCifras`**: antes de entregar la respuesta como buena,
  busca cifras que "parecen dinero" (signo `$`, unidad `millones`/`mil`, o
  cerca de una palabra de precio) y verifica que cada una aparece en el
  historial o en lo que devolvió alguna herramienta. Si hay una cifra sin
  origen, la respuesta se descarta (`EstadoDescartado`) y se reemplaza por un
  aviso explícito — el modelo no puede entregar un precio inventado o
  calculado por su cuenta.
- Auditoría completa: cada herramienta usada durante la respuesta se
  registra (`tbl_asistente_herramientas_uso`) con su entrada, salida,
  resultado y latencia, incluso si falló.
- `clsAsistenteConversacionesController`: `POST /mensajes` (SSE),
  `GET` (listado propio), `GET /{id}` (detalle, dueño o Admin). Restringido a
  `AdminTarifas`/`Admin` (coincide con el rol de quien hoy usaría el
  asistente).
- Esquema nuevo y aditivo (`docs/Database/372_asistente_conversaciones.sql`):
  `tbl_asistente_conversaciones`, `tbl_asistente_mensajes`,
  `tbl_asistente_herramientas_uso`, con sus índices y FKs; idempotente, con
  verificación de solo lectura al final.

## Qué se verificó

1. **Conflicto de merge resuelto en `docs/Database/ESQUEMA_NEXO.sql`**: la
   rama de la PR traía la sección de llaves foráneas en el orden viejo
   (FKs antes de índices), y `QA` ya tiene el orden nuevo de la PR #400
   (tablas → índices → FKs → triggers). Se resolvió adoptando la estructura
   de `QA` e insertando ahí las 4 FKs nuevas del asistente
   (`FK_asistente_conversaciones_usuarios_UsuarioId`,
   `FK_asistente_mensajes_conversaciones_ConversacionId`,
   `FK_asistente_herramientas_uso_conversaciones_ConversacionId`,
   `FK_asistente_herramientas_uso_mensajes_MensajeId`), verificado con
   `grep -c` que cada una quedó una sola vez.
2. Diff aislado de la PR (1 commit propio más la integración con #371, ya
   verificada en la PR #401): el módulo de conversación completo, el
   adaptador de Anthropic para conversación, el repositorio EF, el
   controller y el esquema.
3. Revisado línea por línea `clsConversarAsistenteCommand` y
   `clsVerificadorDeCifras`: aislamiento por usuario, límites, y la regla de
   que una cifra de dinero sin origen descarta la respuesta.
4. `tests/Nexo.Api.Tests/clsRolesPorEndpointTests.cs`: 3 casos nuevos que
   verifican que los 3 endpoints de conversación están restringidos a
   `AdminTarifas`/`Admin`.
5. `dotnet build -c Release` → 0 errores. **2 warnings nuevos no
   bloqueantes, propios de esta PR** (no afectan el comportamiento):
   `CS1573` (falta la etiqueta `<param>` de `Mensaje` en el comentario XML
   de `clsEnviarMensajeRequest`) y `xUnit2013` (un `Assert.Equal` de tamaño
   de colección que debería ser `Assert.Single`, en
   `clsAsistenteConversacionApiTests.cs:377`). Ninguno rompe build ni
   pruebas; quedan anotados aquí por transparencia, no bloquean la PR.
6. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
7. `dotnet test tests/Nexo.Application.Tests` → **1023/1023**.
8. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **primera corrida: 12 errores**,
   todos `Invalid object name 'db_NEXO.tbl_asistente_herramientas_uso'`
   porque el esquema de esta PR no estaba aplicado en la BD real todavía.
   Se aplicó `372_asistente_conversaciones.sql` (autorizado por Kevin; solo
   DDL, sin insertar datos) y se confirmó con su propia verificación de
   solo lectura: 3 tablas, 6/7/11 columnas, 4 índices, 4 llaves foráneas —
   exactamente lo esperado. **Segunda corrida: 996/1009**, 13 omitidas (las
   esperadas de siempre).

## Datos de prueba

Se aplicó el esquema (`372_asistente_conversaciones.sql`, solo DDL) contra
la BD real de pruebas — necesario para que la suite de Api.Tests pudiera
correr. Ningún dato de negocio se insertó manualmente; las filas que crean
las pruebas de `clsAsistenteConversacionApiTests` las borra el propio test
en su `Dispose()` (usuarios, conversaciones, mensajes y uso de herramientas
propios de cada caso).

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes (2 warnings de
estilo no bloqueantes, documentados arriba).
