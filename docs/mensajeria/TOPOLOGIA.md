# Topología de mensajería (issue #13, S2-HU-041)

Documento de cierre del criterio "topología documentada" de la HU. No es
teoría: cada tabla de este documento se verificó contra un broker RabbitMQ
real (`docker compose up`, API de administración en `:15672`) — ver
`tests/QA/13-s2-hu-041-aprovisionar-rabbitmq-y-topologia-de-mensajeria/README.md`
para el registro completo de esa verificación.

## 1. El broker

- **MassTransit 8.5.10** sobre **RabbitMQ 3.13** (imagen propia,
  `build/Dockerfile.rabbit`, con el plugin `rabbitmq_delayed_message_exchange`
  — lo necesita el planificador de timeouts del Saga, no viene en la imagen
  oficial).
- Un solo proceso (`CabinaFletes.Api`) es hoy API y worker a la vez — todos
  los consumers viven en el mismo `AddMensajeria()` (`src/CabinaFletes.
  Infrastructure/Common/Mensajeria/MensajeriaDependencyInjection.cs`). Si en
  el futuro se separa un proceso de workers, ambos deben llamar al mismo
  método — está pensado para eso desde su comentario de cabecera.
- **Conexión**: `ConnectionStrings:RabbitMq` (o `RabbitMq:Host`). Vacío ⇒
  transporte en memoria (desarrollo/tests, sin persistencia real). **Guard de
  Producción** (`Program.cs`): si `ASPNETCORE_ENVIRONMENT=Production` y esa
  cadena está vacía, la app **no arranca** — mismo criterio que el guard de
  AzureAd. Cuando IT entregue el broker corporativo, solo cambia esa cadena;
  el código no se toca (ver `.env.example`).
- **Outbox transaccional** (`AddEntityFrameworkOutbox` + `UseBusOutbox()`):
  publicar dentro de un caso de uso escribe en la misma transacción que los
  datos de negocio — si la transacción no confirma, el mensaje no existe; si
  confirma, se entrega aunque el broker esté caído en ese instante (se
  entrega cuando vuelva). Barrido cada 5s (`QueryDelay`).

## 2. Reintentos escalonados

Configurados **una sola vez**, para TODOS los receive endpoints por igual
(`Tuberia<T>` en `MensajeriaDependencyInjection.cs` — la misma función se
usa con y sin broker, así que un test contra memoria prueba exactamente la
política que corre en producción):

```csharp
cfg.UseMessageRetry(r => r.Intervals(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(15),
    TimeSpan.FromSeconds(60)));
```

4 reintentos, ~81s de margen total antes de darse por vencido. Un fallo
transitorio (hipo de una fuente externa, el broker reiniciando) se resuelve
solo, sin que nadie se entere. Agotados los 4, MassTransit:

1. Mueve el mensaje original a la cola de error del endpoint que lo
   consumía, con el patrón `<NombreDeLaCola>_error` (ej. `CostearLinea_error`
   — **no** `<TipoDeMensaje>_error`; verificado, ver sección 4).
2. Publica `MassTransit.Fault<T>` al exchange `MassTransit:Fault--<tipo
   completo>--`, que a su vez alimenta el exchange genérico `MassTransit:Fault`.

## 3. Colas y qué consume cada una

Nombre de cola = nombre de la clase Consumer sin el sufijo "Consumer" (regla
por defecto de MassTransit), salvo el Saga, que usa el nombre del tipo de
estado (`EstadoAutoCosteo`).

| Cola | Consumer / Saga | Mensaje que recibe | Reintenta con la política de arriba |
|---|---|---|---|
| `AutoCosteoSolicitado` | `AlertaDeFalloConsumer<AutoCosteoSolicitado>` | `Fault<AutoCosteoSolicitado>` | — (solo escucha fallos) |
| `EstadoAutoCosteo` | `AutoCosteoSaga` | `AutoCosteoSolicitado`, `PliegoRepartido`, `LineaCosteada`, `LineaFalloTecnico`, `LineaRequiereTarifaManual`, `ExpiracionAutoCosteo.Received` | Sí |
| `RepartidorDeLineas` | `RepartidorDeLineasConsumer` | `RepartirLineasDeCosteo` | Sí |
| `CostearLinea` | `CostearLineaConsumer` | `CostearLineaSolicitada` | Sí |
| `FalloDeCosteo` | `FalloDeCosteoConsumer` | `Fault<CostearLineaSolicitada>` | — (de negocio: convierte el fallo en `LineaFalloTecnico` para que el Saga lo cuente) |
| `RecosteoPorTarifas` | `RecosteoPorTarifasConsumer` | `TarifasActualizadas` | Sí |
| `RecostearLinea` | `RecostearLineaConsumer` | `RecostearLineaSolicitada` | Sí |
| `AvisarTrabajo` | `AvisarTrabajoConsumer` | `TrabajoActualizado` | Sí |
| `PliegoRepartido`, `LineaCosteada`, `LineaFalloTecnico`, `LineaRequiereTarifaManual`, `RepartirLineasDeCosteo`, `RecostearLineaSolicitada`, `TarifasActualizadas`, `TrabajoActualizado`, `ExpiracionAutoCosteo` | `AlertaDeFalloConsumer<T>` (uno por tipo) | `Fault<T>` | — (solo escucha fallos) |

**Por qué la lista de `AlertaDeFalloConsumer<T>` es explícita y no automática**
(issue #13): MassTransit 8.5 no soporta registrar un `IConsumer<Fault<T>>`
genérico ABIERTO contra `IRegistrationConfigurator` — se intentó
`x.AddConsumer(typeof(AlertaDeFalloConsumer<>))` y falló en runtime con
`Type.ContainsGenericParameters is true` (verificado, no es una suposición).
La alternativa es una línea por tipo de mensaje
(`MensajeriaDependencyInjection.cs`, cerca del final del bloque
`AddMassTransit`). **Si se agrega un mensaje nuevo y se olvida esa línea, ese
mensaje simplemente no genera alerta** — sin excepción, sin romper nada,
solo silencio. Cualquiera que agregue un mensaje nuevo debe agregar también
su `AlertaDeFalloConsumer<NuevoMensaje>`.

## 4. Nombres reales de exchange (particularidad verificada)

MassTransit no usa siempre el nombre corto del tipo para el exchange — dos
convenciones conviven:

- **Consumer normal** (`AddConsumer<T>()` con una clase concreta cuyo nombre
  no es igual al del mensaje, como `CostearLineaConsumer`): exchange con el
  nombre corto del mensaje (`CostearLineaSolicitada`).
- **Evento de Saga** (`Event(() => X, ...)` dentro de un
  `MassTransitStateMachine`): exchange con el nombre **completo** (namespace
  incluido) del mensaje (`CabinaFletes.Mensajes.Solicitudes:
  AutoCosteoSolicitado`), que alimenta el exchange/cola del Saga
  (`EstadoAutoCosteo`).

Esto importa porque significa que para un mismo tipo de mensaje pueden
existir DOS exchanges distintos (uno corto, uno completo) sin que sea un
error — cada consumer (incluido `AlertaDeFalloConsumer<T>`) se engancha al
que le corresponde según cómo esté registrado. El exchange de **fallos**
(`MassTransit:Fault--<tipo completo>--`) SIEMPRE usa el nombre completo,
sin importar cuál de las dos convenciones use el consumer "feliz" — es una
propiedad del tipo del mensaje, no del consumer. Por esto
`AlertaDeFalloConsumer<T>` recibe el fallo sin importar si su propia cola
"normal" quedó con nombre corto o completo.

## 5. Alerta al equipo (issue #13, lo nuevo de esta HU)

`AlertaDeFalloConsumer<T>` (`src/CabinaFletes.Infrastructure/Common/
Mensajeria/AlertaDeFalloConsumer.cs`):

1. Recibe `Fault<T>` (cualquier tipo, ver tabla de arriba).
2. Registra un log `Error` con el motivo real (tipo y mensaje de la excepción).
3. Lee destinatarios de `Alertas:EquipoTecnico` (`appsettings.json`, array
   fijo — hoy: scalderon, dguerra, jpvelez, kcano, ivasco, bquintero @
   boterosoto.com.co). Si está vacío, o si el SMTP no está configurado
   (`ICorreoPort.Configurado`), degrada con un log `Warning` — **no** lanza
   excepción (un correo que no sale no debe, a su vez, agotar reintentos y
   terminar en su propia cola de error).
4. Envía un correo por destinatario vía `ICorreoPort`/`SmtpCorreoAdapter` —
   el mismo adaptador que ya usa Cotizaciones, movido a `Application/Common/
   Ports` porque ahora lo consume más de un módulo (mismo criterio ya usado
   con `IPropuestaTarifaPort`/`IAuditoriaPort`, ver `MIGRACION.md`).

No reemplaza a `FalloDeCosteoConsumer` (que sigue existiendo, específico de
`CostearLineaSolicitada`, y convierte el fallo en un desenlace de NEGOCIO
para que el Saga lo cuente) — corren en paralelo, cada uno con su propia
cola, ambos alimentados por el mismo `Fault<CostearLineaSolicitada>`.

## 6. Consola de administración

`http://localhost:15672` (usuario/clave de `RABBITMQ_USER`/
`RABBITMQ_PASSWORD`) — colas, mensajes atascados, contenido de las colas
`_error` para inspección manual.
