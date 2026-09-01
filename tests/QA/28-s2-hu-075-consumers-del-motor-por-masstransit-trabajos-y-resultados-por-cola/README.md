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
- `docs/mensajeria/CUBICACION.md` — contrato + decisiones + compatibilidad
  con el envelope de MassTransit del lado de Cubicador (implementada y
  verificada, ver sección 4 de este documento).
- Del lado de Cubicador (`cubicador/`, mismo repo): `worker/main.py`,
  `infrastructure/mensajeria/rabbitmq.py`, `scripts/ci_docker_smoke.py`,
  `docs/CONTRATOS.md`, `docs/DECISIONES_TECNICAS.md` #12 — compatibilidad
  con el envelope de MassTransit, ver sección 4.

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

**Efecto colateral encontrado y YA RESUELTO**: publicar `CubicacionSolicitada`
con el envelope real de MassTransit hacia Cubicador hizo que su worker
fallara con `KeyError: 'tipo'` en la primera corrida — Cubicador leía JSON
plano, no el envelope. Se implementó compatibilidad del lado de Cubicador
(desenvuelto al consumir con fallback, envelope al publicar los 2 eventos
que sí lee NEXO — ver `docs/mensajeria/CUBICACION.md` #4 y
`cubicador/docs/DECISIONES_TECNICAS.md` #12) y se re-verificó de punta a
punta: **`estado=optimizado, puntaje=75.00`**, el mismo resultado de la
sección 3, ahora sin ningún workaround manual.

**Aparte, un hallazgo de metodología de prueba, no de producto**: al repetir
la verificación con el contenedor `nexo-api-1` real corriendo A LA VEZ que el
harness de xUnit, el mensaje de resultado se enrutó al consumer del
contenedor real (mismo nombre de cola, RabbitMQ reparte round-robin entre
ambos) en vez de al del harness — el harness, apuntando a la base de datos
de PRUEBAS, vio el plan "atascado" en `procesando` y el test dio timeout,
aunque el mensaje SÍ se procesó correctamente (confirmado en los logs del
contenedor real). Se resolvió deteniendo `nexo-api-1` mientras corre el
harness — no es un bug del puente, es evitar dos consumidores compitiendo
por la misma cola durante la verificación.

## 5. Regresión

`dotnet test CabinaFletes.Clean.sln` contra infraestructura real: **193/193**
(Domain 61, Application 75, Api 57 — 54 + 3 nuevas de
`ContratoCubicacionTests`). Sin regresiones.

## 6. Qué queda pendiente, honesto

- Nadie dispara `ICubicacionPort` todavía desde un flujo de usuario real —
  ver `docs/mensajeria/CUBICACION.md` #5 (datos duplicados entre las dos
  bases, disciplina del Outbox).
- Solo se probó el camino "optimizar" (no "crear_desde_linea").
- El harness de verificación no quedó como prueba automatizada en CI (igual
  criterio que HU-041) — la garantía de contrato en cada build la dan las 3
  pruebas de `ContratoCubicacionTests` (rápidas, sin infraestructura) del
  lado NEXO, y las 70 de Cubicador (incluida la compatibilidad de envelope,
  cubierta indirectamente por `ci_docker_smoke.py`) del otro lado.

## 7. Verificación QA independiente (Kevin Cano, situado en esta rama antes de aprobar el PR #73)

Reproducido de cero, no solo leído — con los dos stacks reales corriendo a la vez
(NEXO: `db`+`rabbit`+`api`; Cubicador: `db`+`api`+`worker`, conectado al mismo
`rabbit`) y el publicador MassTransit de esta misma carpeta
([`Program.cs`](Program.cs)/[`Publisher.csproj`](Publisher.csproj)), que respeta la
misma topología manual que registra `MensajeriaDependencyInjection.cs` — sin eso el
mensaje "se publica" pero no le llega a nadie (topic exchange sin bind que matchee,
sin error, el mismo hallazgo #2 del autor).

**Cómo reproducir** (con ambos stacks arriba, `rabbit` compartido por
`nexo_internal`):
```bash
# 1) Feliz: crear plan+ítem en Cubicador (POST /api/planes-cargue, /{id}/items) y su
#    espejo en NEXO (INSERT directo en planes_cargue/vehiculos_cubicacion — hoy no
#    existe ningún endpoint que lo haga por REST, ver sección 6).
docker run --rm --network nexo_internal \
  -e RABBIT_HOST=rabbit -e RABBIT_USER=<user> -e RABBIT_PASS=<pass> \
  -v "$PWD:/src" -w /src/tests/QA/28-s2-hu-075-consumers-del-motor-por-masstransit-trabajos-y-resultados-por-cola \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet run -- solicitar <planId>

# 2) Reintentos+DLQ+alerta: mismo publicador, modo "fault", sobre un plan que YA
#    exista en NEXO (con uno inexistente no pasa nada — el propio consumer hace
#    `if (plan is null) return`, a propósito).
docker run --rm --network nexo_internal \
  -e RABBIT_HOST=rabbit -e RABBIT_USER=<user> -e RABBIT_PASS=<pass> \
  -v "$PWD:/src" -w /src/tests/QA/28-s2-hu-075-consumers-del-motor-por-masstransit-trabajos-y-resultados-por-cola \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet run -- fault <planId-existente>

# 3) Carga: "solicitar" admite varios planId a la vez, publicados concurrentemente.
```

**Regresión:** build 0 errores, `dotnet test` completo **Domain 103/103, Application
113/113, Api.Tests 92/92** (+2 saltadas por diseño) — coincide con las 3 nuevas de
`ContratoCubicacionTests` sumadas al total ya acumulado de sesiones anteriores. Cero
regresiones.

**Flujo feliz, reproducido con datos propios (no los del autor):** creé un plan +
ítem en Cubicador por su API REST, su espejo en NEXO por SQL directo, y publiqué
`CubicacionSolicitada` con la topología correcta. Resultado: **`estado=optimizado,
puntaje=75.10`** — un resultado real del motor (distinto al `75.00` del autor porque
usé datos de prueba distintos), confirmando que es una optimización real y no una
respuesta fija.

**Reintentos + DLQ + alerta, reproducido con un fallo propio:** publiqué un
`PlanDeCargueGenerado` con `Resultado=null` sobre un plan que sí existe en NEXO (mi
primer intento, con un plan inexistente, no disparó nada — confirma además el
`if (plan is null) return` documentado por el autor). Resultado exacto al de la
sección 4: `R-RETRY` → `R-FAULT` a los **85 s** (coincide con 1s+5s+15s+60s), **1
correo con los 6 destinatarios** (consolidado, gracias al ajuste que le hice a
`AlertaDeFalloConsumer<T>` en el PR #71), **1 mensaje en `Cubicacion.
PlanDeCargueGenerado_error`**.

**Carga y estrés — cobertura nueva, no incluida por el autor:** 5 solicitudes de
cubicación concurrentes (planes distintos, mismo lote de publicación) → **las 5
terminaron `optimizado`**, sin pérdidas ni cruces entre planes, colas en 0 mensajes
después, ambos `/health` sanos.

**Conclusión:** los 3 hallazgos reales del autor (disciplina del Outbox, routing key
explícito, colisión de nombres de endpoint) están todos vigentes en el código
mergeado y el puente funciona de punta a punta, verificado con datos y ejecuciones
propias. Sin objeciones para mergear.
