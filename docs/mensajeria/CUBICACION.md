# Puente de mensajería con Cubicador (issue #28, S2-HU-075)

Documento de cierre del criterio "contrato de mensajes documentado". Cubicador
(`cubicador/`, microservicio Python, mismo repo) ya documenta este mismo
contrato desde su lado en `cubicador/docs/CONTRATOS.md` sección B — este
archivo es la versión NEXO/C# del mismo contrato, más las decisiones que solo
aplican de este lado.

## 0. Cumplimiento contra el texto original de la HU-075 (honesto, línea por línea)

> Objetivo: el servicio Python consume los trabajos de cubicación desde
> RabbitMQ y publica resultados/rechazos por cola de respuesta — NADA
> síncrono entre backend y motor. Reintentos y DLQ según la topología de
> S2-HU-04.1. Conecta con la saga de HU-04 y alimenta el flujo de entrega de
> HU-08.

| Frase del objetivo | Estado | Salvedad |
|---|---|---|
| "el servicio Python consume los trabajos... y publica resultados/rechazos por cola" | ✅ Cumple | — |
| "NADA síncrono entre backend y motor" | ⚠️ Cumple en el código del puente (`ICubicacionPort`/`CubicacionRabbitAdapter`/`CubicacionResultadosConsumidor` no hacen ninguna llamada HTTP) | Ver la precondición de la sección 2: hoy nada CREA el plan+ítems del lado de Cubicador antes de solicitar la cubicación salvo llamando a su API REST — esa pieza, cuando exista un disparador real, sí necesita resolverse (¿otro evento asíncrono con el payload completo? ¿el mismo puente `crear_desde_linea`?), y hoy no está resuelta |
| "Reintentos y DLQ según la topología de S2-HU-04.1" | ⚠️ Hay reintentos + DLQ reales, verificados con infraestructura real | NO son la topología de MassTransit (`UseMessageRetry`/`Fault<T>`, 1s/5s/15s/60s) — ver sección 1 para el porqué completo y sección 3 para el patrón que sí se implementó (mismo que ya usa Cubicador: contador `x-intentos`, 3 intentos, cola de error, alerta) |
| "Conecta con la saga de HU-04 y alimenta el flujo de entrega de HU-08" | ❌ No implementado — decisión explícita | `AutoCosteoSaga` es puramente de costeo por diseño (su propio docstring lo dice) y HU-08 no tiene ningún artefacto en el repo todavía. Acoplar cubicación a esa saga habría forzado dos conceptos de negocio sin destino real esperando el resultado — ver sección 4 |

**Los 3 criterios de cierre sí cumplen**, verificados con infraestructura real (ver secciones 2 y 3 de este documento, y `tests/QA/28-s2-hu-075-.../README.md`):

- Consumo de trabajos y publicación de resultados por cola, contrato documentado — ✅
- Reintentos + DLQ + alerta ante fallos repetidos del motor — ✅ (con la salvedad de arriba: patrón manual, no MassTransit nativo)
- Prueba de extremo a extremo: trabajo publicado → resultado recibido por cola — ✅

**Puerta abierta**: si el equipo decide que esto debe ser MassTransit nativo
de verdad (no la aproximación con RabbitMQ.Client directo elegida aquí), el
usuario ya está al tanto de qué cambios concretos implicaría del lado de
Cubicador (envolver los mensajes con el `messageType`/`message` de
MassTransit en ambas direcciones, actualizar sus pruebas y su contrato
documentado) y del lado de NEXO (reemplazar `CubicacionRabbitAdapter`/
`CubicacionResultadosConsumidor` por `IConsumer<T>` reales + registro en
`MensajeriaDependencyInjection.cs` + `Bind()` manual al exchange
`cubicador.eventos`), y asumiría hacer esos cambios él mismo si se decide
seguir por ese camino.

## 1. Por qué esto no es un `IConsumer<T>` de MassTransit

Cubicador publica JSON plano al exchange `cubicador.eventos` (topic) vía
`aio_pika`, sin el envelope de MassTransit (sin `messageType`, sin campo
`message` anidado — ver `cubicador/infrastructure/mensajeria/rabbitmq.py`).
Un `IConsumer<T>`/`IPublishEndpoint` normal de NEXO no puede hablar ese
formato sin reescribir Cubicador para que hable MassTransit — y Cubicador ya
está construido, probado (70 pruebas propias) y verificado contra
infraestructura real con SU contrato tal como está.

Por eso el puente (`src/CabinaFletes.Infrastructure/Cubicacion/`) usa
**RabbitMQ.Client directo**, no `IBus`/`IPublishEndpoint`:

- `CubicacionRabbitAdapter` (implementa `ICubicacionPort`) — publica
  `CubicacionSolicitada` al exchange `cubicador.eventos`, routing key
  `cubicacion.solicitada`, con el JSON exacto que espera
  `worker/main.py::_ejecutar_solicitud`.
- `CubicacionResultadosConsumidor` (`BackgroundService`) — consume
  `plan-de-cargue.generado`/`cubicacion.rechazada` del mismo exchange y
  actualiza el `PlanCargue` correspondiente.

Mismo broker físico que el resto de NEXO (`ConnectionStrings:RabbitMq`), dos
topologías independientes que no se cruzan — igual que Cubicador y su propio
broker compartido con NEXO ya conviven sin colisión de nombres (ver
`cubicador/docs/DECISIONES_TECNICAS.md` #9).

## 2. Contrato de mensajes

### Comando saliente: `CubicacionSolicitada` (NEXO → Cubicador)

Routing key `cubicacion.solicitada`.

```json
{
  "tipo": "optimizar",
  "planId": "<uuid>",
  "correlationId": "<uuid, igual a planId>",
  "solicitud": {
    "estrategia": "Combinada",
    "algoritmo": "bfd",
    "semilla": null,
    "ponderaciones": null,
    "respetarBloqueadas": false
  }
}
```

**Precondición importante, no obvia**: `planId` debe ser el Id de un plan que
YA EXISTE en la base de datos PROPIA de Cubicador (con sus ítems), no solo en
la de NEXO — Cubicador optimiza un plan que él mismo persiste, no uno que
solo NEXO conoce. Hoy nada crea ese plan del lado de Cubicador a partir de
esta solicitud: quien dispare `ICubicacionPort.SolicitarCubicacionAsync`
primero necesita que el plan (con sus ítems) ya exista en Cubicador — vía su
propia API REST, o vía el puente `crear_desde_linea` (que a su vez depende de
un endpoint de NEXO que tampoco existe todavía en esta rama, ver
`cubicador/docs/CONTRATOS.md` sección D). Este acoplamiento de datos entre
las dos bases es la pieza que falta resolver cuando HU-04/HU-08 conecten esto
de verdad — ver sección 4.

### Eventos entrantes: Cubicador → NEXO

Ambos en el mismo exchange, consumidos por `CubicacionResultadosConsumidor`
en la cola `nexo.cubicacion.resultados`.

**`plan-de-cargue.generado`** (routing key `plan-de-cargue.generado`):

```json
{ "correlationId": "<uuid>", "planId": "<uuid>", "resultado": { "estado": "optimizado", "puntaje": 75.1, "colocaciones": 2 } }
```

→ actualiza `PlanCargue.Estado`/`Puntaje` con esos valores.

**`cubicacion.rechazada`** (routing key `cubicacion.rechazada`):

```json
{ "correlationId": "<uuid>", "planId": "<uuid>", "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado." }
```

→ marca `PlanCargue.Estado = "error"`.

Ambos shapes están protegidos por `tests/CabinaFletes.Api.Tests/Cubicacion/
ContratoCubicacionTests.cs` (sin broker, solo (de)serialización) — copiados
literal de lo que publica `RabbitMqEventosPublisher` del lado de Cubicador,
para que un cambio de nombre de campo en cualquiera de los dos lados lo
detecte un test, no un incidente en producción.

## 3. Reintentos + DLQ + alerta

`CubicacionResultadosConsumidor` NO tiene `UseMessageRetry`/`Fault<T>`
automático — eso es exclusivo de los `IConsumer<T>` reales de MassTransit
(ver `docs/mensajeria/TOPOLOGIA.md`). Al no poder usar esa tubería aquí (ver
sección 1), se replicó el MISMO patrón que ya usa Cubicador del otro lado del
puente en vez de inventar uno nuevo:

- Contador `x-intentos` en la cabecera del mensaje, 3 intentos.
- Al agotarlos, el mensaje se republica en `nexo.cubicacion.resultados_error`
  (cola dedicada, para inspección manual).
- Alerta por correo con `ICorreoPort` (mismo mecanismo que
  `AlertaDeFalloConsumer<T>` usa para el resto de NEXO, #13) a
  `Alertas:EquipoTecnico`.

**Verificado contra infraestructura real (2026-08-27)**: se publicó un
mensaje JSON malformado directo a `plan-de-cargue.generado`. Log real:

```
fail: Fallo procesando mensaje de plan-de-cargue.generado (intento 1/3) ...
fail: Fallo procesando mensaje de plan-de-cargue.generado (intento 2/3) ...
fail: Fallo procesando mensaje de plan-de-cargue.generado (intento 3/3) ...
fail: Mensaje de plan-de-cargue.generado movido a nexo.cubicacion.resultados_error tras 3 intentos: ...
info: SmtpCorreoAdapter Correo enviado a scalderon@boterosoto.com.co: [NEXO] Resultado de cubicación (plan-de-cargue.generado) cayó a la cola de error
(... 5 correos más, uno por destinatario)
```

Confirmado en el capturador SMTP de pruebas: `rowCount: 6`. Detalle completo
en `tests/QA/28-s2-hu-075-.../README.md`.

## 4. Limitaciones conocidas, honestas (no resueltas por esta HU)

- **Sin transacción/Outbox**: `SolicitarCubicacionAsync` publica directo al
  broker, no vía el Outbox transaccional de NEXO (`IPublicadorDeMensajes`
  publica con esa garantía; este puerto no). Si el proceso muere entre
  guardar el `PlanCargue` y publicar, el trabajo queda huérfano. Aceptable
  hoy porque nada dispara esto en producción todavía.
- **Ningún trigger real todavía**: no hay endpoint HTTP ni consumer que
  llame a `ICubicacionPort` — se verificó con un harness de prueba (ver QA),
  no desde un flujo de usuario real. "Conecta con la saga de HU-04" y
  "alimenta el flujo de entrega de HU-08" (texto original de la HU-075) son
  aspiracionales: `AutoCosteoSaga` es puramente de costeo (ver su propio
  docstring) y HU-08 no tiene ningún artefacto en el repo todavía — no se
  modificó la Saga a propósito, ver la discusión de diseño de esta HU.
  Cuando exista un disparador real, solo necesita llamar a
  `ICubicacionPort.SolicitarCubicacionAsync` — el puente ya está listo.
- **Datos duplicados entre dos bases**: ver la precondición de la sección 2
  — es la pieza de diseño más grande que falta resolver antes de que esto
  sea un flujo de negocio real, no solo una prueba de mensajería.
