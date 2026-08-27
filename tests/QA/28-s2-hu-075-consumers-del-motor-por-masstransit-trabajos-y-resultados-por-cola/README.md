# Verificación QA — S2-HU-075 (issue #28)

Rama: `28-s2-hu-075-consumers-del-motor-por-masstransit-trabajos-y-resultados-por-cola`.

## 0. Contexto: el lado Python ya estaba hecho, esta HU cierra el lado NEXO

Al llegar a esta rama, el microservicio Cubicador (Python) ya consumía
trabajos y publicaba resultados por cola, con reintentos+DLQ+alerta y una
prueba E2E por cola — trabajo hecho en la rama `13-s2-hu-041-...` (de la que
esta rama parte). Investigar el estado antes de empezar encontró que el gap
real de esta HU no era del lado Python: **no existía ningún consumer/publisher
de MassTransit en NEXO** que hablara con Cubicador. Ver
`docs/mensajeria/CUBICACION.md` para el porqué de la arquitectura elegida
(RabbitMQ.Client directo, no `IConsumer<T>`) y sus limitaciones honestas.

**Decisión de diseño explícita**: NO se modificó `AutoCosteoSaga` (HU-04). Es
una saga pura de costeo por diseño (su propio docstring lo dice), y HU-08
("flujo de entrega") no tiene ningún artefacto en el repo todavía — acoplar
cubicación a esa saga habría sido forzar dos conceptos de negocio distintos
sin ningún destino real esperando el resultado. Se construyó el puente como
pieza aislada, lista para que quien dispare esto en el futuro solo llame a
`ICubicacionPort`.

**Cumplimiento línea por línea contra el texto original de la HU** (con sus
salvedades: "NADA síncrono" cumple en el puente pero no resuelve cómo se
crea el plan en Cubicador; "reintentos y DLQ según S2-HU-04.1" cumple
funcionalmente pero con un patrón manual, no MassTransit nativo; "conecta con
la saga de HU-04/HU-08" no se implementó, por decisión explícita) — ver
`docs/mensajeria/CUBICACION.md` sección 0, incluida la puerta abierta: si se
decide que esto debe ser MassTransit nativo de verdad, el usuario asumiría
los cambios necesarios (documentados ahí, en ambos lados del puente).

## 1. Qué trae este PR

- `src/CabinaFletes.Mensajes/Cubicacion/MensajesDeCubicacion.cs` — DTOs del
  contrato (no son mensajes de MassTransit, ver el porqué en el archivo).
- `src/CabinaFletes.Application/Common/Ports/ICubicacionPort.cs` — puerto
  para solicitar una cubicación.
- `src/CabinaFletes.Infrastructure/Cubicacion/`:
  - `CubicacionRabbitAdapter.cs` — implementa el puerto, publica al exchange
    `cubicador.eventos` de Cubicador con RabbitMQ.Client directo.
  - `CubicacionResultadosConsumidor.cs` — `BackgroundService` que consume
    los resultados y actualiza `PlanCargue` (reactivando una tabla que
    existía en el `DbContext` sin ningún código que la usara — ver
    `docs/DECISIONES_TECNICAS.md` #11 de Cubicador).
  - `CubicacionDependencyInjection.cs` — registro (`AddCubicacion()`).
- `tests/CabinaFletes.Api.Tests/Cubicacion/ContratoCubicacionTests.cs` — 3
  pruebas de (de)serialización contra el JSON real que produce/espera
  Cubicador, sin broker.
- `docs/mensajeria/CUBICACION.md` — el contrato completo + decisiones.

## 2. Criterios de cierre — verificados contra infraestructura real, no solo leídos

| Criterio | Verificado cómo | Resultado |
|---|---|---|
| Consumo de trabajos y publicación de resultados por cola, contrato documentado | Harness real: plan creado en Cubicador → `CubicacionSolicitada` publicada desde NEXO → Cubicador optimiza → `plan-de-cargue.generado` consumido por NEXO → `PlanCargue` actualizado | ✅ — ver sección 3 |
| Reintentos + DLQ + alerta ante fallos repetidos del motor | Mensaje malformado forzado → 3 reintentos reales → cola de error → 6 correos confirmados | ✅ — ver sección 4 |
| Prueba de extremo a extremo: trabajo publicado → resultado recibido por cola | El mismo harness de la sección 3 ES esta prueba | ✅ |

## 3. Flujo feliz — evidencia real

**Preparación**: stack completo de NEXO (`db`+`rabbit`+`api`, con
`AddCubicacion()` ya registrado) y de Cubicador (`db`+`api`+`worker`,
conectado al mismo `rabbit` — ver `13-s2-hu-041-...`) corriendo a la vez.

**Harness** (referencia real a `CabinaFletes.Infrastructure`, no una
reimplementación a mano — código en el historial de la sesión, no
committeado por ser desechable):

1. Crea un plan + ítem en Cubicador vía su propia API REST (`POST
   /api/planes-cargue`, `POST /items`) — esto NO es "el trabajo", es
   preparar datos de prueba, igual que haría cualquier caller real hoy.
2. Crea un `PlanCargue` espejo en NEXO con el MISMO `Id`.
3. Llama a `ICubicacionPort.SolicitarCubicacionAsync(planId, "Combinada", "bfd", null, false, ct)` — **esto sí es el trabajo, y es 100% asíncrono desde aquí**.
4. Poll de `PlanCargue.Estado` en NEXO hasta que cambie de `"procesando"`.

**Resultado real**:

```
Plan creado en Cubicador: f7186240-347c-41b8-8c26-e82760e4bd9c
Item agregado en Cubicador.
PlanCargue espejo creado en NEXO.
CubicacionSolicitada publicada.
RESULTADO: estado=optimizado puntaje=75.00
```

**Hallazgo real durante la primera corrida** (documentado porque cambió el
diseño del harness, no del código de producción): la primera versión del
harness solo creaba el `PlanCargue` en NEXO, sin el plan/ítems equivalentes
en Cubicador. Resultado: `estado=error` — Cubicador rechazó la solicitud
porque el `planId` no existía en SU base. Esto no es un bug del puente: es la
precondición real documentada en `docs/mensajeria/CUBICACION.md` sección 2
("el `planId` debe existir ya en Cubicador, no solo en NEXO") — confirma que
el consumer de NEXO también maneja `cubicacion.rechazada` correctamente (lo
persistió como `error`, sin caerse).

## 4. Reintentos + DLQ + alerta — evidencia real

Con el mismo stack arriba, se publicó un mensaje JSON malformado directo a
`cubicador.eventos`/`plan-de-cargue.generado` (simulando un fallo real de
deserialización). Log real de `api`:

```
fail: CubicacionResultadosConsumidor Fallo procesando mensaje de plan-de-cargue.generado (intento 1/3) System.Text.Json.JsonException: ...
fail: CubicacionResultadosConsumidor Fallo procesando mensaje de plan-de-cargue.generado (intento 2/3) System.Text.Json.JsonException: ...
fail: CubicacionResultadosConsumidor Fallo procesando mensaje de plan-de-cargue.generado (intento 3/3) System.Text.Json.JsonException: ...
fail: CubicacionResultadosConsumidor Mensaje de plan-de-cargue.generado movido a nexo.cubicacion.resultados_error tras 3 intentos: ...
info: SmtpCorreoAdapter Correo enviado a scalderon@boterosoto.com.co: [NEXO] Resultado de cubicación (plan-de-cargue.generado) cayó a la cola de error
info: SmtpCorreoAdapter Correo enviado a dguerra@boterosoto.com.co: ...
info: SmtpCorreoAdapter Correo enviado a jpvelez@boterosoto.com.co: ...
info: SmtpCorreoAdapter Correo enviado a kcano@boterosoto.com.co: ...
info: SmtpCorreoAdapter Correo enviado a ivasco@boterosoto.com.co: ...
info: SmtpCorreoAdapter Correo enviado a bquintero@boterosoto.com.co: ...
```

Confirmado por 2 fuentes independientes:
1. **RabbitMQ real**: `nexo.cubicacion.resultados_error: messages=1`.
2. **smtp4dev real** (capturador de pruebas, nunca el SMTP corporativo):
   `rowCount: 6`, los 6 destinatarios de `Alertas:EquipoTecnico`.

Limpieza tras la prueba: cola de error purgada, capturador SMTP eliminado.

## 5. Regresión

`dotnet test CabinaFletes.Clean.sln` contra infraestructura real: **193/193**
(Domain 61, Application 75, Api 57 — 54 + 3 nuevas de
`ContratoCubicacionTests`). Sin regresiones.

## 6. Qué queda pendiente, honesto

- Nadie dispara `ICubicacionPort` todavía desde un flujo de usuario real —
  ver `docs/mensajeria/CUBICACION.md` sección 4 para las limitaciones
  conocidas (sin Outbox, sin trigger real, datos duplicados entre las dos
  bases). Cuando HU-04/HU-08 (u otra HU) necesite disparar esto de verdad,
  hay que resolver esas piezas primero.
- Solo se probó el camino "optimizar" (no "crear_desde_linea", que depende
  de un endpoint de NEXO que tampoco existe en esta rama — ver
  `cubicador/docs/CONTRATOS.md` sección D).
- El harness de verificación no quedó como prueba automatizada en CI (igual
  criterio que HU-041: acoplar el pipeline de `dotnet test` de NEXO a tener
  Cubicador+RabbitMQ corriendo es una decisión más grande que esta HU) — la
  garantía de contrato en cada build la dan las 3 pruebas de
  `ContratoCubicacionTests` (rápidas, sin infraestructura).
