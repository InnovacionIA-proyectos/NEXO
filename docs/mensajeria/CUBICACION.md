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
`crear_desde_linea` (que a su vez depende de un endpoint de NEXO que tampoco
existe todavía en esta rama, ver `cubicador/CONTRATO.md` sección 7).

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
