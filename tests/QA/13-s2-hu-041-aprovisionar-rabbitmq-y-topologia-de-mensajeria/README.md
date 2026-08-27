# Verificación QA — S2-HU-041 (issue #13)

Rama: `13-s2-hu-041-aprovisionar-rabbitmq-y-topología-de-mensajería`. Esta
rama partió directo de `main` (que ya trae fusionado HU-01.1/01.2/02.1/01.7/
04.2/04.3/04.4/04.5 vía PR #65) — no se creó desde cero: al llegar aquí ya
existía todo el broker, MassTransit, Outbox transaccional, el Saga de
auto-costeo y el canal SignalR, construidos por las HU-04.2 a 04.5. Antes de
escribir código se hizo una investigación completa de ese estado (código +
`.env.example` + los 4 READMEs de QA ya existentes) para no duplicar ni
entrar en conflicto con nada — ver el hallazgo del punto 0.

## 0. Hallazgo antes de empezar: 2 de los 3 criterios de cierre ya estaban resueltos

- **"Broker accesible desde los ambientes de la API/workers"**: ya
  implementado por HU-04.2. `Program.cs` tiene un guard que impide arrancar
  en Producción sin `ConnectionStrings:RabbitMq` (mismo patrón que el guard
  de AzureAd), y `.env.example` línea 133 ya dice explícitamente: *"cuando
  IT entregue el broker corporativo (issue #13) solo cambia
  ConnectionStrings__RabbitMq: el código no se toca"*. Nada que construir
  aquí — solo lo que se documenta como parte del criterio 2.
- **"Reintentos escalonados"**: ya implementados por HU-04.2
  (`MensajeriaDependencyInjection.cs`, `cfg.UseMessageRetry(r =>
  r.Intervals(1s, 5s, 15s, 60s))`). El propio comentario del código dice
  *"Agotados los intentos, MassTransit lo lleva a la cola de error (DLQ),
  que es donde la #13 conectará la alerta al equipo"* — confirmando que
  quien construyó HU-04.2 dejó esto a propósito para esta HU.
- **Lo único genuinamente pendiente**: la alerta al equipo cuando un mensaje
  cae a la cola de error, y el documento de topología. Eso es lo que trae
  este PR.

## 1. Qué trae este PR

- **`AlertaDeFalloConsumer<T>`** (`src/CabinaFletes.Infrastructure/Common/
  Mensajeria/AlertaDeFalloConsumer.cs`): consumer de `Fault<T>` que envía un
  correo al equipo técnico cuando un mensaje agota reintentos. Registrado
  como 11 tipos cerrados (`AddConsumer<AlertaDeFalloConsumer<CostearLineaSolicitada>>()`,
  etc.) — MassTransit 8.5 no soporta un `IConsumer<Fault<T>>` genérico
  ABIERTO contra `IRegistrationConfigurator` (se intentó
  `AddConsumer(typeof(AlertaDeFalloConsumer<>))`, revienta en runtime con
  `Type.ContainsGenericParameters is true` — ver sección 3).
- **`ICorreoPort` movido** de `Application/CasosDeUso/Cotizaciones/Interfaces`
  a `Application/Common/Ports` — ahora lo consume más de un módulo
  (Cotizaciones y Mensajería), mismo criterio ya aplicado a
  `IPropuestaTarifaPort`/`IAuditoriaPort` (ver `MIGRACION.md`).
- **`Alertas:EquipoTecnico`** (`appsettings.json`, array fijo con los 6
  destinatarios) — no es secreto, así que va commiteado, no por variable de
  entorno.
- **`docs/mensajeria/TOPOLOGIA.md`**: colas, exchanges, política de
  reintentos, y la particularidad real encontrada de nombres de exchange
  corto vs. completo (ver sección 4 de ese documento).

## 2. Criterios de cierre de la issue #13 — verificados contra infraestructura real, no solo leídos

| Criterio | Verificado cómo | Resultado |
|---|---|---|
| Broker accesible desde API/workers | Ya existía (HU-04.2); confirmado de nuevo con `GET /health` contra el stack real | ✅ |
| Topología documentada | `docs/mensajeria/TOPOLOGIA.md`, cada tabla contrastada contra la API de administración de RabbitMQ real (no de memoria) | ✅ |
| Alerta verificada: mensaje forzado a DLQ genera notificación | Forzado de verdad contra RabbitMQ real, correo capturado en un servidor SMTP de prueba (no el corporativo) | ✅ — ver sección 3 |

## 3. La prueba de la alerta — ejecutada de punta a punta, con evidencia real

**Entorno**: `docker compose up -d --build --wait` (db + rabbit + api,
`ASPNETCORE_ENVIRONMENT=Development`), con `Smtp:Host` apuntando a un
capturador SMTP local desechable (`rnwood/smtp4dev`, contenedor aparte, NO
el SMTP corporativo — para no mandar correos reales a los 6 destinatarios
durante la prueba).

**Cómo se forzó el fallo**: se publicó un `CostearLineaSolicitada` con
`SolicitudId`/`LineaId` inexistentes directo al broker (harness .NET
desechable, corrido dentro de la red Docker del compose — el puerto AMQP no
está publicado al host a propósito). `CostearLineaCommandHandler` no
envuelve la consulta de tarifa en try/catch (a propósito, según su propio
comentario: *"la excepción SUBE... perdería el paso a la cola de error que
pide el criterio 3"*), así que una línea inexistente produce un
`NotFoundException` real y determinístico en cada intento.

**Resultado observado, con timestamps reales del log del contenedor**:

```
15:03:00 fail: AlertaDeFalloConsumer[0] Mensaje CostearLineaSolicitada cayó
  a la cola de error tras agotar reintentos (..., 15:03:00). Motivo:
  CabinaFletes.Application.Common.Exceptions.NotFoundException:
  Línea de solicitud no encontrada.
15:03:01 dbug: Declare queue: name: CostearLinea_error, ...
15:03:01 dbug: RECEIVE .../FalloDeCosteo ... Fault<CostearLineaSolicitada>
  FalloDeCosteoConsumer(...)          <- el consumer de NEGOCIO, en paralelo
15:03:02-15:03:05 info: SmtpCorreoAdapter Correo enviado a
  scalderon@, dguerra@, jpvelez@, kcano@, ivasco@, bquintero@
  boterosoto.com.co: [NEXO] Mensaje CostearLineaSolicitada cayó a la cola
  de error
15:03:05 dbug: RECEIVE .../CostearLineaSolicitada ... Fault<CostearLineaSolicitada>
  AlertaDeFalloConsumer<CostearLineaSolicitada>(...)
```

Publicado a las ~14:59:39 (retries a 1s/5s/15s/60s ⇒ fault a los ~81s
después ⇒ coincide con el 15:03:00 observado).

**Confirmado por 3 fuentes independientes, no solo el log**:

1. **RabbitMQ real** (`GET /api/queues`, autenticado):
   `CostearLinea_error: messages=1, consumers=0` — el mensaje original
   queda ahí para inspección manual, nadie lo descarta solo.
2. **smtp4dev real** (`GET /api/Messages`, capturador SMTP):
   `rowCount: 6` — los 6 correos, uno por destinatario configurado en
   `Alertas:EquipoTecnico`, con el asunto exacto
   `[NEXO] Mensaje CostearLineaSolicitada cayó a la cola de error`.
3. **Ambos consumers de `Fault<CostearLineaSolicitada>` coexistiendo**:
   `FalloDeCosteoConsumer` (de negocio, ya existía) y
   `AlertaDeFalloConsumer<CostearLineaSolicitada>` (nuevo, de esta HU)
   procesaron el MISMO evento de fallo de forma independiente — confirma
   que la alerta no reemplaza ni interfiere con el manejo de negocio ya
   construido.

**Limpieza tras la prueba**: se purgó el mensaje de `CostearLinea_error`
(`DELETE .../queues/%2F/CostearLinea_error/contents`) y se apagó el
capturador SMTP — no queda ningún residuo en el broker ni se envió ningún
correo real a los 6 destinatarios.

## 4. Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ `dotnet build CabinaFletes.Clean.sln` — 0 errores |
| Regresión | ✅ `dotnet test CabinaFletes.Clean.sln` — **190/190**: `Domain.Tests` 61/61, `Application.Tests` 75/75, `Api.Tests` 54/54. `Api.Tests` necesita `TEST_DB_CONNECTION` apuntando al SQL Server real (el `db` del compose no publica el puerto al host, a propósito) — corrida desde un contenedor `dotnet/sdk:10.0` unido a `nexo_internal`, igual patrón que el publicador desechable de la sección 3 |
| Integración | ✅ Ver sección 3 — RabbitMQ real, correo real (a un catcher), 3 fuentes de evidencia independientes |
| Carga y estrés | No aplica a esta HU — `AlertaDeFalloConsumer<T>` es un consumer de bajo volumen por diseño (solo dispara cuando algo YA falló 4 veces), la carga del bus en sí ya se verificó en HU-04.2/04.3-04.4 |

## 5. Cómo repetir esta verificación

```bash
# 1) Capturador SMTP desechable (nunca el SMTP corporativo)
docker run -d --name smtp4dev-qa-test -p 5000:80 -p 2525:25 rnwood/smtp4dev

# 2) .env con Smtp__Host=host.docker.internal, Smtp__Puerto=2525, Smtp__Ssl=false
#    (ver .env.example para el resto de claves obligatorias)
docker compose up -d --build --wait --wait-timeout 240

# 3) Forzar el fallo (dentro de la red del compose - el puerto AMQP no
#    esta publicado al host a proposito): publicar CostearLineaSolicitada
#    con SolicitudId/LineaId al azar via un pequeno bus MassTransit
#    apuntando a amqp://<user>:<pass>@rabbit:5672/, corrido dentro de un
#    contenedor con --network nexo_internal.

# 4) Esperar ~90s (4 reintentos: 1s+5s+15s+60s) y revisar:
docker compose logs api --since 3m | grep -i "cayó a la cola de error"
curl -u <user>:<pass> http://localhost:15672/api/queues/%2F/CostearLinea_error
curl http://localhost:5000/api/Messages   # deberian aparecer 6 mensajes

# 5) Limpiar
curl -u <user>:<pass> -X DELETE http://localhost:15672/api/queues/%2F/CostearLinea_error/contents
docker rm -f smtp4dev-qa-test
```

## 6. Qué queda pendiente, honesto

- Solo se forzó el fallo para `CostearLineaSolicitada` (el camino con mejor
  soporte para un fallo determinístico y reproducible: una línea inexistente
  siempre lanza `NotFoundException`, sin depender de tumbar una fuente
  externa real). Los otros 10 tipos de mensaje usan exactamente el mismo
  mecanismo (`Fault<T>` → `AlertaDeFalloConsumer<T>` → `ICorreoPort`, sin
  ninguna rama de código específica por tipo), y la topología del exchange
  de fallos está confirmada por tipo en `docs/mensajeria/TOPOLOGIA.md`
  sección 4 (el nombre del exchange de `Fault<T>` depende únicamente del
  tipo del mensaje, no de cómo esté registrado su consumer) — pero no se
  forzó un fallo real para cada uno de los 11.
- El aprovisionamiento del broker corporativo (para ambientes reales, no
  Docker local) sigue dependiendo de IT — sin novedad respecto a lo que ya
  registró HU-04.2, ver `.env.example`.

## 7. Re-verificación tras acoplar Cubicador al mismo broker (2026-08-27)

Después del cierre de este PR se hicieron cambios adicionales al
`docker-compose.yml` raíz para que `NEXO/cubicador/` (otro servicio de este
mismo repo, antes con su propio RabbitMQ provisional) se conecte a este
mismo `rabbit` compartido en vez de duplicar broker — ver
`cubicador/docs/DECISIONES_TECNICAS.md` #9. Esos cambios (agregar `name:
nexo` a la cabecera, publicar el puerto AMQP al host, limpiar el bloque
`cubicador3d` comentado y obsoleto) tocan el mismo archivo que esta HU, así
que se repitió la verificación completa del criterio 3 para confirmar que
nada se rompió:

- Mismo fallo forzado (`CostearLineaSolicitada` con IDs inexistentes) →
  mismos 4 reintentos escalonados → `CostearLinea_error` con 1 mensaje → 6
  correos confirmados de nuevo en un capturador SMTP desechable
  (`rowCount=6`, los 6 destinatarios de `Alertas:EquipoTecnico`).
- `dotnet test CabinaFletes.Clean.sln` completo: **190/190** (Domain 61,
  Application 75, Api 54) — sin regresiones.
- Confirmado además que Cubicador comparte el broker sin colisión de
  nombres: su propia topología (`cubicador.eventos`, `cubicador.optimizar`,
  `cubicacion.dlq`) convive en el mismo `rabbit` sin cruzarse con ninguna
  cola/exchange de MassTransit de esta HU — ver
  `cubicador/docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.7
  para la verificación del lado de Cubicador.

## 8. CI hacia QA (issue #5)

Aparte, se confirmó que el pipeline de Cubicador
(`.github/workflows/cubicador-ci.yml`) ya dispara contra la rama `QA`, no
solo `main`, en `push` y `pull_request` (`branches: [QA, main]`) — el fix ya
estaba commiteado (`bc952b0`, anterior a esta rama) y sigue intacto. Cuando
se abra el PR de esta HU hacia QA, el CI de Cubicador correrá si el PR toca
`cubicador/**`; el backend C# de NEXO todavía no tiene un workflow de CI
propio en este repo (solo `cubicador-ci.yml` y `add-to-project-nexo.yml`
existen hoy en `.github/workflows/`) — fuera de alcance de esta HU.
