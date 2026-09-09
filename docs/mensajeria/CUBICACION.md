# Puente de mensajería con Cubicador (issue #28, S2-HU-075)

Documento de cierre del criterio "contrato de mensajes documentado". Cubicador
(`cubicador/`, microservicio Python, mismo repo) ya documenta este mismo
contrato desde su lado en `cubicador/CONTRATO.md` sección 6 — este
archivo es la versión NEXO/C# del mismo contrato, más las decisiones que solo
aplican de este lado.

**Decisión del equipo (2026-08-27)**: MassTransit nativo — no la aproximación
previa con RabbitMQ.Client directo (probada primero, descartada). Todo sigue
viajando por el mismo broker RabbitMQ compartido.

## 0. Cumplimiento contra el texto original de la HU-075

> Objetivo: el servicio Python consume los trabajos de cubicación desde
> RabbitMQ y publica resultados/rechazos por cola de respuesta — NADA
> síncrono entre backend y motor. Reintentos y DLQ según la topología de
> S2-HU-04.1. Conecta con la saga de HU-04 y alimenta el flujo de entrega de
> HU-08.

| Frase del objetivo | Estado |
|---|---|
| "el servicio Python consume los trabajos... y publica resultados/rechazos por cola" | ✅ Cumple |
| "NADA síncrono entre backend y motor" | ⚠️ El puente en sí no hace ninguna llamada HTTP — pero sigue sin resolver cómo se crea el plan+ítems en la base de Cubicador antes de solicitar la cubicación (ver sección 2, precondición) |
| "Reintentos y DLQ según la topología de S2-HU-04.1" | ✅ **Ahora literal**: `UseMessageRetry`/`Fault<T>` reales de MassTransit — mismo mecanismo, mismos intervalos (1s/5s/15s/60s), que el resto de NEXO. Verificado con infraestructura real, ver sección 3 |
| "Conecta con la saga de HU-04 y alimenta el flujo de entrega de HU-08" | ❌ No implementado — decisión explícita: `AutoCosteoSaga` es puramente de costeo por diseño, y HU-08 no tiene ningún artefacto en el repo todavía |

Los 3 criterios de cierre cumplen — ver `tests/QA/28-s2-hu-075-.../README.md`
para la evidencia completa.

## 1. Por qué esto NO es un `IConsumer<T>` estándar (topología manual)

Cubicador declara su propio exchange topic (`cubicador.eventos`) con routing
keys semánticas (`cubicacion.solicitada`, `plan-de-cargue.generado`,
`cubicacion.rechazada`) — no el exchange-por-tipo que MassTransit crea por
defecto. La topología se anula explícitamente SOLO para estos 3 mensajes
(`MensajeriaDependencyInjection.cs`):

- **Publicar** `CubicacionSolicitada`: `cfg.Message<T>(m =>
  m.SetEntityName("cubicador.eventos"))` + `cfg.Publish<T>(p => p.ExchangeType
  = ExchangeType.Topic)` fijan el exchange; el **routing key** se fija por
  mensaje al publicar (`IPublishEndpoint.Publish(mensaje, ctx =>
  ctx.SetRoutingKey("cubicacion.solicitada"))`) — no hay forma de fijarlo a
  nivel de topología para RabbitMQ en MassTransit 8.5, confirmado en runtime
  (ver "Hallazgo real" más abajo).
- **Consumir** `PlanDeCargueGenerado`/`CubicacionRechazada`: dos
  `cfg.ReceiveEndpoint(nombre, e => { e.ConfigureConsumeTopology = false;
  e.Bind("cubicador.eventos", b => {...routing key...}); })` manuales,
  configurados ANTES de `Tuberia(...)` para heredar su política de
  reintentos (ver sección 3).

**Por qué el puerto genérico `IPublicadorDeMensajes` no sirve para publicar
`CubicacionSolicitada`** (hallazgo real, no obvio): ese puerto
(`Task PublicarAsync<T>(T mensaje, CancellationToken ct)`) no expone forma de
fijar el routing key del mensaje. Sin `cubicacion.solicitada` como routing
key, el mensaje llega al exchange `cubicador.eventos` (confirmado con las
métricas del exchange: `publish_in: 1`) pero no calza con el binding de la
cola de Cubicador y se pierde en silencio — un exchange topic sin bind que
matchee no entrega el mensaje a nadie, y no es un error. Por eso
`CubicacionPublicadorAdapter` inyecta `IPublishEndpoint` directo (sigue
recibiendo el Outbox transaccional — el Outbox intercepta a nivel de
`IPublishEndpoint`, no del puerto genérico) y replica a mano la validación
que `PublicadorValidado` daría gratis (`IValidadorDeMensaje<T>`, mismo
criterio "todo mensaje que sale al broker debe declarar sus invariantes").

**Nombres de endpoint con prefijo `Cubicacion.`** (`Cubicacion.
PlanDeCargueGenerado`, `Cubicacion.CubicacionRechazada`): el nombre de
endpoint por defecto de `AlertaDeFalloConsumer<T>` es el nombre del TIPO de
mensaje, no el de la clase consumer — sin el prefijo, colisiona con estos
endpoints manuales (mismo nombre exacto) y MassTransit revienta al arrancar
con *"A receive endpoint with the same key was already added"* — confirmado
en runtime, no una suposición.

## 2. Contrato de mensajes

### Comando saliente: `CubicacionSolicitada` (NEXO → Cubicador)

Exchange `cubicador.eventos`, routing key `cubicacion.solicitada`. El BODY es
JSON plano (Cubicador no entiende el envelope de MassTransit — ver sección
4), con los mismos nombres de campo que ya lee `worker/main.py::
_ejecutar_solicitud`:

```json
{
  "tipo": "optimizar",
  "planId": "<uuid>",
  "correlationId": "<uuid, igual a planId>",
  "solicitud": {
    "estrategia": "Combinada", "algoritmo": "bfd", "semilla": null,
    "ponderaciones": null, "respetarBloqueadas": false
  }
}
```

**Precondición importante, no obvia**: `planId` debe ser el Id de un plan que
YA EXISTE en la base de datos PROPIA de Cubicador (con sus ítems) — Cubicador
optimiza un plan que él mismo persiste, no uno que solo NEXO conoce. Hoy nada
crea ese plan del lado de Cubicador a partir de esta solicitud: quien dispare
`ICubicacionPort.SolicitarCubicacionAsync` primero necesita que el plan (con
sus ítems) ya exista en Cubicador — vía su propia API REST, o vía el puente
`crear_desde_linea`, que a su vez llama de vuelta a NEXO
(`GET /api/lineas-solicitud/{id}/cubicacion`,
`LineasSolicitudCubicacionController`, autenticación
`CubicadorApiKeyAuthenticationHandler` — ver `cubicador/CONTRATO.md`
sección 7). Verificado de punta a punta.

### Eventos entrantes: Cubicador → NEXO

Mismo exchange, consumidos por `PlanDeCargueGeneradoConsumer`/
`CubicacionRechazadaConsumer` en las colas `Cubicacion.PlanDeCargueGenerado`/
`Cubicacion.CubicacionRechazada`.

**`plan-de-cargue.generado`**: `{ "correlationId", "planId", "resultado":
{ "estado", "puntaje", "colocaciones" } }` → actualiza `PlanCargue.Estado`/
`Puntaje` (reactivando una tabla que existía en el `DbContext` sin ningún
código que la usara).

**`cubicacion.rechazada`**: `{ "correlationId", "planId", "codigo",
"mensaje" }` → marca `PlanCargue.Estado = "error"`.

Ambos shapes protegidos por `tests/CabinaFletes.Api.Tests/Cubicacion/
ContratoCubicacionTests.cs` (sin broker, solo (de)serialización) — copiados
literal de lo que publica `RabbitMqEventosPublisher` del lado de Cubicador.

## 3. Reintentos + DLQ + alerta — MassTransit real, verificado

A diferencia de la primera versión de este puente (RabbitMQ.Client directo,
reintentos manuales), `PlanDeCargueGeneradoConsumer`/
`CubicacionRechazadaConsumer` son `IConsumer<T>` REALES: heredan
`UseMessageRetry` (1s/5s/15s/60s) y el `Fault<T>` automático de
`Tuberia<T>` — exactamente la misma tubería que usa el resto de NEXO. La
alerta es literal la misma clase genérica de la #13
(`AlertaDeFalloConsumer<PlanDeCargueGenerado>`/`<CubicacionRechazada>`), cero
código nuevo de alerta.

**Verificado contra infraestructura real (2026-08-27)** — se publicó un
envelope de MassTransit bien formado (`messageType` correcto) pero con
`resultado: null`, forzando una `NullReferenceException` real dentro del
consumer (no un fallo de deserialización — esos son un `ReceiveFault`
distinto, que NO pasa por reintentos ni por `AlertaDeFalloConsumer`, ver nota
al final). Log real:

```
21:32:00 warn: R-RETRY rabbitmq://rabbit/Cubicacion.PlanDeCargueGenerado ...
  System.NullReferenceException: Object reference not set to an instance of an object.
  at CabinaFletes.Infrastructure.Cubicacion.PlanDeCargueGeneradoConsumer.Consume(...)
21:33:21 dbug: SEND rabbitmq://rabbit/MassTransit:Fault--CabinaFletes.Mensajes.Cubicacion:PlanDeCargueGenerado-- ...
  MassTransit.Fault<CabinaFletes.Mensajes.Cubicacion.PlanDeCargueGenerado>
21:33:21 fail: R-FAULT rabbitmq://rabbit/Cubicacion.PlanDeCargueGenerado ... (agotados los reintentos)
21:33:21 info: SmtpCorreoAdapter Correo enviado a scalderon@boterosoto.com.co: [NEXO] Mensaje PlanDeCargueGenerado cayó a la cola de error
  (... 5 correos más, uno por destinatario)
21:33:21 dbug: RECEIVE rabbitmq://rabbit/PlanDeCargueGenerado ... Fault<PlanDeCargueGenerado>
  AlertaDeFalloConsumer<CabinaFletes.Mensajes.Cubicacion.PlanDeCargueGenerado>
```

81 segundos entre el primer `R-RETRY` (21:32:00) y el `Fault` (21:33:21) — 
coincide exacto con 1s+5s+15s+60s. Confirmado también en el capturador SMTP
de pruebas: `rowCount: 6`, los 6 destinatarios de `Alertas:EquipoTecnico`.

**Nota importante, encontrada probando esto**: un mensaje que ni siquiera es
JSON válido (probado también) no genera este mismo camino — MassTransit lo
clasifica como `ReceiveFault` (falla al deserializar el ENVELOPE, antes de
saber de qué tipo es el mensaje), que NO pasa por `UseMessageRetry` ni por
`AlertaDeFalloConsumer<T>` (ese filtro solo actúa sobre `Fault<T>`, que
requiere haber identificado el tipo del mensaje primero). Distinción real
entre dos categorías de fallo de MassTransit, no una suposición.

## 4. Compatibilidad con Cubicador (Python) — RESUELTO

**Estado inicial (2026-08-27, primer intento)**: Cubicador publicaba/consumía
JSON plano vía `aio_pika` — sin el envelope de MassTransit (`messageType`,
`message` anidado). Al publicar `CubicacionSolicitada` con el envelope real
de MassTransit, el worker de Cubicador fallaba con `KeyError: 'tipo'`
(`cuerpo["tipo"]` ya no existía en la raíz), y su propio manejo de error
también reventaba con el mismo `KeyError` al construir el movimiento a DLQ —
el mensaje ni siquiera llegaba a `cubicacion.dlq`.

**Solución implementada** (mismo día, ver `cubicador/docs/
DECISIONES_TECNICAS.md` #12 para el detalle completo del lado Python):
Cubicador NO adoptó MassTransit ni ninguna librería .NET — solo aprendió a
hablar el shape de JSON que el envelope necesita, en los 2 puntos exactos
donde cruza la frontera con NEXO:

- **Al consumir** `cubicacion.solicitada`
  (`worker/main.py::_procesar`): si el JSON trae `message`+`messageType`, se
  desenvuelve antes de procesar; si no, se usa tal cual — fallback
  transparente, Cubicador consigo mismo sigue sin sobre.
- **Al publicar** `plan-de-cargue.generado`/`cubicacion.rechazada`
  (`infrastructure/mensajeria/rabbitmq.py::_sobre_masstransit`): se envuelven
  con el `messageType` exacto de la clase C# correspondiente — acoplamiento
  cruzado real y aceptado (si NEXO renombra esa clase, Cubicador queda
  desincronizado en silencio, sin error de compilación que lo avise;
  protegido hoy solo por `ContratoCubicacionTests.cs` de este lado y la
  verificación E2E de `tests/QA/28-.../README.md`).

**Verificado de punta a punta con infraestructura real (mismo día, segunda
corrida)**: plan creado en Cubicador → `CubicacionSolicitada` publicada por
NEXO con MassTransit real → worker de Cubicador desenvuelve y optimiza
correctamente → `plan-de-cargue.generado` publicado envuelto → NEXO lo
consume → `PlanCargue` actualizado: **`estado=optimizado, puntaje=75.00`**.
El flujo feliz completo del criterio 1 y 3 ya no depende de intervención
manual — funciona con el Cubicador real, tal como quedó en esta rama.

## 5. Limitaciones conocidas, honestas (no resueltas por esta HU)

- **Sin trigger real todavía**: no hay endpoint HTTP ni consumer que llame a
  `ICubicacionPort` — se verificó con un harness de prueba (ver QA), no
  desde un flujo de usuario real.
- **Datos duplicados entre dos bases**: ver la precondición de la sección 2.
- **El Outbox exige disciplina de llamada**: `ICubicacionPort.
  SolicitarCubicacionAsync` solo se entrega si el MISMO `DbContext` llama a
  `SaveChangesAsync()` DESPUÉS (no antes) de publicar — confirmado en
  runtime: la primera versión de la verificación llamó a `SaveChangesAsync`
  antes de publicar, sin una llamada posterior, y el mensaje nunca se
  escribió en `OutboxMessage` (0 filas). Mismo orden que ya usa
  `CostearLineaCommand.cs` en el resto de NEXO — no es una regla nueva, solo
  hay que respetarla cuando alguien conecte un trigger real.

---

## 6. S3-HU-02 — los 8 comandos de escritura del plan de cargue (issue #108)

Contrato publicado para que el frontend (S3-HU-04 en
`administracion_embarqa_nexo`) pueda construir en paralelo. **Si algo de esto
cambia después de publicado, se avisa en la issue #108 — nunca en silencio**
(compromiso explícito de la HU).

Las 2 decisiones de diseño detrás de esta sección (S3-HU-01) están registradas
como ADR en `docs/decisiones/`:
[`ADR-planid-generado-por-nexo.md`](../decisiones/ADR-planid-generado-por-nexo.md)
(issue #102) y
[`ADR-ajuste-manual-async.md`](../decisiones/ADR-ajuste-manual-async.md)
(issue #101).

### 6.0 Qué cambia

Hasta esta HU, el navegador podía hablarle directo a Cubicador para las
operaciones de escritura del plan de cargue (POST/DELETE/PATCH síncronos de
`cubicador/api/routers/planes_cargue.py`, ver `cubicador/CONTRATO.md` §3.2).
Eso se acaba: **los 8 endpoints nuevos viven en NEXO** (nunca una URL de
Cubicador) y publican por el MISMO camino de cola que ya usa `optimizar`
(sección 2 de este documento) — mismo exchange `cubicador.eventos`, mismos
reintentos 1s/5s/15s/60s, mismo DLQ, misma alerta. La única pieza compartida
con `optimizar` es el tipo de mensaje `CubicacionSolicitada`: su campo
`Solicitud` pasó de `SolicitudDeCubicacion` fijo a `object` — cada uno de los
9 "Tipo" posibles (`optimizar` + los 8 de abajo) trae su propio payload, y
`CubicacionSolicitadaValidador` (issue #103) valida cuál corresponde a cuál.
A propósito, ninguno de los 8 pasa por `IParidadCubicadorPort`: son
operaciones de datos, no de cálculo — la compuerta de paridad solo bloquea
`optimizar`.

### 6.1 Los 8 endpoints

| # | Método y ruta | Comando NEXO | Tipo de cola | Respuesta |
|---|---|---|---|---|
| 1 | `POST /api/planes-cargue` | `CrearPlanCommand` | `crear_plan` | **202** `SolicitudDeCubicacionAceptadaDto` |
| 2 | `POST /api/planes-cargue/demo` | `CrearPlanDemoCommand` | `crear_plan_demo` | **202** `ComandoAceptadoDto` |
| 3 | `POST /api/vehiculos` | `GuardarVehiculoCommand` | `guardar_vehiculo` | **202** `ComandoAceptadoDto` |
| 4 | `POST /api/planes-cargue/{id}/items` | `GuardarItemCommand` | `guardar_item` | **202** `ComandoAceptadoDto` |
| 5 | `POST /api/planes-cargue/{id}/items/importar` | `ImportarItemsCommand` | `importar_items` | **202** `ComandoAceptadoDto` |
| 6 | `DELETE /api/planes-cargue/{id}/items/{itemId}` | `EliminarItemCommand` | `eliminar_item` | **202** `ComandoAceptadoDto` |
| 7 | `DELETE /api/planes-cargue/{id}` | `EliminarPlanCommand` | `eliminar_plan` | **202** `ComandoAceptadoDto` |
| 8 | `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}` | `AjustarColocacionCommand` | `ajustar_colocacion` | **202** `ComandoAceptadoDto` |
| 9 | `DELETE /api/vehiculos/{id}` | `EliminarVehiculoCommand` | `eliminar_vehiculo` | **202** `ComandoAceptadoDto` |

> **Por qué son 9 y no 8.** El #9 no venía en la lista original de la HU, y se
> agregó porque su ausencia bloqueaba S3-HU-05. El frontend llama
> `eliminarVehiculo()` (`cubicador-api.service.ts`), así que sin este comando esa
> escritura seguiría saliendo del navegador directo a Cubicador con la API key en
> el bundle — y retirar el CORS y rotar la clave exige **cero** llamadas de
> navegador. Cerrar el par de `guardar_vehiculo` era la única forma de no dejar
> ese hilo suelto. Ver el comentario de `EliminarVehiculoCommand`.

Todos exigen rol `Comercial`, `CoordinadorFletes` o `Admin` (mismo criterio
que `POST /api/planes-cargue/{id}/optimizar`), **salvo `DELETE /api/vehiculos/{id}`,
que exige `Admin`**: el catálogo de vehículos es maestro y lo comparten todos los
planes, a diferencia del alta, que un comercial puede necesitar en medio de una
cotización. Todos usan la forma de error
uniforme de la sección 6 de `docs/contrato-integracion/README.md`
(`{code, message, timestamp, details}`).

```csharp
public record SolicitudDeCubicacionAceptadaDto(Guid PlanCargueId, string Estado, string ConsultarEn);
public record ComandoAceptadoDto(Guid CorrelationId, string Estado);
```

### 6.2 Por qué los 8 responden 202 (no 200/201/204) — decisión documentada

La issue #104 pide decidir esto endpoint por endpoint. La decisión fue
**202 para los 8, sin excepción**, por consistencia con el resto del
contrato de trabajos pesados (sección 4 de `docs/contrato-integracion/
README.md`) — incluso donde un `200`/`201`/`204` síncrono hubiera sido
defendible:

- **`POST /api/planes-cargue` (#1)**: es el único de los 8 con un matiz real.
  NEXO genera el `PlanId` y deja su fila espejo **en el mismo POST** (antes de
  responder) — un `GET /api/planes-cargue/{id}` inmediato después del 202 ya
  funciona, aunque la fila REAL de Cubicador se cree async. Se documenta como
  202 igual, para no prometer un 201 que no sería del todo cierto (la
  escritura definitiva sigue pendiente en Cubicador). Esa fila nace en
  **`procesando`** — ver el aviso de cambio de contrato de abajo.
- **`DELETE /api/planes-cargue/{id}` (#7)**: un DELETE normalmente responde
  `204`. Acá responde **202** porque el borrado en Cubicador es async — la
  fila espejo de NEXO sí se borra de inmediato (optimista, ver 6.4), pero
  prometer `204` implicaría que el borrado ya es un hecho de punta a punta.
- El resto (#2 a #6, #8) no tenían candidato razonable a síncrono: dependen
  de que Cubicador aplique el comando en su propia base.

#### ⚠️ Cambio de contrato — `POST /api/planes-cargue` devuelve `procesando`

**Publicado el 2026-09-09**, después de la publicación inicial del contrato.
Se avisa acá y en el hilo de la issue, según el compromiso de S3-HU-02.6 (los
cambios de contrato no se hacen en silencio).

**Qué cambia:** el campo `estado` del `202` pasa de `"borrador"` a
`"procesando"`. La fila espejo de NEXO nace en `procesando` y
`PlanDeCargueGeneradoConsumer` la mueve al estado real cuando llega la
confirmación de Cubicador (ese consumer ya hace
`plan.Estado = mensaje.Resultado.Estado`, que para un plan recién creado es
`borrador`) — no hizo falta código nuevo en el consumer.

**Por qué:** con la fila en `borrador` desde el POST había una carrera real. Un
`POST /{id}/optimizar` inmediato pasaba la guarda de
`SolicitarOptimizacionCommand`, que solo rechaza con `409
PLAN_CARGUE_EN_PROCESO` cuando el plan ya está en `procesando`. Los dos
mensajes van a la **misma cola** con `prefetch_count = 2`, así que el worker
podía atender `optimizar` antes que `crear_plan` y fallar con
`PLAN_NO_ENCONTRADO` sobre un plan que estaba por crearse. Con `procesando`, esa
guarda que ya existía bloquea el caso sin agregar código.

Un beneficio adicional: `borrador` es un estado de reposo legítimo (un plan
puede quedarse ahí días mientras el comercial le carga ítems), así que no
distinguía "esperando a Cubicador" de "a medio armar". `procesando` sí, y eso
es lo que hace posible un vigilante de timeout del lado NEXO para los comandos
de Cubo 2 — hoy imposible, porque no había ningún estado que significara
inequívocamente "esperando confirmación".

**Impacto en el frontend (S3-HU-04):** ninguno si la UI trata el 202 como el
resto del contrato de trabajos pesados — de hecho lo alinea con la UX descrita
para Cubo 2 (spinner mientras el estado sea `procesando`, la fila aparece
cuando llega el aviso por SignalR). Sí rompe a quien haya asumido literalmente
el string `"borrador"` en esa respuesta.

**Fuera de alcance:** `guardar_vehiculo` y `eliminar_vehiculo` no escriben
ninguna fila en NEXO, así que siguen sin dejar rastro con el que detectar un
comando perdido. Cubrirlos exige un registro explícito de comandos en vuelo.

### 6.3 Payload de cada comando en la cola

Mismo sobre de siempre (routing key `cubicacion.solicitada`), con
`solicitud` variando por tipo — ver `CabinaFletes.Mensajes.Cubicacion.
MensajesDeCubicacion.cs` para el shape exacto (records `CrearPlanInput`,
`GuardarVehiculoInput`, `GuardarItemInput`, `ImportarItemsInput`,
`EliminarItemInput`, `EliminarPlanInput`, `AjustarColocacionInput`) y
`cubicador/CONTRATO.md` §3.2 para los DTOs Pydantic espejo
(`CrearPlanRequest`, `GuardarVehiculoInput`, `GuardarItemPlanCargueInput`,
`AjusteColocacionInput`) que `worker/main.py` usa para aplicarlos.

```json
// crear_plan
{ "tipo": "crear_plan", "planId": "<guid, generado por NEXO>", "correlationId": "<igual a planId>",
  "solicitud": { "nombre": "Plan Bogotá-Medellín", "vehiculoId": "<guid del catálogo de Cubicador>" } }

// crear_plan_demo — sin datos: Cubicador elige vehículo e ítems internamente
{ "tipo": "crear_plan_demo", "planId": "<correlationId, NO es un plan real>", "correlationId": "...", "solicitud": {} }

// guardar_vehiculo
{ "tipo": "guardar_vehiculo", "planId": "<vehiculoId, generado por NEXO>", "correlationId": "...",
  "solicitud": { "id": "<mismo vehiculoId>", "nombre": "...", "descripcion": null,
    "largoInternoMm": 6200, "anchoInternoMm": 2200, "altoInternoMm": 2000,
    "capacidadKg": 6300, "taraKg": 4200, "puertaAnchoMm": 2200, "puertaAltoMm": 1950,
    "puertaPosicion": "trasera", "ejes": [{ "posicionMm": 1000, "capacidadKg": 3500 }],
    "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0 } }

// guardar_item (planId = el plan al que pertenece)
{ "tipo": "guardar_item", "planId": "<guid del plan>", "correlationId": "...",
  "solicitud": { "id": null, "sku": "SKU-1", "descripcion": "Pallet industrial", "pedido": null,
    "cliente": null, "destino": null, "secuenciaEntrega": 1, "largoMm": 1200, "anchoMm": 1000,
    "altoMm": 1400, "pesoKg": 800, "cantidad": 10, "color": null, "prioridad": 3,
    "tipoEmbalaje": null, "apilable": true, "maxNiveles": 0, "maxPesoEncimaKg": -1,
    "rotable": true, "mantenerVertical": false, "fragil": false, "peligroso": false,
    "refrigerado": false, "grupoCompatibilidad": null, "observaciones": null } }

// importar_items — 1 a 2.000 ítems con el mismo shape que guardar_item
{ "tipo": "importar_items", "planId": "<guid del plan>", "correlationId": "...",
  "solicitud": { "items": [ { /* mismo shape que guardar_item.solicitud */ }, "..." ] } }

// eliminar_item
{ "tipo": "eliminar_item", "planId": "<guid del plan>", "correlationId": "...",
  "solicitud": { "itemId": "<guid del item>" } }

// eliminar_plan — sin datos adicionales, planId ya identifica el plan
{ "tipo": "eliminar_plan", "planId": "<guid del plan>", "correlationId": "...", "solicitud": {} }

// eliminar_vehiculo — el id del vehiculo viaja en planId, igual que en guardar_vehiculo
// (los comandos de catalogo no tienen plan). Puede ser rechazado con VEHICULO_EN_USO.
{ "tipo": "eliminar_vehiculo", "planId": "<guid del vehiculo>", "correlationId": "...", "solicitud": {} }

// ajustar_colocacion — reemplaza el PATCH síncrono de Cubicador
{ "tipo": "ajustar_colocacion", "planId": "<guid del plan>", "correlationId": "...",
  "solicitud": { "colocacionId": "<guid>", "x": null, "y": null, "z": null,
    "orientacionIndice": null, "bloqueada": null, "retirar": true, "reinsertar": null } }
```

### 6.4 Cómo se entera el frontend del desenlace

Reutiliza EXACTAMENTE los dos eventos que ya existen (sección 2) — no se creó
ningún evento nuevo para estos 8 comandos:

- **Éxito** → `plan-de-cargue.generado` → `PlanDeCargueGeneradoConsumer`
  actualiza `PlanCargue.Estado`/`Puntaje` en NEXO y avisa por
  `/hubs/plan-cargue` (grupo = el id que el 202 devolvió).
- **Rechazo** → `cubicacion.rechazada` → `CubicacionRechazadaConsumer` marca
  `Estado = "error"` y avisa por el mismo canal — así es como
  `ajustar_colocacion` "reconcilia" al frontend si el ajuste no era válido
  (posición fuera de límites, colisión, soporte insuficiente...).

**Esto solo funciona de punta a punta cuando NEXO tiene una fila de
`PlanCargue` con ese id** (`crear_plan`, `guardar_item`, `importar_items`,
`eliminar_item`, `ajustar_colocacion`, además de `optimizar`). Ver 6.5 para
los dos comandos donde esto NO aplica — es la limitación más importante de
esta HU y hay que leerla antes de construir la pantalla.

### 6.5 Limitaciones conocidas de ESTOS 8 comandos, honestas

- **`crear_plan_demo` no tiene seguimiento por id todavía.** Cubicador elige
  el vehículo internamente (preset "13.6 m" de SU catálogo, que NEXO no
  conoce) y genera su propio `PlanId` — NEXO no tiene forma de anticiparlo,
  así que el 202 solo trae un `correlationId` de seguimiento, no un
  `PlanCargueId` consultable con `GET /api/planes-cargue/{id}`. El plan
  creado no queda reflejado en NEXO en este alcance (no hay fila espejo que
  el consumer pueda actualizar). Pensado para demos/pruebas — si el frontend
  necesita seguirlo, es trabajo de una iteración futura de lectura.
- **`guardar_vehiculo` no tiene aviso de éxito ni de fallo.** NEXO no
  mantiene espejo de vehículos (la fuente de verdad es Cubicador — hoy nada
  en NEXO lee su propia tabla `VehiculosCubicacion` salvo el stub interno que
  `CrearPlanCommand` asegura para no romper su FK). Si Cubicador rechaza el
  vehículo (dimensiones inválidas, etc.), `CubicacionRechazadaConsumer` no
  encuentra ninguna fila que marcar y el rechazo se pierde en silencio del
  lado de NEXO — el 202 es una promesa de "quedó en cola", no de "quedó
  guardado". Mitigación posible a futuro: validar en NEXO lo mismo que ya
  valida `CubicacionSolicitadaValidador` (dimensiones, puerta, ejes) para que
  la mayoría de rechazos ni siquiera lleguen a encolarse.
- **`eliminar_plan` borra en NEXO de forma OPTIMISTA.** La fila espejo se
  borra en el mismo request, antes de publicar — no se repone si Cubicador
  rechaza el borrado (caso raro). Se prefirió esta asimetría sobre la
  complejidad de reconciliar un borrado deshecho.
- **`crear_plan` deja un stub de vehículo en la tabla `VehiculosCubicacion`
  de NEXO** (solo `Id` + `Nombre` = el mismo id, sin dimensiones) — existe
  ÚNICAMENTE para satisfacer la FK de `PlanCargue.VehiculoId` en la base de
  NEXO. Nadie más lee esa fila; no es una fuente de verdad de vehículos.
