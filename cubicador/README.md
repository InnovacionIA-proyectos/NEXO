# Cubicador

Microservicio en Python que vive en este mismo repositorio (`cubicador/`), como carpeta separada: no es parte de la solución
.NET (`Nexo.Clean.sln`) ni comparte proceso con el backend de NEXO. Es dueño del dominio de cubicación y tiene su propia base
de datos en SQL Server.

Tiene dos motores:

- **Selección de flota** (`domain/motor/seleccion_flota.py`). Lee el catálogo de la base y responde qué vehículos hacen falta
  para una carga, en qué cantidad y con qué mercancía en cada uno. La flota puede ser mixta. Se expone en
  `POST /recomendaciones`, que además guarda cada corrida. Reemplazó al cubicador rápido heredado de `cabinaFletes`, que
  comparaba contra nueve arquetipos fijos y decidía solo por volumen; ese módulo y su endpoint `POST /api/cubicacion` ya no
  existen.
- **Bin-packing 3D** (`domain/motor/motor.py`): planes de cargue, optimización y ajuste manual. Es un port del motor C# de
  `cabinaFletes`, con una suite de paridad que compara en cada build su salida contra la del motor original en los 26 casos
  dorados.

NEXO se comunica con el servicio por RabbitMQ para todo lo del plan de cargue y por HTTP para las recomendaciones y la
administración de catálogos. El servicio solo llama a NEXO para leer las posiciones de una línea de solicitud. Ver
«Cómo se conecta con NEXO» más abajo.

Documentación:

- [`CONTRATO.md`](./CONTRATO.md): API REST, mensajes de RabbitMQ, DTOs, errores y autenticación.
- [`docs/ARQUITECTURA.md`](./docs/ARQUITECTURA.md): capas, estructura de carpetas y cómo se traduce la arquitectura de NEXO a
  Python.
- [`docs/DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md): registro de decisiones y su porqué.
- [`docs/HOJA-DE-RUTA-CUBICADOR.md`](./docs/HOJA-DE-RUTA-CUBICADOR.md): especificación funcional del contrato de
  recomendaciones y de los catálogos.
- [`docs/DEUDA_DIFERIDA_3D.md`](./docs/DEUDA_DIFERIDA_3D.md): lo que quedó pendiente por no tocar el motor 3D, con su costo.
- [`database/README.md`](./database/README.md): la base de datos, cómo se crea y cómo se conecta la aplicación.
- [`docs/testDocumentation/`](./docs/testDocumentation/): las 31 reglas de negocio portadas de `cabinaFletes` y las pruebas de
  integración con Docker.

## Stack

- **Python 3.14**
- **FastAPI**: la API (`/recomendaciones`, los catálogos, `/api/planes-cargue`).
- **SQLAlchemy 2.0 (async)**: persistencia. `DATABASE_URL` apunta a SQL Server en QA y producción, y a SQLite en las pruebas y
  en el arranque local sin Docker. En SQL Server el esquema lo gobierna `database/esquema/ESQUEMA_CUBICADOR.sql`, que se aplica
  a mano; la aplicación no crea ni altera tablas ahí.
- **aio-pika (RabbitMQ)**: el worker que consume los comandos de NEXO y publica los eventos.
- **pytest**: la suite corre contra SQLite en memoria. Cubre las reglas de negocio del motor 3D y su paridad con el motor C#, el
  motor de selección de flota, el contrato de `/recomendaciones` y de los catálogos, el worker (los comandos por cola, los
  reintentos, la cola de error y el vigilante de timeouts), el puente desde línea, los eventos que consume NEXO y la IA
  (explicación y modo sombra).

## Estado actual

**Verificado con pytest, sin Docker:**

- El port del motor 3D (`domain/motor/*`): las 31 reglas de
  [`REGLAS_DE_NEGOCIO.md`](./docs/testDocumentation/REGLAS_DE_NEGOCIO.md) pasan sin ajustes.
- El flujo API → aplicación → infraestructura → dominio contra SQLite: crear vehículo (con la siembra de los 10 presets), crear
  plan, agregar ítem, optimizar, leer el resultado y ajustar una colocación (`tests/api/test_smoke.py`).
- La autenticación por llave, incluida la llave por sistema de `/recomendaciones`.
- El motor de selección de flota: el circuito completo adaptador → puerto → caso de uso → motor contra SQLite, y el algoritmo
  con un catálogo calcado del real (`tests/domain/test_seleccion_flota.py`, `tests/domain/test_dblf.py`,
  `tests/application/test_recomendar_flota.py`).

**Verificado contra el catálogo real de QA:**

- Las 31 mercancías del catálogo resuelven una por una. Las dos cuya carrocería principal no tiene vehículo asignado
  (`CARGA_CONTENERIZADA` y `VIDRIO_PLANO`) caen a su carrocería de respaldo y vuelven marcadas como condicionadas.
- Los siete escenarios del contrato: flota mixta, carga heterogénea en un vehículo, reparto por incompatibilidad, condicionada,
  refrigerada, extradimensionado y `SUPERA_K_MAX`.
- El mapeo de las 10 tablas del catálogo y de las recomendaciones cuadra columna por columna contra `sys.columns`
  (`scripts/verificar_mapeo_catalogo.py`).
- El empaque de DBLF iguala la rejilla óptima en 20 combinaciones de vehículo y bulto (`scripts/medir_empaque_dblf.py`).

**Verificado con Docker Compose (SQL Server, api y worker propios, conectados al RabbitMQ compartido de NEXO):**

- El mismo flujo de arriba contra SQL Server real. `api` y `worker` esperan a que `db` esté sano y a que `db-init` haya creado
  la base y las tablas.
- **El flujo asíncrono de punta a punta:** `POST /api/planes-cargue/{id}/optimizar` responde `202` con el plan en `procesando`;
  el worker consume el mensaje de la cola `cubicador.optimizar`, optimiza contra la base y publica `plan-de-cargue.generado`. Se
  confirmó con `GET /resultado` que el plan quedó persistido como `optimizado`, no solo que el evento salió.
- **Reintentos y cola de error:** un mensaje que provoca un error inesperado se intenta tres veces en total (contador
  `x-intentos` en la cabecera) y después se publica en `cubicacion.dlq`, con alerta por correo. Un error de negocio
  (`AppException`, por ejemplo un plan inexistente) no se reintenta: se reporta una sola vez, porque reintentar daría el mismo
  resultado.
- **Vigilante de timeouts:** un plan que se queda en `procesando` (por ejemplo, porque el worker se detuvo a mitad de proceso)
  recibe un `cubicacion.rechazada` con `TIMEOUT_PROCESAMIENTO` y queda en `error` al pasar el límite
  (`CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS`). El rechazo se publica antes de marcar el plan, para que un broker caído se reintente
  en el ciclo siguiente en lugar de perderse ([`CONTRATO.md`](./CONTRATO.md), sección 6.4).
- **El puente desde línea** (`crear_desde_linea`), con los dos stacks de Docker a la vez: `POST /desde-linea/{id}` → worker →
  `GET /api/lineas-solicitud/{id}/cubicacion` en NEXO → flota armada y optimizada, confirmada con `GET /por-linea/{id}` y
  `GET /{id}/resultado` ([`PRUEBAS_DE_INTEGRACION.md`](./docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md), secciones 2.6 y
  2.9).

Bugs que solo aparecieron probando con el stack real, ya corregidos:

- `metricas.puntaje` de `GET /resultado` siempre daba 0, porque `obtener_resultado` no reconstruía el puntaje desde el plan
  persistido (`application/plan_cargue/resultado.py`).
- Al servicio `api` le faltaba `CUBICADOR_RABBITMQ_URL` en `docker-compose.yml` e intentaba conectarse a `localhost:5672`
  dentro de su propio contenedor.
- El worker leía `lineaSolicitudId` en la raíz del mensaje y no dentro de `solicitud`: cada `crear_desde_linea` terminaba en la
  cola de error (`worker/main.py`).
- `api` y `worker` se caían a veces al arrancar en frío con `AMQPConnectionError`, porque el healthcheck de RabbitMQ puede dar
  sano un instante antes de que el puerto AMQP acepte conexiones. La conexión inicial reintenta 30 veces cada 2 segundos
  (`infrastructure/mensajeria/rabbitmq.py`, `conectar_con_reintentos`), margen que también cubre levantar este stack antes que
  el `rabbit` de NEXO.
- Swagger no mandaba el Bearer: la autenticación usaba un `Header()` crudo. Ahora usa `fastapi.security.HTTPBearer` y Swagger
  muestra el botón *Authorize*.

**Paridad automatizada contra el motor C#:**

- `herramientas/generador_casos_dorados/` es un harness .NET que referencia el `.csproj` real de `cabinaFletes` (sin
  modificarlo), corre los 26 casos de `MotorCubicacion3DTests.cs` contra el motor C# y graba su salida en
  `tests/paridad/fixtures_dorados/*.json`.
- `tests/paridad/test_paridad_casos_dorados.py` compara el motor Python contra esos 26 archivos en cada `pytest`, CI incluido.
  Coinciden los 26 sin haber ajustado el port. Si un caso deja de coincidir, el build falla con el valor esperado y el obtenido
  ([`DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) #3).

**RabbitMQ compartido con NEXO.** Este servicio no trae su propio RabbitMQ: `api` y `worker` se conectan al `rabbit` del
`docker-compose.yml` de la raíz, enganchándose a su red Docker (`nexo_internal`) por nombre externo
([`DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) #9). Los nombres de exchange y colas (`cubicador.eventos`,
`cubicador.optimizar`, `cubicacion.dlq`) no chocan con la topología de MassTransit de NEXO
([`TOPOLOGIA.md`](../docs/mensajeria/TOPOLOGIA.md)).

## CI

`.github/workflows/_cubicador-pruebas.yml` (en la raíz del repositorio) es un workflow reutilizable. Lo llaman
`ci-cd-cubicador-api.yml` y `ci-cd-cubicador-worker.yml` en cada push y pull request a `main` y `DEPLOY` que toque
`cubicador/**` (sin contar `cubicador/docs/**` ni los `.md`), el `docker-compose.yml` de la raíz o `build/Dockerfile.rabbit`.
Tiene dos jobs:

- **`unit-tests`**: instala dependencias y corre `pytest -v` contra SQLite en memoria, sin infraestructura externa.
- **`docker-smoke`**: levanta el `rabbit` compartido de NEXO y luego el stack propio (SQL Server, `db-init`, api y worker).
  Después:
  - corre `tests/infrastructure/test_plan_cargue_repo_contra_sqlserver.py` dentro del contenedor `api`, contra el SQL Server del
    stack: SQLite acepta SQL que SQL Server rechaza, y esta es la única prueba que lo detecta;
  - corre `scripts/ci_docker_smoke.py`, que automatiza las secciones 2.2 a 2.4 de
    [`PRUEBAS_DE_INTEGRACION.md`](./docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md): `POST /optimizar` → `202` → el worker lo
    procesa → `optimizado` con `metricas.puntaje == plan.puntaje`; el resultado recibido por la cola
    (`plan-de-cargue.generado`), no solo por HTTP; y un mensaje con `tipo` inválido que agota los intentos y cae en
    `cubicacion.dlq`.

  El job falla si el flujo real no llega al resultado esperado, no solo si el código no compila.

**Todavía sin verificar automáticamente:**

- El vigilante de timeouts: exige detener el contenedor `worker` a mitad de un mensaje, lo que ataría el script a la topología
  interna del compose. Sigue siendo un procedimiento manual
  ([`PRUEBAS_DE_INTEGRACION.md`](./docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md), sección 2.4).
- `crear_desde_linea` contra un NEXO real: se verificó a mano, pero el job de CI no levanta NEXO.

## Puesta en marcha

**Con Docker Compose (api, worker y SQL Server propios, RabbitMQ compartido de NEXO).** Es la forma recomendada.

Primero el `rabbit` de NEXO, desde la raíz del repositorio:

```bash
cp .env.example .env                 # si no existe; como mínimo, RABBITMQ_PASSWORD
docker compose up -d --wait rabbit   # solo el broker, no todo NEXO
```

Luego el stack del Cubicador, desde `cubicador/`:

```bash
cp .env.example .env
docker compose up -d --build --wait
curl http://localhost:8081/health
```

El compose exige en `.env`: `MSSQL_SA_PASSWORD`, `RABBITMQ_USER` y `RABBITMQ_PASSWORD` (los mismos del `.env` de la raíz),
`CUBICADOR_API_KEY`, `NEXO_BASE_URL` y `NEXO_CALLBACK_API_KEY`. `CUBICADOR_API_KEY_NEXO` y `CUBICADOR_API_KEY_COTIFLY` son
opcionales para levantar el stack, pero sin ellas `/recomendaciones` responde `401` a todo.

- `db-init` crea la base `dbCubicador_Pruebas`, el esquema (`CUBICADOR_DB_ESQUEMA`, `dbo` por defecto) y las 16 tablas de
  `database/esquema/ESQUEMA_CUBICADOR.sql`, si no existen. El catálogo queda vacío: hasta sembrarlo, `/recomendaciones` no
  encuentra mercancías. Para cargar un catálogo mínimo de desarrollo:

  ```bash
  docker compose exec api python scripts/preparar_catalogo_local.py
  ```

  El script se niega a correr contra un host que no sea local. El catálogo inicial completo se carga con
  `database/scripts/ejecutar_schema_y_seeders.py` ([`database/README.md`](./database/README.md)).
- La API escucha en el puerto 8000 dentro del contenedor y se publica en el host en el **8081** (`CUBICADOR_HOST_PORT`), para no
  chocar con el 8080 del backend de NEXO. SQL Server queda en el host en el 1434.
- Swagger en `http://localhost:8081/docs`: en *Authorize* se pega la llave sin la palabra `Bearer` y queda aplicada a todos los
  endpoints. El contrato OpenAPI exportado está en [`docs/openapi.json`](./docs/openapi.json).

**Local, sin Docker (SQLite):**

```bash
python -m venv .venv && source .venv/Scripts/activate   # .venv/bin/activate en Linux o Mac
pip install -r requirements.txt
export CUBICADOR_API_KEY=dev-key
export CUBICADOR_RABBITMQ_URL=amqp://<usuario>:<clave>@localhost:5672/
uvicorn api.main:app --reload
curl http://localhost:8000/health
```

- La API necesita un RabbitMQ al arrancar: el `rabbit` de NEXO publica el 5672 en el host. Sin broker, deja de intentar tras
  unos 60 segundos y no arranca.
- Sin `DATABASE_URL` usa `./cubicador.db` (SQLite) y crea solo las tablas de `infrastructure/persistence/modelos.py` (motor 3D,
  medidas estándar e `iaSombra`). Las del catálogo y las recomendaciones no se crean, así que en este modo funcionan
  `/api/planes-cargue` y `/medidas-estandar`, pero no `/recomendaciones` ni los CRUD de catálogo.

**Variables de entorno.** La lista completa, comentada, está en [`.env.example`](./.env.example). En Dokploy, las aplicaciones
`cubicador-api` y `cubicador-worker` se configuran con la plantilla de [`.env.dokploy`](../.env.dokploy) de la raíz. Las
principales:

| Variable | Para qué |
|---|---|
| `DATABASE_URL`, `CUBICADOR_DB_ESQUEMA` | Base y esquema ([`database/README.md`](./database/README.md)). Dentro del compose, `DATABASE_URL` la arma el propio compose. |
| `CUBICADOR_RABBITMQ_URL` | Broker. El compose la arma con `RABBITMQ_USER` y `RABBITMQ_PASSWORD`. |
| `CUBICADOR_API_KEY`, `CUBICADOR_API_KEY_NEXO`, `CUBICADOR_API_KEY_COTIFLY` | Llaves de entrada ([`CONTRATO.md`](./CONTRATO.md), sección 5). |
| `NEXO_BASE_URL`, `NEXO_CALLBACK_API_KEY` | Llamada a NEXO del puente desde línea. La llave es igual a `Cubicador3D__InboundApiKey` de NEXO. |
| `IA_SOMBRA_HABILITADA`, `ANTHROPIC_API_KEY`, `ANTHROPIC_*`, `IA_SOMBRA_*` | Explicación de la recomendación y modo sombra. Solo los usa `api`; nacen apagados. |
| `CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS`, `CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS` | Vigilante de timeouts. Solo los usa `worker`. |
| `ALERTAS_SMTP_*`, `ALERTAS_EQUIPO_TECNICO` | Correo al caer un mensaje en `cubicacion.dlq`. Solo los usa `worker`; opcionales. |

**Correr las pruebas**, desde `cubicador/`:

```bash
pip install -r requirements.txt   # incluye pytest y pytest-asyncio
pytest
```

## Cómo se conecta con NEXO

- **NEXO → este servicio, por cola.** Toda escritura del plan de cargue y de los vehículos del motor 3D llega como un comando en
  `cubicacion.solicitada` (crear, optimizar o borrar planes, ítems, colocaciones y vehículos). El worker responde con eventos, y
  NEXO sirve las lecturas desde su propia proyección ([`CUBICACION.md`](../docs/mensajeria/CUBICACION.md)).
- **NEXO → este servicio, por HTTP.** `POST` y `GET /recomendaciones` y `GET /recomendaciones/explicacion`, con la llave de NEXO
  (`Cubicador3D:RecomendacionesApiKey` = `CUBICADOR_API_KEY_NEXO`), y el reenvío de los CRUD de catálogo que hace el portal, con
  la llave genérica (`Cubicador3D:OutboundApiKey` = `CUBICADOR_API_KEY`).
- **Este servicio → NEXO.** Un único endpoint de solo lectura, `GET /api/lineas-solicitud/{id}/cubicacion`, para el puente Mesa
  de Costeo → plan 3D (`crear_desde_linea`). Usa `NEXO_CALLBACK_API_KEY`, que debe ser igual a `Cubicador3D__InboundApiKey` en
  NEXO ([`CONTRATO.md`](./CONTRATO.md), sección 7).
- Ningún navegador llama a este servicio: no tiene CORS.
- No comparten base de datos: cada uno tiene la suya y todo cruce es por RabbitMQ o por las llamadas HTTP de arriba.

## Fuera de alcance

- `POST /api/cubicacion/costear` (cubicar y estimar flete): no se migró en NEXO, porque depende de un método de Biblioteca
  (`EstimarFleteAsync`) que nunca se migró a su puerto. Es un hueco previo, ajeno al Cubicador.
- Unificar los dos catálogos de vehículos (el de la selección de flota y el del motor 3D): hoy los cruza un diccionario de
  compatibilidad ([`DEUDA_DIFERIDA_3D.md`](./docs/DEUDA_DIFERIDA_3D.md)).
