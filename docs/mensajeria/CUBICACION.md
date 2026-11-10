# Mensajería entre NEXO y el Cubicador

Contrato de los mensajes que intercambian NEXO (.NET) y el Cubicador (microservicio Python en `cubicador/`, mismo
repositorio) por RabbitMQ, y de las pocas llamadas HTTP que existen entre los dos. El mismo contrato, visto desde el
Cubicador, está en [`cubicador/CONTRATO.md`](../../cubicador/CONTRATO.md) (sección 6). Las colas, los reintentos y la alerta
del broker están en [`TOPOLOGIA.md`](TOPOLOGIA.md).

Decisiones relacionadas, en [`DECISIONES-NEXO.md`](../decisiones/DECISIONES-NEXO.md):
[D-07](../decisiones/DECISIONES-NEXO.md#d-07) (NEXO genera el id del plan),
[D-08](../decisiones/DECISIONES-NEXO.md#d-08) (el ajuste manual de colocaciones es asíncrono) y
[D-09](../decisiones/DECISIONES-NEXO.md#d-09) (el evento del plan viaja completo).

## 1. Resumen

| Dirección | Cómo | Qué |
|---|---|---|
| NEXO → Cubicador | Cola, routing key `cubicacion.solicitada` | Un solo tipo de mensaje (`clsCubicacionSolicitada`) con un campo `tipo` que dice qué hacer: crear, optimizar o borrar planes, ítems, vehículos y colocaciones (sección 3). |
| Cubicador → NEXO | Cola, varias routing keys | Resultado del plan, rechazos y cambios en los catálogos de vehículos, mercancías y carrocerías (sección 4). |
| NEXO → Cubicador | HTTP síncrono | Solo la cubicación rápida (`POST /api/cubicacion`) y la administración de catálogos del Cubicador (sección 7). |
| Cubicador → NEXO | HTTP síncrono | Solo la lectura de una línea de solicitud para armar un plan desde ella (sección 7). |

Todo va por un mismo exchange de tipo topic, `cubicador.eventos`, con MassTransit del lado de NEXO. Todo mensaje que NEXO
publica pasa por el Outbox: se guarda en la misma transacción que el cambio en la base y sale después.

## 2. Por qué la topología se configura a mano

El Cubicador usa su propio exchange topic con routing keys semánticas, no el exchange por tipo de mensaje que MassTransit
crea por defecto. Por eso la configuración de estos mensajes es explícita (`clsMensajeriaDependencyInjection`):

- **Publicar el comando.** El exchange se fija por tipo de mensaje (`SetEntityName("cubicador.eventos")`, tipo topic) y el
  routing key se fija en cada publicación (`SetRoutingKey("cubicacion.solicitada")`): MassTransit no permite fijarlo a nivel
  de topología para RabbitMQ. Por eso `clsCubicacionPublicadorAdapter` usa `IPublishEndpoint` directamente y no el puerto
  genérico de publicación, que no expone el routing key. Sin routing key, el mensaje llega al exchange pero no coincide con
  ninguna cola y se pierde sin error. El adaptador sigue pasando por el Outbox y valida el mensaje con
  `clsCubicacionSolicitadaValidador` antes de publicarlo.
- **Consumir los eventos.** Cada evento tiene su propia cola, declarada a mano con `ConfigureConsumeTopology = false` y un
  binding al exchange con su routing key. Las colas se declaran antes de la tubería común para heredar sus reintentos.
- **Nombres de cola con prefijo `Cubicacion.`** (`Cubicacion.clsPlanDeCargueGenerado`, `Cubicacion.clsCubicacionRechazada`,
  `Cubicacion.clsVehiculoCreado`…). Las demás colas de NEXO las nombra `clsFormateadorNombresSinPrefijoCls` (en minúsculas
  con guiones y sin `cls`, por ejemplo `plan-de-cargue-generado` para la alerta de fallos), así que hoy no hay choque de
  nombres; el prefijo deja claro en la consola de RabbitMQ qué colas son del puente con el Cubicador (ver
  [`TOPOLOGIA.md`](TOPOLOGIA.md)).

## 3. Comando NEXO → Cubicador: `cubicacion.solicitada`

### El sobre

```json
{ "tipo": "<uno de los 11 de la tabla>", "planId": "<guid>", "correlationId": "<guid>", "solicitud": { } }
```

`solicitud` cambia según el `tipo`. Los payloads exactos están en `src/Nexo.Mensajes/Cubicacion/MensajesDeCubicacion.cs`
(records `cls*Input`) y sus espejos en Python en `cubicador/CONTRATO.md` (sección 3.3).

### Los 11 tipos

| `tipo` | Endpoint de NEXO | Qué hace | `planId` lleva |
|---|---|---|---|
| `crear_plan` | `POST /api/planes-cargue` | Crea un plan con un vehículo. | El id que genera NEXO (D-07) |
| `crear_plan_demo` | `POST /api/planes-cargue/demo` | Crea un plan de demostración; el Cubicador elige vehículo e ítems. | Un id de correlación (el plan real lo genera el Cubicador) |
| `crear_desde_linea` | `POST /api/planes-cargue/desde-linea/{lineaId}` | Arma la flota (uno o más vehículos) con las posiciones de una línea de solicitud. | El id de correlación |
| `optimizar` | `POST /api/planes-cargue/{id}/optimizar` | Calcula el acomodo con la estrategia elegida. Es el único que pasa por la compuerta de paridad. | El plan |
| `guardar_item` | `POST /api/planes-cargue/{id}/items` | Crea o actualiza una unidad de carga. | El plan |
| `importar_items` | `POST /api/planes-cargue/{id}/items/importar` | Importa de 1 a 2.000 unidades. | El plan |
| `eliminar_item` | `DELETE /api/planes-cargue/{id}/items/{itemId}` | Borra una unidad. | El plan |
| `eliminar_plan` | `DELETE /api/planes-cargue/{id}` | Borra el plan. | El plan |
| `ajustar_colocacion` | `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}` | Mueve, rota, bloquea, retira o reinserta una unidad ya colocada (D-08). | El plan |
| `guardar_vehiculo` | `POST /api/vehiculos` | Crea o actualiza un vehículo del catálogo del motor 3D. | El id del vehículo |
| `eliminar_vehiculo` | `DELETE /api/vehiculos/{id}` | Borra un vehículo; se rechaza con `VEHICULO_EN_USO` si algún plan lo usa. | El id del vehículo |

Ejemplos de `solicitud`:

```json
// optimizar
{ "estrategia": "Combinada", "algoritmo": "bfd", "semilla": null, "ponderaciones": null, "respetarBloqueadas": false }

// crear_plan
{ "nombre": "Plan Bogotá-Medellín", "vehiculoId": "<guid del catálogo del Cubicador>" }

// crear_desde_linea
{ "lineaSolicitudId": "<guid de la línea>" }

// guardar_vehiculo
{ "id": "<vehiculoId>", "nombre": "...", "descripcion": null,
  "largoInternoMm": 6200, "anchoInternoMm": 2200, "altoInternoMm": 2000,
  "capacidadKg": 6300, "taraKg": 4200, "puertaAnchoMm": 2200, "puertaAltoMm": 1950,
  "puertaPosicion": "trasera", "ejes": [{ "posicionMm": 1000, "capacidadKg": 3500 }],
  "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0 }

// guardar_item (importar_items lleva { "items": [ ...mismo formato... ] })
{ "id": null, "sku": "SKU-1", "descripcion": "Pallet industrial", "pedido": null, "cliente": null, "destino": null,
  "secuenciaEntrega": 1, "largoMm": 1200, "anchoMm": 1000, "altoMm": 1400, "pesoKg": 800, "cantidad": 10,
  "color": null, "prioridad": 3, "tipoEmbalaje": null, "apilable": true, "maxNiveles": 0, "maxPesoEncimaKg": -1,
  "rotable": true, "mantenerVertical": false, "fragil": false, "peligroso": false, "refrigerado": false,
  "grupoCompatibilidad": null, "observaciones": null }

// eliminar_item
{ "itemId": "<guid del ítem>" }

// ajustar_colocacion
{ "colocacionId": "<guid>", "x": null, "y": null, "z": null,
  "orientacionIndice": null, "bloqueada": null, "retirar": true, "reinsertar": null }

// crear_plan_demo, eliminar_plan, eliminar_vehiculo
{ }
```

### Respuesta de los endpoints

Todos responden **202** de inmediato; el resultado llega después por los eventos de la sección 4.

```csharp
public record SolicitudDeCubicacionAceptadaDto(Guid PlanCargueId, string Estado, string ConsultarEn); // crear_plan
public record ComandoAceptadoDto(Guid CorrelationId, string Estado);                                   // el resto
```

- Son 202 aunque en algunos un 201 o un 204 parecería posible, porque la escritura definitiva la hace el Cubicador en su
  base. Es el mismo patrón del resto de los trabajos pesados (`docs/contrato-integracion/README.md`, sección 4).
- **`crear_plan`** deja en NEXO, en el mismo request, una copia del plan en estado **`procesando`**. El consumidor del
  resultado la pasa al estado real (`borrador`) cuando el Cubicador confirma. Así, un `optimizar` inmediato se rechaza con
  `409 PLAN_CARGUE_EN_PROCESO` en vez de llegar al Cubicador antes que el `crear_plan` (los dos van a la misma cola).
- **`eliminar_plan`** borra la copia de NEXO de inmediato (borrado optimista): si el Cubicador rechazara el borrado, la fila
  no se repone.
- Roles: los comandos del plan los pueden usar Comercial, AdminTarifas y Admin; `eliminar_vehiculo` solo Admin.
- Errores con la forma única de la API (`{ code, message, timestamp, details }`).

### Reglas al publicar

- **Compuerta de paridad:** solo `optimizar` la consulta. Cerrada, responde `503 CUBICACION_EN_VERIFICACION` sin publicar
  nada (ver [`CHECKLIST_DESPLIEGUE.md`](../despliegue/CHECKLIST_DESPLIEGUE.md), sección 1). Los demás comandos son
  operaciones de datos, no de cálculo.
- **Outbox:** el mensaje solo sale si el mismo `DbContext` llama a `SaveChangesAsync()` **después** de publicar. Si se
  guarda antes y no se vuelve a guardar, el mensaje nunca llega a `OutboxMessage`.

## 4. Eventos Cubicador → NEXO

| Routing key | Mensaje | Consumidor en NEXO | Qué hace |
|---|---|---|---|
| `plan-de-cargue.generado` | `PlanDeCargueGenerado` | `clsPlanDeCargueGeneradoConsumer` | Proyecta el plan completo en la base de NEXO (plan, ítems, colocaciones, métricas e incidencias). Si la fila no existía (por ejemplo, un plan de demostración), la crea. |
| `cubicacion.rechazada` | `CubicacionRechazada` | `clsCubicacionRechazadaConsumer` | Marca el plan en estado `error` con el código y el motivo. |
| `vehiculo.creado` / `vehiculo.actualizado` | `VehiculoCreado` / `VehiculoActualizado` | `clsVehiculoCatalogoConsumer` | Crea o actualiza el vehículo en la proyección de NEXO (`GET /api/vehiculos`). |
| `vehiculo.eliminado` | `VehiculoEliminado` | `clsVehiculoCatalogoConsumer` | Borra el vehículo de la proyección. No se publica si el Cubicador rechazó el borrado. |
| `mercancia.creada` / `.actualizada` / `.eliminada` | `MercanciaCreada`… | `clsMercanciaCatalogoConsumer` | Mantiene la proyección de tipos de mercancía. |
| `carroceria.creada` / `.actualizada` / `.eliminada` | `CarroceriaCreada`… | `clsCarroceriaCatalogoConsumer` | Mantiene la proyección de tipos de carrocería. |

**`plan-de-cargue.generado`** trae el resultado completo del Cubicador (`ResultadoPlanDto`), no solo un conteo:

```json
{
  "correlationId": "...", "planId": "...",
  "resultado": {
    "plan": { "id": "...", "nombre": "...", "vehiculoId": "...", "lineaSolicitudId": null, "numeroVehiculo": 1,
              "estado": "optimizado", "estrategia": "Combinada", "algoritmo": "bfd", "semilla": null,
              "pesoTotalKg": 500.0, "volumenUtilizadoM3": 1.2, "utilizacionVolumen": 45.0, "utilizacionPiso": 60.0,
              "puntaje": 75.1, "items": [ { "...": "los campos de guardar_item" } ] },
    "vehiculo": { "...": "el formato de guardar_vehiculo" },
    "colocaciones": [ { "id": "...", "itemPlanCargueId": "...", "x": 0.0, "y": 0.0, "z": 0.0, "cargada": true, "...": "geometría completa" } ],
    "metricas": { "...": "o null si el plan no se ha optimizado" },
    "incidencias": [ { "severidad": "advertencia", "codigoRegla": "...", "...": "" } ]
  }
}
```

**`cubicacion.rechazada`:** `{ "correlationId", "planId", "codigo", "mensaje" }`. Es el mismo evento para cualquier comando
que el Cubicador no pueda aplicar; así se entera el front, por ejemplo, de que un ajuste de colocación no era válido.

**Eventos de catálogo:** llevan el objeto completo (`{ "correlationId", "vehiculo": { ... } }`), salvo los de borrado, que
solo llevan el id. Los consumidores son idempotentes: crear o actualizar es un upsert por id, y borrar algo que no existe no
es un error. Si la proyección queda desalineada, `POST /api/tipos-carroceria/republicar-catalogo`,
`/api/tipos-mercancia/republicar-catalogo` y `/api/vehiculos-catalogo/republicar-catalogo` (AdminTarifas y Admin) piden al
Cubicador que vuelva a emitir el evento de creación de todas las filas.

**Avisos al front:** después de procesar un resultado o un rechazo, NEXO avisa por el canal `/hubs/plan-cargue` al grupo del
plan. El front se suscribe con `Suscribir(planCargueId)`. El aviso es una ayuda: la fuente de verdad son los `GET` de la
sección 6.

Los formatos de estos eventos están protegidos por `tests/Nexo.Api.Tests/Cubicacion/clsContratoCubicacionTests.cs`, que
deserializa ejemplos copiados de lo que publica el Cubicador.

## 5. Compatibilidad con el Cubicador (Python)

El Cubicador no usa MassTransit. Para hablar con NEXO hace dos cosas en la frontera:

- **Al consumir `cubicacion.solicitada`** (`cubicador/worker/main.py`): si el JSON trae el sobre de MassTransit
  (`message` y `messageType`), lo desenvuelve; si no, lo usa tal cual.
- **Al publicar sus eventos** (`cubicador/infrastructure/mensajeria/rabbitmq.py`): los envuelve en el sobre de MassTransit
  con el `messageType` de la clase C# correspondiente (por ejemplo, `urn:message:Nexo.Mensajes.Cubicacion:PlanDeCargueGenerado`).

Este acoplamiento es aceptado: **si se renombra una de esas clases en C#, el Cubicador deja de coincidir sin ningún error de
compilación.** Por eso los eventos conservan su nombre sin el prefijo `cls`. Lo protegen `clsContratoCubicacionTests` y
`clsContratoMessageTypeTests` de este lado, y `test_eventos_catalogo.py` del lado del Cubicador.

## 6. Proyección de lectura

NEXO guarda en su propia base lo que llega en los eventos, y estos endpoints leen **solo** de esa proyección: ninguno
consulta al Cubicador.

| Endpoint | Responde |
|---|---|
| `GET /api/vehiculos` | Vehículos del catálogo del motor 3D |
| `GET /api/planes-cargue` | Los 100 planes más recientes (resumen) |
| `GET /api/planes-cargue/{id}` | El plan |
| `GET /api/planes-cargue/{id}/resultado` | Detalle completo (`ResultadoPlanDto`) |
| `GET /api/planes-cargue/{id}/flota` | Los vehículos de la flota |
| `GET /api/planes-cargue/{id}/metricas` | Métricas, o `null` si el plan no se ha optimizado |
| `GET /api/planes-cargue/{id}/no-cargadas` | Unidades que no cupieron |
| `GET /api/planes-cargue/{id}/colocaciones` | Colocaciones |
| `GET /api/planes-cargue/{id}/exportar` | Lo mismo que `/resultado` |
| `GET /api/planes-cargue/por-linea/{lineaId}` y `.../estado` | Los planes armados desde una línea y su estado (procesando, listo o rechazado) |

Los pueden consultar todos los roles de NEXO: son lecturas y no cambian estado. Los DTOs están en
`src/Nexo.Application/CasosDeUso/PlanDeCargue/DTOs/PlanCargueDtos.cs` y espejan los del Cubicador, con `Guid` y `decimal` en
lugar de `string` y `float`.

## 7. Llamadas HTTP síncronas

Son las únicas excepciones a la regla de comunicarse por cola, y cada una tiene su motivo:

- **Cubicación rápida** (`POST /api/cubicacion` → `POST /recomendaciones` del Cubicador). Es una recomendación de vehículo
  sin plan ni nada que persistir después, que responde en cientos de milisegundos; un patrón de solicitud y respuesta por
  cola habría sido un mecanismo nuevo para un solo uso. Usa la llave `Cubicador3D:RecomendacionesApiKey` (igual
  a `CUBICADOR_API_KEY_NEXO` del Cubicador, de la que sale el sistema de origen de la recomendación), que solo vive en la
  configuración del backend. Sin `Cubicador3D:OutboundBaseUrl` o `RecomendacionesApiKey`, responde
  `503 CUBICACION_RAPIDA_NO_CONFIGURADA`. La reconsulta de la última corrida y la explicación usan la misma llave.
- **Administración de catálogos del Cubicador** (`/api/tipos-mercancia`, `/api/categorias-mercancia`,
  `/api/reglas-incompatibilidad`, `/api/tipos-carroceria`, `/api/vehiculos-catalogo`, `/api/parametros-motor`). NEXO reenvía
  la petición al Cubicador, que es el dueño de esos catálogos, con la llave genérica `Cubicador3D:OutboundApiKey` (igual a
  `CUBICADOR_API_KEY`); los cambios vuelven a NEXO por los eventos de catálogo.
- **Plan desde una línea** (`GET /api/lineas-solicitud/{id}/cubicacion`). Cuando procesa `crear_desde_linea`, el Cubicador
  pide a NEXO las posiciones de la línea. El endpoint solo acepta la llave del Cubicador (`Cubicador3D:InboundApiKey`, igual a
  `NEXO_CALLBACK_API_KEY`).

Ningún navegador llama al Cubicador directamente: el front usa solo los endpoints de NEXO, y el Cubicador no tiene CORS.

## 8. Reintentos, cola de error y alerta

Los consumidores de esta sección son consumidores normales de MassTransit y usan la misma tubería que el resto de NEXO:

- **Reintentos:** 1 s, 5 s, 15 s y 60 s. Se comprobó con un fallo forzado: entre el primer reintento y el envío a la cola de
  error pasaron 81 segundos, la suma exacta.
- **Cola de error y alerta:** al agotar los reintentos MassTransit publica un `Fault<T>`, y `clsAlertaDeFalloConsumer<T>`
  envía un correo a la lista `Alertas:EquipoTecnico`. No hay código de alerta propio de estos mensajes.
- **Mensajes que ni siquiera se pueden leer** (JSON inválido o un sobre roto) no siguen ese camino: MassTransit los clasifica
  como `ReceiveFault`, sin reintentos ni alerta por `Fault<T>`, porque nunca llegó a saber de qué tipo eran.

## 9. Limitaciones conocidas

- **`crear_desde_linea` publica un solo evento.** Si la carga necesita varios vehículos, el Cubicador arma toda la flota
  pero solo publica `plan-de-cargue.generado` del primero, así que `GET /api/planes-cargue/por-linea/{lineaId}` puede
  devolver una lista incompleta.
- **Un rechazo de `guardar_vehiculo` no avisa a nadie en NEXO.** Un vehículo no es un plan, así que `cubicacion.rechazada` no
  encuentra fila que marcar: el vehículo simplemente no aparece en `GET /api/vehiculos`. El log y la cola de error del
  Cubicador son la única pista.
- **La copia del plan en NEXO usa una fila mínima de vehículo** (solo id y nombre) para cumplir la llave foránea de
  `PlanCargue.VehiculoId` (D-07). La proyección la completa cuando llega el primer evento con ese vehículo.
- **No hay relleno automático de planes antiguos.** Un plan cuyo evento NEXO nunca procesó no aparece con detalle; poblarlo
  exigiría un script aparte.
