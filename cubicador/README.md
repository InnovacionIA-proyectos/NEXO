# Cubicador

Microservicio propio y contenerizado que vive dentro de este mismo repo
(`NEXO/cubicador/`), como carpeta separada — no es parte de la solución
.NET (`CabinaFletes.Clean.sln`), ni comparte proceso ni base de datos con el
backend C#. Construido para S2-HU-072 ("portar el motor de cubicación a
Python, servicio contenerizado") y S2-HU-07.3 ("paridad automatizada con
los 26 casos dorados en CI"): trae **completo** el cubicador que
originalmente vivía en `cabinaFletes` (C#) — el cubicador rápido (evaluador
SICCE por criterios/veto) y el motor de bin-packing 3D (planes de cargue,
optimización, ajuste manual) — con su propia persistencia, y una suite que
prueba en cada build que el port sigue produciendo exactamente lo mismo que
el motor C# original.

No es un servicio de cómputo sin estado: es dueño de todo el dominio
Cubicación — vehículos, planes de cargue, ítems, colocaciones y parámetros —
con su propia base de datos (Postgres, contenedor propio). NEXO lo consume
vía REST (operaciones simples) y RabbitMQ (operaciones pesadas: optimizar,
crear plan desde línea).

**Nota sobre el puente hacia NEXO**: el endpoint que NEXO debe exponer para
"crear plan desde línea" (`GET /api/lineas-solicitud/{id}/cubicacion`) fue
construido en una fase anterior, en la rama `cubicador` de este mismo repo
(un módulo C# de Cubicación 3D que quedó descartado con el cambio de
dirección hacia este microservicio). **Esta rama no lo tiene** — si vas a
probar `crear_desde_linea`, necesitas ese endpoint corriendo desde la otra
rama, o usar `infrastructure/clientes/nexo_client.py` contra un NEXO que sí
lo tenga. Además, la decisión técnica de S2-HU-072 apunta a que la
comunicación NEXO↔Cubicador termine siendo solo por colas (RabbitMQ/
MassTransit, S2-HU-04, aparte) — este endpoint REST podría no ser el
mecanismo final.

Ver:
- [`docs/ARQUITECTURA.md`](./docs/ARQUITECTURA.md) — capas, estructura de carpetas, cómo se traduce Clean Architecture a Python.
- [`docs/DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) — registro de cada decisión tomada y por qué.
- [`docs/CONTRATOS.md`](./docs/CONTRATOS.md) — API REST, eventos de RabbitMQ, DTOs, errores, autenticación.
- [`docs/testDocumentation/`](./docs/testDocumentation/) — documentación completa de los tests: las 31 reglas de negocio portadas desde cabinaFletes y las pruebas de integración/Docker.

## Stack

- **Python 3.14**
- **FastAPI** — API síncrona (`/api/cubicacion`, `/api/planes-cargue`)
- **SQLAlchemy 2.0 (async)** — persistencia propia; `DATABASE_URL` apunta a SQLite en desarrollo o PostgreSQL en producción (mismo código, solo cambia la URL)
- **aio-pika (RabbitMQ)** — worker consumidor separado, para `optimizar` y `crear-desde-línea`
- **pytest** — 68 pruebas: 33 de reglas de negocio del motor (26 casos dorados de `MotorCubicacion3DTests.cs` + 5 de apilamiento multi-soporte) + **27 de paridad cross-motor** (los mismos 26 casos, comparados contra la salida real del motor C#, no contra un valor transcrito a mano) + 4 de humo end-to-end de la API + 2 de regresión del worker + 2 del registro de algoritmos intercambiables

## Estado actual (léelo antes de asumir que algo funciona)

**Verificado con pytest (sin Docker):**
- El port completo del motor (`domain/motor/*`) — las 31 reglas de
  `docs/testDocumentation/REGLAS_DE_NEGOCIO.md` pasan sin ajustes.
- El flujo completo API → Application → Infrastructure → Domain contra
  SQLite: crear vehículo (con siembra de los 9 presets) → crear plan → agregar
  ítem → optimizar → leer resultado → ajustar manualmente una colocación. Ver
  `tests/api/test_smoke.py`.
- Autenticación por Bearer estático rechazando llamadas sin la key.

**Verificado con Docker Compose real (Postgres + api + worker propios, conectados al RabbitMQ compartido de NEXO):**
- El mismo flujo de arriba, pero contra **PostgreSQL real** vía `docker
  compose up -d` (con healthcheck: `api`/`worker` esperan a que `db` esté
  `healthy` antes de arrancar — sin eso, arrancaban antes de que Postgres
  terminara su init y se caían). RabbitMQ ya no es un contenedor de este
  compose — ver `docs/DECISIONES_TECNICAS.md` #9.
- **El flujo asíncrono end-to-end, vía HTTP real**: `POST /{id}/optimizar`
  responde `202` de inmediato con el plan en `procesando`; el `worker` (otro
  contenedor) consume el mensaje de `cubicador.optimizar`, corre
  `optimizar_plan` contra Postgres y publica `plan-de-cargue.generado` —
  confirmado con `GET /resultado` que el plan quedó persistido como
  `optimizado` (puntaje `75.1`), no solo que el evento se emitió.
- **Reintentos + Dead-Letter Queue**: un mensaje que provoca un error
  inesperado se reintenta 3 veces (contador `x-intentos` en la cabecera,
  visible en los logs del worker) y, al agotarse, se publica en
  `cubicacion.dlq` para inspección manual — confirmado con la consola de
  administración de RabbitMQ (`http://localhost:15672`). Un error de negocio
  (`AppException`, ej. plan inexistente) en cambio **no reintenta**: se
  marca/reporta una sola vez, como corresponde (reintentar dará siempre el
  mismo resultado).
- **Vigilante de timeout**: un plan que quedó en `procesando` (ej. el worker
  se detiene a mitad de proceso) es marcado `error` automáticamente al pasar
  el límite configurado — confirmado forzando el escenario (worker detenido,
  plan en `procesando`, se corrió `marcar_error_por_timeout` con el límite
  cumplido) y viendo el plan pasar a `error` vía `GET /{id}`.
- Un bug real que solo apareció probando con datos reales: `metricas.puntaje`
  en `GET /resultado` siempre daba 0 porque `obtener_resultado` no
  reconstruía `resultado.puntaje` desde el plan persistido antes de pedirle
  las métricas al motor (el original en NEXO sí lo hace explícito:
  `resultado.Puntaje = plan.Puntaje`). Corregido en
  `application/plan_cargue/resultado.py`.
- Otro bug encontrado al reconstruir el stack: al servicio `api` le faltaba
  la variable `CUBICADOR_RABBITMQ_URL` en `docker-compose.yml` (solo la
  tenía `worker`) — sin ella, `api` intentaba conectarse a `localhost:5672`
  dentro de su propio contenedor y fallaba al arrancar. Corregido.

- `infrastructure/clientes/nexo_client.py` (el puente hacia `GET
  /api/lineas-solicitud/{id}/cubicacion`): el endpoint ya existe en NEXO
  (rama `cubicador`) y se verificó ejecutando este cliente real contra la
  API de NEXO en Docker Compose (SQL Server real, datos sembrados) — parseo
  correcto de `posiciones`/`origen`/`destino`, incluida una línea sin
  `Dimensiones` (lista vacía), y `None` en una línea inexistente (404). Ver
  docs/DECISIONES_TECNICAS.md #8.
- **`crear_plan_desde_linea` de punta a punta**, con ambos stacks de Docker
  reales corriendo a la vez (NEXO + Cubicador): `POST /desde-linea/{id}` →
  worker consume → llama a NEXO real → crea plan/flota → `GET
  /por-linea/{id}` y `GET /{id}/resultado` confirman `optimizado` con las 2
  unidades cargadas. Encontró un bug real: el worker leía
  `cuerpo["lineaSolicitudId"]` del mensaje, pero el router lo publica
  anidado en `cuerpo["solicitud"]["lineaSolicitudId"]` — `KeyError` en cada
  intento, terminaba en `cubicacion.dlq` a los 3 intentos. Corregido en
  `worker/main.py`.

**Cambios al traer este servicio de su repo standalone a `NEXO/cubicador/`**
(validados contra los criterios de cierre de S2-HU-072):
- Los 26 casos dorados de `cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs`
  ya estaban portados 1:1 (mismo nombre/orden) en `tests/domain/test_motor.py`
  — confirmado, no hubo que tocar nada del motor.
- `Dockerfile` no tenía `HEALTHCHECK` propio (solo existía a nivel de
  `docker-compose.yml`) — agregado.
- La fábrica de algoritmos (`domain/motor/motor.py`) era un `if/else` cerrado
  a `ffd`/`bfd`; ahora es un registro abierto
  (`domain/motor/optimizadores.py::registrar_optimizador`/
  `obtener_optimizador`/`algoritmos_disponibles`) para poder evaluar un motor
  alternativo en paralelo sin tocar `motor.py` — ver
  `tests/domain/test_optimizadores_registro.py`.
- **Otro bug de arranque en frío**: `api`/`worker` a veces se caían con
  `AMQPConnectionError: Connection refused` aun con `depends_on: condition:
  service_healthy` correcto — el healthcheck de RabbitMQ puede dar "sano" un
  instante antes de que el puerto AMQP acepte conexiones de verdad. Corregido
  con reintentos en la conexión inicial (`infrastructure/mensajeria/
  rabbitmq.py::conectar_con_reintentos`, 5 intentos / 2s). Reproducido a
  propósito (`down -v` + `up`) para confirmar que ya no tumba el contenedor.
- **Swagger no mandaba el Bearer**: `verificar_api_key` usaba un `Header()`
  crudo llamado `authorization`; sin un esquema de seguridad real, Swagger UI
  no lo exponía de forma confiable (confirmado con un usuario real: el campo
  se llenaba en la UI pero el request nunca llevaba el header, visto en el
  Network tab). Cambiado a `fastapi.security.HTTPBearer` — ahora Swagger
  muestra el botón "Authorize" real.

**Paridad automatizada contra el motor C# real (S2-HU-07.3, hotspot H5):**
- `herramientas/generador_casos_dorados/` — harness .NET que referencia el
  `.csproj` real de `cabinaFletes` (sin modificarlo) y corre los 26 casos de
  `MotorCubicacion3DTests.cs` contra el motor C# de verdad, grabando su
  salida en `tests/paridad/fixtures_dorados/*.json`.
- `tests/paridad/test_paridad_casos_dorados.py` — compara el motor Python
  contra esos 26 JSON en cada `pytest` (CI incluido). **Resultado: 26/26
  coinciden exacto**, sin haber tenido que ajustar el port. Falla el build
  con el esperado/obtenido exactos si algún caso deja de coincidir — ver
  `docs/DECISIONES_TECNICAS.md` #3 (la decisión de no construir esto se
  revirtió cuando la HU lo pidió explícitamente).

## CI

`.github/workflows/cubicador-ci.yml` (raíz del repo NEXO, con
`paths: ['cubicador/**']` para no disparar en cambios del backend C#), dos
jobs en cada push/PR a `main`:

- **`unit-tests`** — instala dependencias y corre `pytest` (68 pruebas contra
  SQLite en memoria, sin infraestructura externa).
- **`docker-smoke`** — levanta el stack real (`docker compose up -d --build
  --wait`: Postgres + RabbitMQ + api + worker) y corre
  `scripts/ci_docker_smoke.py`, que automatiza el flujo de las secciones
  2.2-2.4 de `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md`: `POST
  /optimizar` → 202 → espera a que el worker lo procese → `optimizado` con
  `metricas.puntaje == plan.puntaje` (regresión del bug real ya corregido),
  y un mensaje con `tipo` inválido → 3 reintentos → cae en `cubicacion.dlq`.
  Falla el job (exit ≠ 0) si el flujo real no llega al resultado esperado, no
  solo si el código no compila/importa.

**Todavía sin verificar automáticamente:**
- El vigilante de timeout (`docs/DECISIONES_TECNICAS.md` #10): requiere
  detener el contenedor `worker` a mitad de un mensaje, lo que acopla el
  script a la topología interna del compose de una forma que no valía la
  pena para CI — sigue siendo procedimiento manual (ver
  `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.4).
- `crear_plan_desde_linea` de punta a punta contra un NEXO real: el job de
  CI no levanta el repo NEXO — sigue siendo verificación manual con ambos
  stacks corriendo a la vez (sección 2.6 del mismo documento).

## Puesta en marcha

**Local, sin Docker (desarrollo rápido, SQLite):**

```bash
python -m venv .venv && source .venv/Scripts/activate   # o .venv/bin/activate en Linux/Mac
pip install -r requirements.txt
export CUBICADOR_API_KEY=dev-key   # o el equivalente en PowerShell/cmd
uvicorn api.main:app --reload
curl http://localhost:8000/health
```

**Con Docker Compose (api + worker + Postgres propios, RabbitMQ compartido de NEXO):**

Requiere el `rabbit` de NEXO ya corriendo (ver `docs/DECISIONES_TECNICAS.md`
#9) — desde la raíz del repo:

```bash
cd ..   # raíz de NEXO
cp .env.example .env   # si no existe ya; completar RABBITMQ_PASSWORD como mínimo
docker compose up -d --wait rabbit   # solo el broker, no todo NEXO (rabbit no depende de db/api)
cd cubicador
```

Y luego el stack propio de Cubicador:

```bash
cp .env.example .env   # completar POSTGRES_PASSWORD, RABBITMQ_USER/PASSWORD (= los del .env raíz de NEXO), CUBICADOR_API_KEY, NEXO_CALLBACK_API_KEY, NEXO_BASE_URL
docker compose up -d --build --wait
curl http://localhost:8000/health
```

Swagger interactivo en `http://localhost:8000/docs` — botón "Authorize"
arriba a la derecha, pega ahí tu `CUBICADOR_API_KEY` (sin la palabra
"Bearer", Swagger la agrega sola) y queda aplicado a todos los endpoints.

**Correr las pruebas:**

```bash
pip install -r requirements.txt   # incluye pytest/pytest-asyncio
pytest
```

## Cómo se conecta con NEXO

- **NEXO → este servicio**: REST síncrono para todo salvo optimizar/crear-desde-línea, que van por RabbitMQ (202 inmediato, resultado por polling — ver arriba). Autenticación: `Authorization: Bearer <CUBICADOR_API_KEY>`.
- **Este servicio → NEXO**: un único endpoint de solo lectura que NEXO debe exponer (`GET /api/lineas-solicitud/{id}/cubicacion`) para el puente Mesa de Costeo → Cubicador. Autenticación: `Authorization: Bearer <NEXO_CALLBACK_API_KEY>`. **Ese endpoint solo existe en la rama `cubicador` de este repo** (ver nota al inicio de este README) — en esta rama, `crear_desde_linea` no tiene con qué hablar hasta que se decida el mecanismo final (posiblemente colas, no REST — ver S2-HU-04).
- Aunque ahora viven en el mismo repo, siguen sin compartir red Docker ni base de datos — cada uno tiene su propio `docker-compose.yml` y todo cruce es por API o mensaje, nunca por tabla compartida.

## Alcance explícitamente fuera de esta primera versión

- **Actualización**: la paridad automatizada cross-motor que decía "no se
  construye" ya se construyó — ver la sección "Estado actual" más arriba y
  `tests/paridad/`. `docs/DECISIONES_TECNICAS.md` #3 documenta por qué se
  revirtió esa decisión (S2-HU-07.3 la pidió explícitamente como criterio de
  liberación).
- `POST /api/cubicacion/costear` (cubicar + estimar flete) — no se migró en
  NEXO tampoco: depende de un método de Biblioteca (`EstimarFleteAsync`) que
  nunca se migró a su puerto. Gap pre-existente, ajeno al Cubicador.
- Frontend del Cubicador 3D — sigue pendiente aparte, en ningún repo todavía.
- "Comunicación solo por colas" (decisión técnica de S2-HU-072) — hoy el
  servicio sigue siendo REST para todo lo que no es `optimizar`/
  `crear-desde-línea`. Ver nota al inicio de este README.
