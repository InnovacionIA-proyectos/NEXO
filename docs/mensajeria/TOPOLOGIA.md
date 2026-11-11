# Topología de mensajería

Referencia de las colas, los exchanges, los reintentos y la alerta de RabbitMQ en NEXO. Todo sale de un solo archivo,
`src/Nexo.Infrastructure/Common/Mensajeria/clsMensajeriaDependencyInjection.cs` (`AddMensajeria()`), y de las clases de
consumidores y sagas que allí se registran. Los mensajes que NEXO intercambia con el Cubicador, con sus routing keys y sus
formatos, están en [`CUBICACION.md`](CUBICACION.md).

## 1. El broker

- **MassTransit 8.5.10** sobre **RabbitMQ 3.13**. MassTransit está fijado en la rama 8.x porque desde la 9.0 tiene licencia
  comercial (ver el comentario en `src/Nexo.Infrastructure/Nexo.Infrastructure.csproj`). RabbitMQ usa una imagen propia,
  `build/Dockerfile.rabbit` (`rabbitmq:3.13-management-alpine` más el plugin `rabbitmq_delayed_message_exchange`): los
  timeouts de las sagas los programa el broker (`AddDelayedMessageScheduler` y `UseDelayedMessageScheduler`) para que
  sobrevivan a un reinicio de la API, y ese plugin no viene en la imagen oficial.
- **Un solo proceso.** `Nexo.Api` es a la vez API y worker: `clsProgram.cs` llama a `AddMensajeria()` y ahí viven todos los
  consumidores y las dos sagas. Si algún día se separa un proceso de workers, debe llamar al mismo método para quedar con
  la misma configuración.
- **Conexión.** `ConnectionStrings:RabbitMq` (o `RabbitMq:Host`). Si está vacía, el transporte va en memoria: sirve para
  pruebas y para levantar la API sin RabbitMQ, pero los mensajes no sobreviven a un reinicio. En Producción la aplicación
  **no arranca** sin esa cadena (guarda en `clsProgram.cs`). Para usar el broker corporativo solo cambia la cadena
  (`ConnectionStrings__RabbitMq`, ver `.env.example`).
- **Outbox transaccional** (`AddEntityFrameworkOutbox<clsNexoDbContext>` con `UseBusOutbox()`). Lo que un caso de uso publica
  se guarda en la misma transacción que sus datos: si la transacción no confirma, el mensaje no existe; si confirma, se
  entrega aunque el broker esté caído en ese momento. La tabla de salida se revisa cada 5 segundos (`QueryDelay`).
- **Las dos sagas se persisten en la base del negocio** (`EntityFrameworkRepository` sobre `clsNexoDbContext`) con bloqueo
  pesimista, porque dos mensajes de la misma instancia pueden llegar a la vez.

## 2. Reintentos y cola de error

La política se configura una sola vez, en la función local `Tuberia<T>`, y vale para todos los receive endpoints, incluidas
las sagas y las colas del Cubicador. La misma función se usa con RabbitMQ y en memoria, así que las pruebas corren sobre la
misma política que producción.

```csharp
cfg.UseMessageRetry(r =>
{
    r.Ignore<clsCorreoRechazadoException>();
    r.Intervals(
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(60));
});
```

- **Cuatro reintentos**, unos 81 segundos en total. Un fallo pasajero (una fuente externa que no responde, el broker que se
  reinicia) se resuelve solo.
- **Lo que no tiene arreglo no se reintenta.** `clsCorreoRechazadoException` (un correo que el servidor rechaza por su forma,
  por ejemplo un adjunto demasiado grande) va directo a la cola de error al primer intento.
- **Agotados los reintentos**, MassTransit:
  1. mueve el mensaje a la cola de error del endpoint que lo consumía, con el nombre de la cola más `_error` (por ejemplo,
     `costear-linea_error`);
  2. publica un `Fault<T>` en el exchange `MassTransit:Fault--<espacio de nombres>:<clase>--` (por ejemplo,
     `MassTransit:Fault--Nexo.Mensajes.Costeo:clsCostearLineaSolicitada--`). De ahí lo toman la alerta al equipo
     (sección 5) y, para el costeo de una línea, `clsFalloDeCosteoConsumer`.
- **Consumidores que nunca llegan a la cola de error a propósito:** los avisos en tiempo real (`clsAvisarTrabajoConsumer`,
  `clsAvisarBandejaConsumer`) registran el fallo y siguen, porque la consulta normal es la garantía y el aviso es solo una
  mejora. Los consumidores del plan de cargue hacen lo mismo con su aviso al front, pero no con la proyección en la base.
- **Mensajes que no se pueden leer** (JSON inválido o un sobre roto) no siguen este camino: no se reintentan ni generan
  `Fault<T>`, porque MassTransit nunca supo de qué tipo eran.

## 3. Colas y qué consume cada una

### Cómo se nombran

Los consumidores y las sagas no declaran el nombre de su cola: lo deriva MassTransit con el formateador
`clsFormateadorNombresSinPrefijoCls`, que aplica el formato kebab-case de MassTransit y luego quita el prefijo `cls-`. Así,
renombrar una clase con o sin el prefijo `cls` no crea una cola nueva ni deja huérfana la anterior.

| Registro | Regla | Ejemplo |
|---|---|---|
| Consumidor (`AddConsumer<T>`) | Nombre de la clase sin el sufijo `Consumer`, en kebab-case, sin `cls-` | `clsCostearLineaConsumer` → `costear-linea` |
| Consumidor genérico (`clsAlertaDeFalloConsumer<T>`) | Nombre del tipo de mensaje `T`, en kebab-case, sin `cls-` | `clsAlertaDeFalloConsumer<clsAutoCosteoSolicitado>` → `auto-costeo-solicitado` |
| Saga (`AddSagaStateMachine<TSaga, TEstado>`) | Nombre de la clase de **estado**, en kebab-case, sin `cls-` | `clsEstadoAutoCosteo` → `estado-auto-costeo` |
| Colas del Cubicador | Nombre fijo en `ReceiveEndpoint(...)`, no pasa por el formateador | `Cubicacion.clsPlanDeCargueGenerado` |

Los nombres de las tablas siguientes salen de aplicar esas reglas a las clases registradas.

### Sagas

| Cola | Saga | Mensajes que recibe |
|---|---|---|
| `estado-auto-costeo` | `clsAutoCosteoSaga` | `clsAutoCosteoSolicitado`, `clsPliegoRepartido`, `clsLineaCosteada`, `clsLineaFalloTecnico`, `clsLineaRequiereTarifaManual` y su propio timeout, `clsExpiracionAutoCosteo` (30 minutos). |
| `estado-priorizacion` | `clsSagaDePriorizacion` | `clsArrancarPriorizacion`, `clsParametrosDePriorizacionCambiaron` y su propio reloj, `clsTicDePriorizacion` (cada `Score:IntervaloRecalculoMinutos`, 15 por defecto, acotado entre 1 y 1.440 minutos). |

`clsArrancarPriorizacion` lo publica `clsArranqueDeLaCadenaHostedService` en cada arranque de la API. Es idempotente: la saga
se correlaciona por una constante, así que un segundo arranque rearma el reloj existente en vez de crear otro.

### Consumidores de NEXO

| Cola | Consumidor | Mensaje | Qué hace |
|---|---|---|---|
| `repartidor-de-lineas` | `clsRepartidorDeLineasConsumer` | `clsRepartirLineasDeCosteo` | Publica un `clsCostearLineaSolicitada` por línea del pliego y luego `clsPliegoRepartido` con el total. |
| `costear-linea` | `clsCostearLineaConsumer` | `clsCostearLineaSolicitada` | Costea la línea; publica `clsLineaCosteada` o `clsLineaRequiereTarifaManual`. Si una fuente falla, la excepción sube y se reintenta. |
| `fallo-de-costeo` | `clsFalloDeCosteoConsumer` | `Fault<clsCostearLineaSolicitada>` | Convierte el costeo agotado en `clsLineaFalloTecnico` para que la saga cuente la línea y no espere hasta el timeout. |
| `recosteo-por-tarifas` | `clsRecosteoPorTarifasConsumer` | `clsTarifasActualizadas` | Al cargar o extender tarifas, vuelve a encolar las líneas que esperan tarifa manual (`clsRecostearLineaSolicitada`). No pasa por la saga. |
| `recostear-linea` | `clsRecostearLineaConsumer` | `clsRecostearLineaSolicitada` | Recostea una línea. |
| `avisar-trabajo` | `clsAvisarTrabajoConsumer` | `clsTrabajoActualizado` | Lee el estado del trabajo y lo envía al front en tiempo real. |
| `avisar-bandeja` | `clsAvisarBandejaConsumer` | `clsBandejaCambio` | Avisa a la bandeja abierta que vuelva a consultar. |
| `recalcular-scores` | `clsRecalcularScoresConsumer` | `clsRecalcularScoresSolicitado` | Recalcula el score persistido de las solicitudes activas. |
| `notificar-entrega` | `clsNotificarEntregaConsumer` | `clsCotizacionEntregada` | Envía el correo de la cotización entregada. |
| `notificar-conexos-cambiados` | `clsNotificarConexosCambiadosConsumer` | `clsConexosCotizacionCambiados` | Avisa a quien creó la solicitud que cambiaron los servicios conexos de su cotización entregada. |
| `notificar-tarifa-vencida` | `clsNotificarTarifaVencidaConsumer` | `clsTarifaVencidaDetectada` | Correo de alerta por tarifa vencida. |
| `notificar-linea-fallo-tecnico` | `clsNotificarLineaFalloTecnicoConsumer` | `clsLineaFalloTecnico` | Correo de alerta por una línea que no se pudo costear por una falla técnica. Recibe el mismo evento que la saga, en su propia cola. |
| `notificar-cotizacion-ganada` | `clsNotificarCotizacionGanadaConsumer` | `clsCotizacionMarcadaGanada` | Correo de alerta por una cotización marcada como ganada. |

### Colas del Cubicador

Solo existen con RabbitMQ. Cada una se declara a mano con `ConfigureConsumeTopology = false` y un binding al exchange topic
`cubicador.eventos` con su routing key, y sus consumidores se excluyen de la configuración automática para que MassTransit
no cree un segundo endpoint con el mismo consumidor. El detalle de los mensajes está en [`CUBICACION.md`](CUBICACION.md),
sección 4.

| Cola | Routing key | Consumidor |
|---|---|---|
| `Cubicacion.clsPlanDeCargueGenerado` | `plan-de-cargue.generado` | `clsPlanDeCargueGeneradoConsumer` |
| `Cubicacion.clsCubicacionRechazada` | `cubicacion.rechazada` | `clsCubicacionRechazadaConsumer` |
| `Cubicacion.clsVehiculoCreado` / `.clsVehiculoActualizado` / `.clsVehiculoEliminado` | `vehiculo.creado` / `.actualizado` / `.eliminado` | `clsVehiculoCatalogoConsumer` |
| `Cubicacion.clsMercanciaCreada` / `.clsMercanciaActualizada` / `.clsMercanciaEliminada` | `mercancia.creada` / `.actualizada` / `.eliminada` | `clsMercanciaCatalogoConsumer` |
| `Cubicacion.clsCarroceriaCreada` / `.clsCarroceriaActualizada` / `.clsCarroceriaEliminada` | `carroceria.creada` / `.actualizada` / `.eliminada` | `clsCarroceriaCatalogoConsumer` |

Hay una cola por routing key, aunque un mismo consumidor atienda tres eventos, para que un fallo en `vehiculo.creado` no
bloquee `vehiculo.actualizado`. En sentido contrario, NEXO publica `clsCubicacionSolicitada` en `cubicador.eventos` con el
routing key `cubicacion.solicitada`; esa cola la consume el Cubicador, no NEXO.

Con el transporte en memoria no se declara nada de esto: los consumidores del Cubicador se configuran con los nombres por
defecto, como el resto, y solo sirven para pruebas.

### Colas de alerta

`clsAlertaDeFalloConsumer<T>` se registra una vez por cada tipo de mensaje que debe alertar, y cada registro crea su propia
cola con el nombre del mensaje:

| Mensajes | Colas |
|---|---|
| Auto-costeo y costeo | `auto-costeo-solicitado`, `repartir-lineas-de-costeo`, `pliego-repartido`, `costear-linea-solicitada`, `linea-costeada`, `linea-fallo-tecnico`, `linea-requiere-tarifa-manual`, `expiracion-auto-costeo` |
| Recosteo y avisos de trabajo | `tarifas-actualizadas`, `recostear-linea-solicitada`, `trabajo-actualizado` |
| Cubicador | `plan-de-cargue-generado`, `cubicacion-rechazada`, `vehiculo-creado`, `vehiculo-actualizado`, `mercancia-creada`, `mercancia-actualizada`, `carroceria-creada`, `carroceria-actualizada` |
| Alertas de negocio | `tarifa-vencida-detectada`, `cotizacion-marcada-ganada` |

Estas colas reciben `Fault<T>`, no el mensaje normal. `clsLineaFalloTecnico` alerta sin importar cuál de sus dos
consumidores (la saga o el correo) haya fallado, porque el `Fault<T>` es del tipo de mensaje.

**Mensajes sin alerta registrada hoy:** `clsBandejaCambio`, `clsRecalcularScoresSolicitado`, `clsCotizacionEntregada`,
`clsConexosCotizacionCambiados`, los tres de la saga de priorización (`clsArrancarPriorizacion`,
`clsParametrosDePriorizacionCambiaron`, `clsTicDePriorizacion`) y los tres eventos de borrado de catálogo
(`VehiculoEliminado`, `MercanciaEliminada`, `CarroceriaEliminada`). Si uno de ellos agota sus reintentos, el mensaje queda en
la cola de error y el log registra el fallo, pero nadie recibe correo. En `clsRecalcularScoresConsumer`,
`clsNotificarEntregaConsumer` y `clsNotificarConexosCambiadosConsumer` esto pesa más: dejan subir la excepción precisamente
para que el fallo no pase inadvertido.

**Por qué la lista es explícita.** MassTransit 8.5 no acepta registrar un consumidor genérico abierto
(`AddConsumer(typeof(clsAlertaDeFalloConsumer<>))`): solo tipos cerrados. Por eso va una línea por mensaje en
`AddMensajeria()`. **Si se agrega un mensaje nuevo y se olvida esa línea, ese mensaje no genera alerta**, sin error ni
advertencia. Quien agregue un mensaje debe agregar también su `x.AddConsumer<clsAlertaDeFalloConsumer<NuevoMensaje>>()`.

## 4. Exchanges

MassTransit crea en RabbitMQ dos clases de exchange:

- **Uno por tipo de mensaje**, con el nombre completo de la clase: `<espacio de nombres>:<clase>`, por ejemplo
  `Nexo.Mensajes.Costeo:clsCostearLineaSolicitada` o `Nexo.Mensajes.Solicitudes:clsAutoCosteoSolicitado`. Ahí se publica.
- **Uno por cola**, con el mismo nombre de la cola (por ejemplo `costear-linea`). El exchange del mensaje se enlaza al de
  cada cola que lo consume, y así un mismo evento llega a varias colas a la vez (por ejemplo, `clsLineaFalloTecnico` llega a
  `estado-auto-costeo` y a `notificar-linea-fallo-tecnico`).

El exchange de fallos (`MassTransit:Fault--<espacio de nombres>:<clase>--`) sigue la misma regla del nombre completo.

El formateador de la sección 3 solo protege los nombres de las **colas**. El exchange de un mensaje conserva el prefijo
`cls` y el espacio de nombres, así que renombrar o mover una clase de mensaje crea un exchange nuevo y deja el anterior sin
uso en el broker. Los mensajes del Cubicador son la excepción a esta topología: van por el exchange topic
`cubicador.eventos` y conservan su nombre sin el prefijo `cls` (ver [`CUBICACION.md`](CUBICACION.md), secciones 2 y 5).

## 5. Alerta al equipo

`clsAlertaDeFalloConsumer<T>` (`src/Nexo.Infrastructure/Common/Mensajeria/clsAlertaDeFalloConsumer.cs`):

1. Recibe el `Fault<T>` de cualquiera de los mensajes de la sección 3.
2. Registra un log `Error` con el tipo del mensaje, el `FaultId`, la hora y el motivo real (tipo y texto de la excepción).
3. Lee los destinatarios de `Alertas:EquipoTecnico` (lista fija de correos corporativos del equipo técnico en
   `src/Nexo.Api/appsettings.json`).
4. Envía un solo correo a todos los destinatarios, con el asunto `[NEXO] Mensaje <tipo> cayó a la cola de error`, el
   `FaultId`, la hora (UTC), el motivo y la indicación de revisar las colas terminadas en `_error` en la consola de RabbitMQ.
   El correo sale por `ICorreoPort`, el mismo puerto de las notificaciones: `clsMailSenderCorreoAdapter` si está configurado
   `MailSender:BaseUrl`, y si no `clsSmtpCorreoAdapter`.

Si la lista está vacía, si el correo no está configurado (`ICorreoPort.Configurado`) o si el envío falla
(`clsCorreoRechazadoException` o `clsCorreoTransitorioException`), la alerta registra un log `Warning` y termina sin lanzar
excepción: un correo que no sale no debe agotar a su vez los reintentos y caer en su propia cola de error.

La alerta no reemplaza a `clsFalloDeCosteoConsumer`: los dos reciben el mismo `Fault<clsCostearLineaSolicitada>`, cada uno en
su cola. La alerta avisa al equipo; `clsFalloDeCosteoConsumer` convierte el fallo en un desenlace de negocio para que la saga
cuente la línea.

## 6. Consola de administración

`http://localhost:15672` cuando el broker es el de `docker-compose.yml` (el puerto cambia con `RABBITMQ_UI_PORT`). El usuario y
la clave son `RABBITMQ_USER` (por defecto `nexo`) y `RABBITMQ_PASSWORD`. Ahí se ven las colas, los mensajes atascados y el
contenido de las colas `_error`, desde donde se puede reintentar un mensaje a mano.

El puerto AMQP (`5672`, o `RABBITMQ_AMQP_PORT`) también se publica al host, solo para herramientas que corren fuera de Docker;
la API y el Cubicador usan la red interna. No debe exponerse en producción.
