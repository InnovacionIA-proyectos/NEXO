# Pruebas de integración y verificación de infraestructura

Complementa a [`REGLAS_DE_NEGOCIO.md`](./REGLAS_DE_NEGOCIO.md) (que documenta
las 33 pruebas unitarias del motor). Este documento cubre tres niveles más:
las 6 pruebas de integración automatizadas (`tests/api/test_smoke.py` +
`tests/worker/test_ejecutar_solicitud.py`), el smoke test de CI contra
Docker Compose real (`.github/workflows/ci.yml` + `scripts/ci_docker_smoke.py`,
automatiza las secciones 2.2-2.4 de abajo en cada push/PR), y la
verificación manual contra infraestructura real (Docker Compose completo con
worker parado a propósito, y el repo NEXO real) que sigue sin automatizarse
pero se ejecutó y se registra aquí con su resultado exacto.

## 1. `tests/api/test_smoke.py` — 4 pruebas automatizadas (`pytest`)

Ejercitan la pila completa **api → application → infrastructure → domain**
contra una base de datos SQLite real (no mocks), usando `httpx.AsyncClient`
con `ASGITransport` para hablarle a la app de FastAPI directamente en
memoria, sin necesidad de un servidor corriendo.

| Test | Qué verifica |
|---|---|
| `test_health` | `GET /health` responde 200. |
| `test_flujo_completo_crear_optimizar` | El flujo de negocio completo: listar vehículos (dispara la siembra de los 9 presets) → crear plan → agregar 3 unidades de un ítem que sabemos que cabe → `POST /optimizar` (verifica 202 + `estado: "procesando"` + que quedó publicado en el `_PublisherFalso`) → simula al worker llamando `optimizar_plan` directo contra la misma base → `GET /resultado` (verifica que las 3 unidades quedaron cargadas y el estado pasó a `optimizado`) → ajustar manualmente una colocación (bloquearla, verifica que el estado pase a `ajustado`). |
| `test_cubicacion_rapida` | `POST /api/cubicacion` evalúa contra las 9 unidades del catálogo y devuelve una sugerencia. |
| `test_sin_api_key_es_rechazado` | Una petición sin `Authorization: Bearer` recibe 401. |

**Detalle técnico que vale la pena recordar** si estos tests empiezan a fallar
de forma rara: `httpx.ASGITransport` **no dispara el `lifespan` de FastAPI**
(donde vive `crear_tablas()` y la conexión del publisher de RabbitMQ), así
que el fixture `client` en `test_smoke.py` llama a `crear_tablas()`
explícitamente antes de instanciar el cliente, y sobreescribe la dependencia
`obtener_eventos_publisher` con un `_PublisherFalso` (que solo registra lo
publicado, sin conectarse a ningún broker) vía
`app.dependency_overrides` — sin eso, cualquier request a `/optimizar` o
`/desde-linea` fallaría con `RuntimeError: El publisher de RabbitMQ no se ha
inicializado`. Y SQLite en modo `:memory:` crea una base nueva por cada
conexión — sin `poolclass=StaticPool` en `infrastructure/persistence/db.py`
(aplicado solo cuando la URL contiene `:memory:`), cada request (o la sesión
nueva que abre `_simular_worker_optimizar`) vería una base vacía distinta.
Estos detalles ya están resueltos en el código; se documentan aquí porque son
la causa más probable si alguien "rompe" esto sin querer al tocar `db.py` o
`dependencias.py`.

## 1.1 `tests/worker/test_ejecutar_solicitud.py` — 2 pruebas automatizadas (`pytest`)

Guarda de regresión del bug real encontrado en la sección 2.6: llaman a
`worker.main._ejecutar_solicitud(cuerpo, planes, vehiculos)` con `cuerpo`
armado exactamente como lo arma `RabbitMqEventosPublisher._publicar` (tipo,
planId, correlationId, **solicitud anidada** — no a mano, para que un cambio
de forma futuro en cualquiera de los dos lados rompa el test en vez de
descubrirse solo contra un broker real).

| Test | Qué verifica |
|---|---|
| `test_ejecutar_solicitud_optimizar_desempaca_solicitud_anidada` | El camino `optimizar` (que nunca tuvo el bug) sigue leyendo `cuerpo["solicitud"]` correctamente. |
| `test_ejecutar_solicitud_crear_desde_linea_desempaca_solicitud_anidada` | El camino `crear_desde_linea` (el que SÍ tenía el bug) lee `cuerpo["solicitud"]["lineaSolicitudId"]`, no `cuerpo["lineaSolicitudId"]`. Usa un `NexoLineaSolicitudClient` falso (vía `monkeypatch.setattr(worker_main, "NexoLineaSolicitudClient", ...)`) para no depender de NEXO real. |

## 1.2 `tests/worker/test_mover_a_dlq.py` — 2 pruebas automatizadas (`pytest`)

Cubre el criterio de cierre "Reintentos + DLQ + alerta ante fallos repetidos
del motor" (ver `docs/DECISIONES_TECNICAS.md` #10) a nivel unitario, sin
necesitar broker ni SMTP real — la verificación con infraestructura real
está en la sección 2.8.

| Test | Qué verifica |
|---|---|
| `test_mover_a_dlq_dispara_la_alerta` | `worker/main.py::_mover_a_dlq` llama a `alertar_mensaje_en_dlq(tipo, error, intentos)` con los valores correctos, y publica el mensaje crudo a `cubicacion.dlq` con los headers esperados — publisher y mensaje son dobles de prueba. |
| `test_alertar_mensaje_en_dlq_degrada_sin_smtp_configurado` | `alertar_mensaje_en_dlq` no lanza excepción (solo loggea un warning) si `ALERTAS_SMTP_HOST` está vacío — un correo que no sale no debe tumbar el worker. |

### Cómo correrlas

```bash
pip install -r requirements.txt   # incluye pytest + pytest-asyncio
pytest tests/api -v
```

## 2. Verificación manual contra Docker Compose real

**Nota (2026-08-27)**: esta sección se escribió cuando RabbitMQ era un
contenedor propio de este `docker-compose.yml` (4 contenedores: `db`,
`rabbitmq`, `api`, `worker`). Eso ya no es así — ver
`docs/DECISIONES_TECNICAS.md` #9: ahora son **3 contenedores propios** (`db`,
`api`, `worker`) que se conectan al `rabbit` COMPARTIDO de NEXO. Se dejan los
relatos de bugs de abajo tal como se encontraron en su momento (siguen siendo
reales y relevantes para entender el porqué de `conectar_con_reintentos`),
pero **el número de contenedores, el nombre del servicio `rabbitmq` y los "5
intentos" ya no reflejan el estado actual** — ver la nota al final de 2.1.

**No toda está automatizada** (el smoke test de CI, sección 2.4, cubre parte
de esto en cada push/PR) — el resto se ejecutó a mano contra el stack completo
(hoy: `rabbit` compartido de NEXO ya corriendo + `db`/`api`/`worker` propios
de Cubicador) y se registra aquí el resultado exacto para que quede como
referencia de qué se probó y cuándo.

### 2.1 Los contenedores arrancan sanos

```bash
cd .. && docker compose up -d --wait rabbit && cd cubicador   # el rabbit compartido de NEXO primero
cp .env.example .env   # completar los valores
docker compose up -d --build
docker compose ps
```

Resultado esperado: `db` en estado `healthy` (tiene `healthcheck` definido en
`docker-compose.yml`); `api` y `worker` `Up` sin reinicios. **Si
`api`/`worker` se caen con `ConnectionRefusedError` apenas arrancan**: revisar
primero que el `rabbit` de NEXO esté realmente `healthy` (`cd .. && docker
compose ps rabbit`) — sin eso, el margen de reintentos (30 intentos/2s, ver
nota al final de esta sección) puede no ser suficiente si el operador levantó
este stack mucho antes que el de NEXO. Para Postgres, sigue aplicando la
condición `depends_on: db: condition: service_healthy` que ya está en
`docker-compose.yml` (Postgres tarda ~15s en su ciclo de init/restart interno
la primera vez; sin el healthcheck, `api`/`worker` intentan conectarse antes
de que esté listo).

**Otro bug encontrado al reconstruir el stack tras conectar los routers a
RabbitMQ**: `api` fallaba al arrancar con `AMQPConnectionError: [Errno 111]
Connection refused` — le faltaba la variable `CUBICADOR_RABBITMQ_URL` en su
bloque `environment` de `docker-compose.yml` (solo `worker` la tenía), así
que `api` intentaba conectarse al default `localhost:5672` **dentro de su
propio contenedor**, donde no hay ningún RabbitMQ escuchando. Corregido
agregando la misma variable (apuntando a `rabbitmq:5672`, el nombre del
servicio en la red de compose) al bloque de `api`.

**Un tercer bug de arranque, encontrado ya con el Cubicador movido a
`NEXO/cubicador/`**: incluso con el `depends_on: rabbitmq: condition:
service_healthy` correcto, `api`/`worker` a veces se caían en un arranque en
frío (`docker compose down -v` + `up`) con `AMQPConnectionError: [Errno 111]
Connection refused` al conectar el publisher. Causa: el healthcheck de
RabbitMQ (`rabbitmq-diagnostics -q ping`) puede dar "sano" un instante antes
de que el puerto AMQP (5672) esté realmente aceptando conexiones — una
carrera real, no una condición de `depends_on` mal puesta. `aio_pika.
connect_robust` solo reintenta reconexiones DESPUÉS de conectar una vez, no
el primer intento, así que ese primer fallo tumbaba el contenedor completo
(`ERROR: Application startup failed. Exiting.`) y había que reiniciarlo a
mano (`docker compose up -d api worker`). Corregido agregando
`conectar_con_reintentos()` en `infrastructure/mensajeria/rabbitmq.py`
(entonces 5 intentos, 2s de espera entre cada uno) — usado tanto por
`RabbitMqEventosPublisher.conectar()` como por la conexión propia del
worker en `worker/main.py::main()`. Reproducido de nuevo a propósito
(`down -v` + `up`, sin `--wait`) para confirmar: esta vez los logs muestran
3 intentos fallidos seguidos de uno exitoso, y ningún contenedor se cayó.

**Actualización (2026-08-27, unificación con el `rabbit` de NEXO)**: el
mismo problema, pero peor — al conectarse ahora al `rabbit` compartido de
NEXO (proyecto Compose separado, ver `docs/DECISIONES_TECNICAS.md` #9), no
hay forma de expresar `depends_on: rabbit: condition: service_healthy` entre
los dos proyectos, así que no hay ninguna garantía de orden de arranque en
absoluto. Se subió el margen de `conectar_con_reintentos()` de 5 intentos/2s
a **30 intentos/2s** — verificado necesario y suficiente: forzando el peor
caso (arrancar Cubicador antes que el `rabbit` de NEXO, sin que existiera
todavía), la conexión se logró en el intento 4 de 30 una vez el broker quedó
listo.

### 2.2 Flujo REST completo contra Postgres real

Se ejecutó, contra `http://localhost:8000` con el stack de arriba levantado:

1. `GET /api/planes-cargue/vehiculos` → sembró y devolvió los 9 presets.
2. `POST /api/planes-cargue` con el furgón de 13,6 m → plan en estado `borrador`.
3. `POST /api/planes-cargue/{id}/items` con un pallet 1200×1000×1000mm, 500kg, cantidad 3.
4. `POST /api/planes-cargue/{id}/optimizar` → las 3 unidades quedaron `cargada: true`, estado `optimizado`, puntaje `75.1`.
5. `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}` con `{"bloqueada": true}` → estado pasó a `ajustado`.
6. `POST /api/cubicacion` con 5 cajas de 100³cm/50kg → evaluó las 9 unidades y sugirió "Turbo con estacas".

**Bug real encontrado en este paso** (no lo detectó ni el motor ni el smoke
test): `metricas.puntaje` en la respuesta de `optimizar`/`GET /resultado`
daba siempre `0`, aunque `plan.puntaje` sí traía el valor correcto (`75.1`).
Causa: `obtener_resultado()` no copiaba `plan.puntaje` al objeto
`ResultadoCubicacion` reconstruido antes de pedirle las métricas al motor —
el original en NEXO sí lo hace explícito
(`resultado.Puntaje = plan.Puntaje` en `ResultadoPlanReader`), porque
`Metricas()` copia `r.Puntaje`/`r.PuntajesParciales` tal cual a la salida.
Corregido en `application/plan_cargue/resultado.py`; reverificado con el
mismo plan y confirmado `metricas.puntaje == plan.puntaje == 75.1`.

### 2.3 Flujo asíncrono end-to-end contra RabbitMQ real

Se publicó un mensaje real en el exchange `cubicador.eventos` (routing key
`cubicacion.solicitada`) usando un script desechable con `aio-pika`,
apuntando al RabbitMQ del `docker-compose` (puerto `5672` expuesto al host):

```json
{
  "tipo": "optimizar",
  "planId": "<uuid de un plan con items sin optimizar>",
  "correlationId": "<mismo uuid>",
  "solicitud": {"estrategia": "Combinada", "algoritmo": "bfd"}
}
```

Resultado observado:
1. El `worker` (contenedor separado, corriendo `python -m worker.main`) consumió el mensaje de la cola `cubicador.optimizar`.
2. Ejecutó `optimizar_plan` contra la base de datos Postgres compartida con `api`.
3. Publicó de vuelta un evento `plan-de-cargue.generado` con el resultado (`{"estado": "optimizado", "puntaje": 75.1, "colocaciones": 2}`), recibido por una cola de prueba suscrita a esa routing key.
4. Se confirmó por HTTP (`GET /api/planes-cargue/{id}`) que el plan quedó **persistido** como `optimizado` en la base de datos — no que el worker solo "dijo" que lo hizo.

Esto prueba que el camino asíncrono (worker + RabbitMQ + Postgres compartido)
funciona de punta a punta a nivel de mensajería. La sección 2.4 repite la
misma verificación pero disparada desde el router HTTP real (`POST
/optimizar`), que es como se usa en producción.

**Actualización (2026-08-27)**: el paso 3 de arriba ("recibido por una cola
de prueba suscrita a esa routing key") — el criterio de cierre "trabajo
publicado -> resultado recibido por cola" — ya no es solo verificación
manual: `scripts/ci_docker_smoke.py::flujo_resultado_por_cola` automatiza
exactamente este mismo camino y corre en cada push/PR contra `QA`/`main` —
ver sección 2.7.

### 2.4 Routers HTTP async, reintentos, Dead-Letter Queue y timeout de vigilancia

Verificación ejecutada tras conectar `POST /{id}/optimizar` y `POST
/desde-linea/{id}` a `publicar_cubicacion_solicitada` (ver
`docs/DECISIONES_TECNICAS.md` #5 y #10), contra el mismo stack de Docker
Compose reconstruido (`docker compose up -d --build api worker`):

**a) Flujo 202 → worker → resultado, disparado por HTTP real (no un script
que publica directo al exchange):**

1. `POST /api/planes-cargue/{id}/optimizar` con un plan recién creado →
   respondió `202` de inmediato con el plan en `"estado": "procesando"`.
2. Sin ninguna otra acción manual, el `worker` consumió el mensaje de
   `cubicador.optimizar` (confirmado por las colas de RabbitMQ: `messages: 0`
   en `cubicador.optimizar` segundos después) y corrió `optimizar_plan`.
3. `GET /{id}/resultado` mostró `estado: "optimizado"`, `puntaje: 75.1` — el
   mismo resultado que la verificación 2.2, pero esta vez todo el camino
   (encolar + consumir + persistir) pasó por HTTP + RabbitMQ real, no por un
   script de prueba publicando directo al exchange.

**b) Reintentos y Dead-Letter Queue**, publicando mensajes directo al
exchange (para forzar los dos casos que el router normal no produce):

- Un mensaje con `"planId": "no-existe-123"` (dispara `NotFoundException`,
  un `AppException` de negocio) se procesó **una sola vez** y no apareció en
  `cubicacion.dlq` — confirma que los errores de negocio no entran al ciclo
  de reintentos (reintentar daría el mismo resultado siempre).
- Un mensaje con `"tipo": "tipo_invalido_para_forzar_error"` (dispara
  `ValueError`, un error inesperado) se reintentó exactamente 3 veces — los
  logs del worker muestran `Fallo procesando mensaje (intento 1/3)`,
  `(intento 2/3)`, `(intento 3/3)` — y al agotar los intentos apareció en
  `cubicacion.dlq` (`messages: 1`, confirmado vía la API de administración
  de RabbitMQ, `GET /api/queues`). Purgado manualmente después de confirmar.
- **Actualización (2026-08-27)**: el mismo escenario, repetido ya con la
  alerta al equipo (`infrastructure/alertas/correo.py`) conectada, contra un
  capturador SMTP real — los 6 destinatarios confirmados con evidencia
  completa en `docs/DECISIONES_TECNICAS.md` #10.

**c) Vigilante de timeout**, forzando el escenario sin esperar los 5 minutos
reales configurados (`TIMEOUT_PROCESANDO_MINUTOS`):

1. Se detuvo el contenedor `worker` (`docker compose stop worker`).
2. Se creó un plan y se llamó `POST /optimizar` — quedó en `procesando` sin
   nadie que lo consuma (worker apagado).
3. Se invocó `SqlPlanCargueRepository.marcar_error_por_timeout(limite=ahora)`
   directamente (dentro del contenedor `api`, contra el Postgres real, con
   `limite` = el instante actual en vez de "ahora menos 5 minutos", para no
   tener que esperar) → devolvió `marcados: 1`.
4. `GET /{id}` confirmó `estado: "error"` en el plan.

> **Actualizado en S3-HU-06.1.** Ese método ya no existe: se partió en
> `obtener_atascados(limite)` (solo consulta) más el `marcar_error` que ya
> había, y el vigilante ahora además **publica `cubicacion.rechazada` con
> `codigo: "TIMEOUT_PROCESAMIENTO"` antes** de marcar el plan (ver CONTRATO.md
> 6.4). Para repetir esta verificación manual, el punto 3 pasa a ser una
> llamada a `worker.main._revisar_timeouts(publisher)` con
> `CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS` bajo, y al punto 4 se le suma
> comprobar que el mensaje llegó a la cola `Cubicacion.CubicacionRechazada` de
> NEXO. La lógica en sí (incluido el orden publicar-antes-de-marcar) ya está
> cubierta de forma automática en `tests/worker/test_vigilante_timeouts.py`.
5. Se reinició el `worker` (`docker compose start worker`) y se confirmó en
   sus logs que retomó el consumo normalmente.

Con esto, los 3 mecanismos de resiliencia diseñados en
`docs/DECISIONES_TECNICAS.md` #10 quedaron verificados contra infraestructura
real, no solo revisados por lectura de código.

### 2.5 Puente hacia NEXO (`GET /api/lineas-solicitud/{id}/cubicacion`)

⚠️ **Nota histórica (2026-09-02)**: esta sección y la 2.6 describen una
verificación contra la rama `cubicador` del repo NEXO, que **nunca se
mezcló a `QA`** — quedó descolgada antes de la reorganización del código
por feature. El endpoint no existió en ningún checkout real de NEXO hasta
que se portó (adaptado a la estructura actual) en esta fecha. Ver la
sección 2.9 para la verificación contra la implementación real, vigente.

El endpoint que NEXO debía exponer (ver `docs/DECISIONES_TECNICAS.md` #8) ya
se implementó ahí (rama `cubicador` del repo NEXO,
`LineasSolicitudController`/`ObtenerCubicacionLineaQuery`). Se verificó
ejecutando `infrastructure/clientes/nexo_client.py` de este repo, de verdad,
contra la API de NEXO levantada con `docker compose up -d` (SQL Server real
con datos sembrados):

1. `NexoLineaSolicitudClient.obtener(<id de una línea sembrada con ruta>)` →
   devolvió `LineaParaCubicacion` con `origen`/`destino` correctos y
   `posiciones=[]` (la línea sembrada no tenía `Dimensiones` capturadas —
   confirma el camino de lista vacía, no solo el camino feliz).
2. Se actualizó manualmente `Dimensiones` de esa línea a un JSON de una
   posición (`{"descripcion":"Bulto de prueba","cantidad":4,"largoCm":120,"anchoCm":100,"altoCm":90,"pesoKg":250}`)
   y se repitió la llamada → `PosicionCarga` con los 6 campos mapeados
   exactamente (confirma que el camelCase de NEXO y los nombres de campo que
   espera `nexo_client.py` coinciden sin ajustes).
3. `NexoLineaSolicitudClient.obtener(<guid inexistente>)` → devolvió `None`
   (NEXO respondió 404; este cliente solo mira el status code, no el cuerpo).
4. Se revirtió el dato de prueba (`Dimensiones = NULL`) para no dejar
   contaminado el seed de NEXO.

### 2.6 `crear_plan_desde_linea` de punta a punta (los dos stacks de Docker a la vez)

Con NEXO (SQL Server real, datos sembrados, línea con `Dimensiones` de
prueba) y Cubicador (Postgres + RabbitMQ) corriendo simultáneamente,
apuntando `NEXO_BASE_URL` del `.env` de Cubicador a
`http://host.docker.internal:8080` (el puerto publicado por el compose de
NEXO):

1. `POST /api/planes-cargue/desde-linea/{lineaSolicitudId}` en Cubicador →
   `202` con `SolicitudEncoladaDto`.
2. **Bug real encontrado**: el worker moría con `KeyError:
   'lineaSolicitudId'` en cada intento (3 veces, terminó en
   `cubicacion.dlq`). Causa: `api/routers/planes_cargue.py::crear_desde_linea`
   publica `{"lineaSolicitudId": ...}` como el `solicitud` del mensaje
   (`publicar_cubicacion_solicitada(linea_id, "crear_desde_linea",
   {"lineaSolicitudId": linea_id})`), que `RabbitMqEventosPublisher` anida
   bajo la clave `"solicitud"` del cuerpo publicado — pero
   `worker/main.py::_ejecutar_solicitud` leía `cuerpo["lineaSolicitudId"]`
   (nivel raíz) en vez de `cuerpo["solicitud"]["lineaSolicitudId"]`. El
   camino `optimizar` no lo tenía porque sí desempaca `cuerpo["solicitud"]`
   correctamente; nadie había probado `crear_desde_linea` disparado por HTTP
   real hasta este momento (la verificación de la sección 2.3 solo probó
   `optimizar`). Corregido en `worker/main.py`.
3. Repetido tras el fix: el worker llamó a
   `GET http://host.docker.internal:8080/api/lineas-solicitud/{id}/cubicacion`
   (confirmado en sus logs), creó el plan, y `GET
   /api/planes-cargue/por-linea/{lineaSolicitudId}` mostró la flota
   resultante (1 vehículo, 2/2 unidades cargadas). `GET /{id}/resultado`
   confirmó `estado: "optimizado"`, `puntaje: 76.4`, 2 colocaciones.
4. Se revirtieron los datos de prueba en NEXO (`Dimensiones = NULL`) y la
   variable `NEXO_BASE_URL` de Cubicador a su valor original
   (`http://host.docker.internal:5080`, para cuando NEXO corre con `dotnet
   run` en vez de en Docker).

### Cómo repetir esta verificación

```bash
cd .. && docker compose up -d --wait rabbit && cd cubicador   # rabbit compartido de NEXO primero
docker compose up -d --build
docker compose ps                       # db/api/worker deben quedar "healthy"/"Up"
curl http://localhost:8081/health       # puerto publicado al host desde CUBICADOR_HOST_PORT (default 8081)

# Para el flujo async, con el venv local activado (localhost:5672 es el
# rabbit COMPARTIDO de NEXO, publicado al host - ver docker-compose.yml raíz):
python -c "
import asyncio, json, aio_pika
async def main():
    conn = await aio_pika.connect_robust('amqp://<usuario>:<clave>@localhost:5672/')
    async with conn:
        canal = await conn.channel()
        ex = await canal.declare_exchange('cubicador.eventos', aio_pika.ExchangeType.TOPIC, durable=True)
        await ex.publish(
            aio_pika.Message(json.dumps({'tipo':'optimizar','planId':'<id>','correlationId':'<id>','solicitud':{}}).encode()),
            routing_key='cubicacion.solicitada',
        )
asyncio.run(main())
"
docker compose logs worker --tail 20   # deberia mostrar el mensaje procesado
```

### 2.7 Verificación de la unificación con el `rabbit` compartido de NEXO (2026-08-27)

Tras el cambio documentado en `docs/DECISIONES_TECNICAS.md` #9 (RabbitMQ dejó
de ser un contenedor propio de este `docker-compose.yml`), se corrieron los
dos jobs de CI (`unit-tests` y `docker-smoke`) exactamente como los correría
el pipeline real, contra el `rabbit` compartido de NEXO real en Docker — no
una simulación ni una lectura de código.

**Preparación** (equivalente a lo que hace `.github/workflows/cubicador-ci.yml`):

```bash
cd .. && docker compose up -d --wait --wait-timeout 60 rabbit   # rabbit compartido de NEXO
cd cubicador && docker compose up -d --build --wait --wait-timeout 90   # db + api + worker propios
```

Resultado: los 3 contenedores (`db`, `api`, `worker`) quedaron `healthy`/`Up`
sin caídas — conexión limpia con el broker ya tibio, sin necesitar ningún
reintento.

**`unit-tests` (68 pruebas, `pytest`):**

```
....................................................................     [100%]
68 passed in 2.59s
```

**`docker-smoke` (`scripts/ci_docker_smoke.py`, el mismo script que corre CI, con las variables `RABBITMQ_USER`/`RABBITMQ_PASSWORD` ya renombradas):**

```
[ci-smoke] esperando GET /health...
[ci-smoke] api saludable.
[ci-smoke] flujo REST + async: vehiculos -> plan -> item -> optimizar -> resultado
[ci-smoke] optimizado correctamente, puntaje=75.1
[ci-smoke] flujo de reintentos + Dead-Letter Queue (mensaje con tipo invalido)
[ci-smoke] mensaje invalido termino en cubicacion.dlq tras agotar reintentos, como se esperaba.
[ci-smoke] todo verificado correctamente.
```

Esto confirma, contra infraestructura real y no solo por lectura del
`docker-compose.yml`: el flujo `optimizar` completo (vehículos → plan → ítem
→ optimizar → resultado) publica al broker compartido, el `worker` lo
consume y persiste el resultado (`puntaje=75.1`, mismo valor conocido de
antes de la unificación — sin regresión de negocio); y el flujo de
reintentos + Dead-Letter Queue (3 reintentos con contador `x-intentos`, caída
a `cubicacion.dlq`) sigue funcionando igual sobre el broker compartido.

**Prueba adicional del peor caso de arranque** (no la corre CI, se hizo a
mano para confirmar el margen de `conectar_con_reintentos` subido a 30
intentos/2s — ver `docs/DECISIONES_TECNICAS.md` #9): se derribaron ambos
stacks, se levantó el `rabbit` de NEXO SIN esperar a que estuviera sano
(`docker compose up -d rabbit`, sin `--wait`) y a los pocos segundos, sin
ninguna pausa, se levantó el stack de Cubicador encima. Log real de `api`:

```
No se pudo conectar a RabbitMQ (intento 1/30): [Errno 111] Connection refused
No se pudo conectar a RabbitMQ (intento 2/30): [Errno 111] Connection refused
No se pudo conectar a RabbitMQ (intento 3/30): [Errno 111] Connection refused
No se pudo conectar a RabbitMQ (intento 4/30): [Errno 111] Connection refused
INFO:     Application startup complete.
```

Conectó en el intento 4 de 30 — margen real, no al filo (con el presupuesto
viejo de 5/2s este caso habría fallado igual que en la corrida de referencia
de más abajo). También se confirmó el caso límite: si el `rabbit` de NEXO
nunca se ha levantado (la red `nexo_internal` no existe todavía),
`docker compose up` de Cubicador falla de inmediato con un mensaje claro
(`network nexo_internal declared as external, but could not be found`), no
con un crash confuso — es un prerrequisito documentado en el README, no una
carrera.

**Corrida de referencia (documentada para comparar)**: la primera vez que se
probó esto, con el broker de NEXO recién construido desde cero (`docker
compose up -d --build --wait rabbit`, sin darle tiempo de calentar), la
conexión falló del todo — RabbitMQ tardó ~22s en abrir el puerto AMQP real
(`Time to start RabbitMQ: 21829 ms` en su propio log) pero Docker ya lo había
marcado `healthy` antes; con el presupuesto de reintentos aún en 5/2s
(~8-10s), tanto `api` como `worker` se cayeron con `AMQPConnectionError`.
Ese hallazgo fue justamente lo que motivó subir el margen a 30/2s.

**Limpieza**: `docker compose down` en ambos proyectos tras cada corrida —
sin residuos.

### 2.8 Alerta al equipo + prueba E2E por cola (2026-08-27)

Cierra los dos criterios de la HU que seguían pendientes tras el commit de
la unificación: "Reintentos + DLQ + alerta ante fallos repetidos del motor"
y "Prueba de extremo a extremo: trabajo publicado -> resultado recibido por
cola" (ambos verificados antes solo parcialmente — ver `README.md` sección
"Alerta al equipo + prueba E2E por cola").

**La alerta** (`infrastructure/alertas/correo.py`), verificada contra un
capturador SMTP real (`rnwood/smtp4dev`, nunca el corporativo) reforzando el
mismo escenario de la sección 2.4(b) — evidencia completa (logs del worker,
`rowCount: 6` del capturador) en `docs/DECISIONES_TECNICAS.md` #10.

**La prueba E2E por cola** (`scripts/ci_docker_smoke.py::
flujo_resultado_por_cola`), corrida como parte del smoke test completo:

```
[ci-smoke] esperando GET /health...
[ci-smoke] api saludable.
[ci-smoke] flujo REST + async: vehiculos -> plan -> item -> optimizar -> resultado
[ci-smoke] optimizado correctamente, puntaje=75.1
[ci-smoke] flujo E2E por cola: trabajo publicado -> resultado recibido en 'plan-de-cargue.generado'
[ci-smoke] evento recibido por cola: {'correlationId': 'c488cad9-...', 'planId': 'c488cad9-...', 'resultado': {'estado': 'optimizado', 'puntaje': 75.0, 'colocaciones': 2}}
[ci-smoke] flujo de reintentos + Dead-Letter Queue (mensaje con tipo invalido)
[ci-smoke] mensaje invalido termino en cubicacion.dlq tras agotar reintentos, como se esperaba.
[ci-smoke] todo verificado correctamente.
```

`pytest` completo tras estos cambios: **70/70** (68 anteriores + 2 nuevas en
`tests/worker/test_mover_a_dlq.py`, que cubren la conexión worker→alerta y
la degradación sin SMTP configurado, sin necesitar broker ni SMTP real).

### 2.9 Puente hacia NEXO real, implementado y verificado (2026-09-02)

A diferencia de 2.5/2.6 (rama `cubicador`, nunca mezclada), esta vez el
endpoint se construyó directamente en el NEXO real (rama que se mezcló con
`QA`): `LineasSolicitudCubicacionController` + `ObtenerCubicacionLineaQuery`
+ `LineaSolicitudCubicacionAdapter`, autenticación de servicio-a-servicio
dedicada (`CubicadorApiKeyAuthenticationHandler`, esquema `CubicadorApiKey`
separado de la sesión del portal, Bearer contra `Cubicador3D:InboundApiKey`).
`LineaSolicitud.Dimensiones` ya capturaba las posiciones desde antes
(`CapturarSolicitudCommand`/`PosicionCargaDto` en NEXO) — no hizo falta
ningún cambio de captura, solo el endpoint de lectura.

**Verificado con tests de integración HTTP reales** (`tests/CabinaFletes
.Api.Tests/Cubicacion/LineasSolicitudCubicacionControllerTests.cs`, contra
SQL Server real, no simulado): `200` con posiciones/ruta correctas, lista
vacía sin posiciones, `404` con línea inexistente, `401` sin Bearer, `401`
con Bearer incorrecto, y `401` si se intenta con la sesión del portal en vez
del Bearer de servicio (confirma que los dos esquemas de auth están
aislados). 361/361 tests de toda la solución NEXO pasaron.

**Verificado con los dos stacks reales en Docker a la vez** (NEXO completo:
`db`+`rabbit`+`api`; Cubicador completo: `db`+`api`+`worker`), con una línea
sembrada real (`A407C429-FADA-4F57-8A69-14FDC07A423A`, ruta Cartagena de
Indias → Soacha) a la que se le cargó `Dimensiones` de prueba (3 pallets
120×100×100cm, 500kg c/u):

1. `POST /api/planes-cargue/desde-linea/{lineaSolicitudId}` en Cubicador →
   `202` con `SolicitudEncoladaDto`.
2. **Bug real de infraestructura encontrado**: NEXO y Cubicador tienen cada
   uno un servicio llamado `api` en su propio `docker-compose.yml`. El
   worker de Cubicador está conectado a las dos redes Docker a la vez
   (`cubicador_default` y `nexo_internal`, compartida con NEXO) — al
   apuntar `NEXO_BASE_URL=http://api:8080`, el DNS embebido de Docker
   resolvió `api` hacia el **propio** `cubicador-api-1` (mismo nombre de
   servicio en la red `cubicador_default`), no hacia `nexo-api-1`. El
   worker "conectó" pero contra sí mismo y falló (`httpx.ConnectError`,
   3 reintentos, DLQ). Corregido apuntando al nombre de contenedor
   explícito: `NEXO_BASE_URL=http://nexo-api-1:8080`.
3. Repetido tras el fix: el worker llamó a
   `GET http://nexo-api-1:8080/api/lineas-solicitud/{id}/cubicacion` →
   confirmado `200 OK` en sus logs. Creó el plan con el vehículo sugerido
   por el cubicador rápido ("Turbo con estacas") y lo optimizó. `GET
   /api/planes-cargue/por-linea/{lineaSolicitudId}` mostró la flota
   resultante (1 vehículo, 3/3 unidades cargadas). `GET /{id}/resultado`
   confirmó `estado: "optimizado"`, `puntaje: 76.6`, 3 colocaciones con
   coordenadas X/Y/Z reales, todas `estable`, sin incidencias, y el
   `nombre` del plan con el origen/destino real tomado de NEXO ("Mesa -
   Cartagena de Indias (BOL) -> Soacha (CUN) - Vehiculo 1").

### Cómo repetir esta verificación (2.9)

```bash
# NEXO completo (raíz del repo)
docker compose up -d --build --wait

# Sembrar Dimensiones de prueba en una línea existente (ejemplo)
docker exec nexo-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P '<MSSQL_SA_PASSWORD>' \
  -d CabinaFletes -Q "UPDATE lineas_solicitud SET Dimensiones = '[{\"descripcion\":\"Pallet\",\"cantidad\":3,\"largoCm\":120,\"anchoCm\":100,\"altoCm\":100,\"pesoKg\":500}]' WHERE Id = '<lineaSolicitudId>'"

# cubicador/.env: NEXO_CALLBACK_API_KEY debe ser IDÉNTICA a
# Cubicador3D__InboundApiKey del .env de NEXO, y NEXO_BASE_URL debe usar el
# nombre de CONTENEDOR de NEXO (no el nombre de servicio "api" — ver el bug
# de arriba), ej. NEXO_BASE_URL=http://nexo-api-1:8080
cd cubicador && docker compose up -d --wait

curl -X POST -H "Authorization: Bearer <CUBICADOR_API_KEY>" \
  "http://localhost:8000/api/planes-cargue/desde-linea/<lineaSolicitudId>"
curl "http://localhost:8000/api/planes-cargue/por-linea/<lineaSolicitudId>" \
  -H "Authorization: Bearer <CUBICADOR_API_KEY>"
```

## 3. Qué falta cubrir (honesto, no se ha hecho todavía)

**Actualización**: el ítem que estaba aquí ("tests de integración
automatizados contra Docker") ya se cerró — ver `.github/workflows/ci.yml` +
`scripts/ci_docker_smoke.py`, que automatizan las secciones 2.2-2.4 en cada
push/PR. Lo que sigue sin automatizar, porque requiere infraestructura que
el runner de CI no tiene o una topología más compleja de simular:

- **El vigilante de timeout, de punta a punta** (sección 2.4, punto c):
  requiere detener el contenedor `worker` a mitad de un mensaje y llamar a
  `_revisar_timeouts` directo contra la base — sigue siendo manual. Desde
  S3-HU-06.1 su *lógica* sí está automatizada con dobles de prueba
  (`tests/worker/test_vigilante_timeouts.py`), incluido el caso de RabbitMQ
  caído; lo que queda manual es el recorrido real contra los dos contenedores.
- **`crear_plan_desde_linea` de punta a punta contra NEXO real** (secciones
  2.6 y 2.9): verificado manualmente contra la implementación real de NEXO
  (2.9) — funciona. Sigue sin automatizarse en CI: el runner no levanta el
  repo NEXO aparte, así que un futuro bug en este camino específico
  (`crear_desde_linea`) no lo atraparía el smoke test actual, que solo
  ejercita `optimizar`. Si se automatiza NEXO en CI más adelante (job aparte
  con su propio SQL Server), esta sección sería la candidata a migrar.
