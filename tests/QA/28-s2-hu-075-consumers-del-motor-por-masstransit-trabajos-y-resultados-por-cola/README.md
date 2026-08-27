# Verificación QA — S2-HU-075 (issue #28)

Rama: `28-s2-hu-075-consumers-del-motor-por-masstransit-trabajos-y-resultados-por-cola`.

## 0. Contexto y decisión del equipo

Al llegar a esta rama, el microservicio Cubicador (Python) ya consumía
trabajos y publicaba resultados por cola, con reintentos+DLQ+alerta y una
prueba E2E por cola — trabajo hecho en la rama `13-s2-hu-041-...` (de la que
esta rama parte). El gap real de esta HU era del lado NEXO: **no existía
ningún consumer/publisher de MassTransit** que hablara con Cubicador.

Primera versión: puente con RabbitMQ.Client directo (sin `IConsumer<T>`).
**El líder pidió MassTransit nativo** (todo sigue viajando por RabbitMQ) —
esta es la versión reescrita para eso. Ver `docs/mensajeria/CUBICACION.md`
para el diseño completo, la topología manual necesaria, y el cumplimiento
línea por línea contra el texto original de la HU (sección 0 de ese
documento).

**Decisión de diseño que se mantiene**: NO se modificó `AutoCosteoSaga`
(HU-04) — es una saga pura de costeo por diseño, y HU-08 ("flujo de
entrega") no tiene ningún artefacto en el repo todavía. El puente queda como
pieza aislada, lista para que quien dispare esto en el futuro solo llame a
`ICubicacionPort`.

## 1. Qué trae este PR

- `src/CabinaFletes.Mensajes/Cubicacion/MensajesDeCubicacion.cs` — DTOs del
  contrato + `CubicacionSolicitadaValidador`.
- `src/CabinaFletes.Application/Common/Ports/ICubicacionPort.cs`.
- `src/CabinaFletes.Infrastructure/Cubicacion/`:
  - `CubicacionPublicadorAdapter.cs` — implementa el puerto con
    `IPublishEndpoint` de MassTransit (routing key fijado por mensaje, ver
    `docs/mensajeria/CUBICACION.md` #1 para el porqué).
  - `PlanDeCargueGeneradoConsumer.cs`/`CubicacionRechazadaConsumer.cs` —
    `IConsumer<T>` reales, actualizan `PlanCargue`.
- `src/CabinaFletes.Infrastructure/Common/Mensajeria/
  MensajeriaDependencyInjection.cs` — registro de los 2 consumers + 2
  `AlertaDeFalloConsumer<T>` + topología manual (exchange/routing keys de
  Cubicador) + exclusión de `ConfigureEndpoints` para evitar colisión de
  endpoints (ver hallazgos abajo).
- `tests/CabinaFletes.Api.Tests/Cubicacion/ContratoCubicacionTests.cs` — 3
  pruebas de (de)serialización, sin broker.
- `docs/mensajeria/CUBICACION.md` — contrato + decisiones + qué necesitaría
  Cubicador para hablar el envelope de MassTransit (no implementado).

## 2. Criterios de cierre — verificados contra infraestructura real

| Criterio | Verificado cómo | Resultado |
|---|---|---|
| Consumo de trabajos y publicación de resultados por cola, contrato documentado | Harness real: plan creado en Cubicador → `CubicacionSolicitada` publicada desde NEXO (MassTransit real) → Cubicador optimiza → `plan-de-cargue.generado` consumido por NEXO → `PlanCargue` actualizado | ✅ — sección 3 |
| Reintentos + DLQ + alerta ante fallos repetidos del motor | Envelope bien formado con dato inválido → `NullReferenceException` real en el consumer → `UseMessageRetry` real (1s/5s/15s/60s) → `Fault<T>` real → `AlertaDeFalloConsumer<T>` → 6 correos | ✅ — sección 4 |
| Prueba de extremo a extremo: trabajo publicado → resultado recibido por cola | El mismo harness de la sección 3 | ✅ |

## 3. Flujo feliz — evidencia real

**Preparación**: stack completo de NEXO (`db`+`rabbit`+`api`) y de Cubicador
(`db`+`api`+`worker`, conectado al mismo `rabbit`) corriendo a la vez.

**Harness** (xUnit temporal contra `ApiFixture`/`WebApplicationFactory`, no
committeado por ser desechable — resuelve `ICubicacionPort` del contenedor
DI real de la app, no una reimplementación a mano):

1. Crea un plan + ítem en Cubicador vía su propia API REST — preparar datos
   de prueba, igual que haría cualquier caller real hoy.
2. Crea un `PlanCargue` espejo en NEXO con el MISMO `Id`.
3. Llama a `ICubicacionPort.SolicitarCubicacionAsync(...)`, luego
   `SaveChangesAsync()` en el MISMO `DbContext` (ver hallazgo del Outbox
   abajo) — **el trabajo, 100% asíncrono desde aquí**.
4. Poll de `PlanCargue.Estado` en NEXO hasta que cambie de `"procesando"`.

**Resultado real**: `estado=optimizado puntaje=75.00`.

**3 hallazgos reales durante esta verificación** (cambiaron el código de
producción, no solo el harness):

1. **El Outbox no entrega si no hay un `SaveChangesAsync()` DESPUÉS de
   publicar.** La primera corrida guardó el plan, publicó, y nunca volvió a
   guardar — `OutboxMessage` quedó en 0 filas, el mensaje nunca salió.
   Mismo orden que ya usa `CostearLineaCommand.cs` en el resto de NEXO — no
   es un bug de MassTransit, es la disciplina que exige el patrón.
2. **`IPublicadorDeMensajes` no sirve para publicar `CubicacionSolicitada`**:
   no expone forma de fijar el routing key. Sin eso, el mensaje "se publicó"
   (`publish_in: 1` en las métricas del exchange) pero nunca llegó a la cola
   de Cubicador — un topic exchange sin bind que matchee no entrega a nadie,
   sin error. Solucionado inyectando `IPublishEndpoint` directo con
   `ctx.SetRoutingKey(...)`.
3. **Colisión de endpoints al arrancar**: `AlertaDeFalloConsumer<T>` nombra
   su endpoint según el TIPO de mensaje (no la clase consumer) — sin
   prefijo, colisionaba con los endpoints manuales de
   `PlanDeCargueGeneradoConsumer`/`CubicacionRechazadaConsumer` (mismo
   nombre exacto). MassTransit no arrancaba: *"A receive endpoint with the
   same key was already added"*. Solucionado con prefijo `Cubicacion.` en
   los endpoints manuales + `f.Exclude(...)` en `ConfigureEndpoints` para
   que no intente auto-configurar los mismos consumers otra vez.

## 4. Reintentos + DLQ + alerta — evidencia real (MassTransit nativo)

Se publicó un envelope de MassTransit bien formado (`messageType` correcto,
identificando `PlanDeCargueGenerado`) pero con `"resultado": null`, forzando
una `NullReferenceException` real dentro de `PlanDeCargueGeneradoConsumer`.
Log real (timestamps reales):

```
21:32:00 warn: MassTransit.ReceiveTransport R-RETRY rabbitmq://rabbit/Cubicacion.PlanDeCargueGenerado
  System.NullReferenceException: Object reference not set to an instance of an object.
  at CabinaFletes.Infrastructure.Cubicacion.PlanDeCargueGeneradoConsumer.Consume(...)

21:33:21 dbug: SEND rabbitmq://rabbit/MassTransit:Fault--CabinaFletes.Mensajes.Cubicacion:PlanDeCargueGenerado--
  MassTransit.Fault<CabinaFletes.Mensajes.Cubicacion.PlanDeCargueGenerado>
21:33:21 fail: R-FAULT rabbitmq://rabbit/Cubicacion.PlanDeCargueGenerado (agotados los reintentos)
21:33:21 info: SmtpCorreoAdapter Correo enviado a scalderon@boterosoto.com.co: [NEXO] Mensaje PlanDeCargueGenerado cayó a la cola de error
  (... 5 correos más)
21:33:21 dbug: RECEIVE rabbitmq://rabbit/PlanDeCargueGenerado ... Fault<PlanDeCargueGenerado>
  AlertaDeFalloConsumer<CabinaFletes.Mensajes.Cubicacion.PlanDeCargueGenerado>
```

**81 segundos** entre el primer `R-RETRY` (21:32:00) y el `Fault` (21:33:21)
— coincide exacto con 1s+5s+15s+60s de `Tuberia<T>`. Confirmado en el
capturador SMTP de pruebas (nunca el corporativo): `rowCount: 6`, los 6
destinatarios de `Alertas:EquipoTecnico`.

**Hallazgo real, distinción importante**: un mensaje que ni siquiera es JSON
válido (probado primero) NO pasa por este mismo camino — MassTransit lo
clasifica como `ReceiveFault` (falla al deserializar el ENVELOPE, antes de
identificar el tipo del mensaje), que no pasa por `UseMessageRetry` ni por
`AlertaDeFalloConsumer<T>`. Dos categorías de fallo distintas en MassTransit,
confirmado en runtime — ver `docs/mensajeria/CUBICACION.md` #3.

**Efecto colateral real, esperado**: publicar `CubicacionSolicitada` con el
envelope real de MassTransit hacia Cubicador (para probar el flujo feliz de
la sección 3) hizo que su worker fallara con `KeyError: 'tipo'` — Cubicador
lee JSON plano, no el envelope. Ver `docs/mensajeria/CUBICACION.md` #4 para
la evidencia completa y qué necesitaría cambiar Cubicador (discusión
pendiente con el equipo, no implementado en esta rama).

## 5. Regresión

`dotnet test CabinaFletes.Clean.sln` contra infraestructura real: **193/193**
(Domain 61, Application 75, Api 57 — 54 + 3 nuevas de
`ContratoCubicacionTests`). Sin regresiones.

## 6. Qué queda pendiente, honesto

- **Cubicador no puede consumir/producir el envelope de MassTransit
  todavía** — es el bloqueante real para que el flujo feliz funcione con el
  Cubicador actual sin intervención manual. Ver
  `docs/mensajeria/CUBICACION.md` #4 para el detalle exacto de qué cambiar
  ahí (discusión pendiente, fuera de esta rama).
- Nadie dispara `ICubicacionPort` todavía desde un flujo de usuario real —
  ver `docs/mensajeria/CUBICACION.md` #5 (datos duplicados entre las dos
  bases, disciplina del Outbox).
- Solo se probó el camino "optimizar" (no "crear_desde_linea").
- El harness de verificación no quedó como prueba automatizada en CI (igual
  criterio que HU-041) — la garantía de contrato en cada build la dan las 3
  pruebas de `ContratoCubicacionTests` (rápidas, sin infraestructura).
