# Cubicador

Microservicio propio y contenerizado que vive dentro de este mismo repo
(`NEXO/cubicador/`), como carpeta separada — no es parte de la solución
.NET (`Nexo.Clean.sln`), ni comparte proceso ni base de datos con el
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

**Nota sobre el puente hacia NEXO** (actualizado 2026-09-02): el endpoint que
NEXO expone para "crear plan desde línea" (`GET /api/lineas-solicitud/{id}
/cubicacion`) ya está implementado y verificado en el NEXO real
(`LineasSolicitudCubicacionController`, ver `PENDIENTES.md` y
`docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.9). Hubo un
primer intento en una rama vieja del repo de NEXO (`cubicador`) que nunca se
mezcló y quedó descolgada — ese diseño es el que se portó a la
implementación actual. La comunicación NEXO→Cubicador sigue siendo por
colas (RabbitMQ/MassTransit) para las operaciones pesadas; este es el único
endpoint REST, en sentido inverso (Cubicador→NEXO), y es deliberadamente
así — es una consulta de solo lectura puntual, no encaja en el patrón de
eventos.

Ver:
- [`CONTRATO.md`](./CONTRATO.md) — API REST, eventos de RabbitMQ, DTOs, errores, autenticación: todo lo necesario para conectar backend o frontend contra este servicio.
- [`docs/ARQUITECTURA.md`](./docs/ARQUITECTURA.md) — capas, estructura de carpetas, cómo se traduce Clean Architecture a Python.
- [`docs/DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) — registro de cada decisión tomada y por qué.
- [`docs/testDocumentation/`](./docs/testDocumentation/) — documentación completa de los tests: las 31 reglas de negocio portadas desde cabinaFletes y las pruebas de integración/Docker.

## Stack

- **Python 3.14**
- **FastAPI** — API síncrona (`/api/cubicacion`, `/api/planes-cargue`)
- **SQLAlchemy 2.0 (async)** — persistencia propia; `DATABASE_URL` apunta a SQLite en desarrollo o PostgreSQL en producción (mismo código, solo cambia la URL)
- **aio-pika (RabbitMQ)** — worker consumidor separado, para `optimizar` y `crear-desde-línea`
- **pytest** — 103 pruebas: 33 de reglas de negocio del motor (28 casos dorados de `MotorCubicacion3DTests.cs` + 5 de apilamiento multi-soporte) + **27 de paridad cross-motor** (los mismos casos, comparados contra la salida real del motor C#, no contra un valor transcrito a mano) + 4 de humo end-to-end de la API + 12 de regresión del worker (los 9 comandos por cola + el puente desde línea) + 4 del vigilante de timeouts + 2 del registro de algoritmos intercambiables + 2 de la alerta al equipo cuando un mensaje cae a `cubicacion.dlq` + **19 guardas de los 3 bugs de QA** (S3-HU-10: mensaje malformado que atascaba el worker, ítem sin SKU, borrado de plan optimizado)

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
  se detiene a mitad de proceso) recibe un `cubicacion.rechazada` con
  `codigo: "TIMEOUT_PROCESAMIENTO"` y queda marcado `error` al pasar el límite
  configurado (`CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS`) — confirmado forzando el
  escenario (worker detenido, plan en `procesando`, se corrió
  `_revisar_timeouts` con el límite cumplido) y viendo el plan pasar a `error`
  vía `GET /{id}`. El rechazo se publica **antes** de marcar el plan, para que
  un broker caído se reintente en el ciclo siguiente en lugar de perderse — ver
  CONTRATO.md 6.4.
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
  rabbitmq.py::conectar_con_reintentos`, entonces 5 intentos / 2s — subido
  después a 30/2s, ver el bullet de unificación con NEXO más abajo).
  Reproducido a propósito (`down -v` + `up`) para confirmar que ya no tumba
  el contenedor.
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

**Unificación de RabbitMQ con el broker compartido de NEXO (2026-08-27):**
- Este servicio ya NO trae su propio contenedor RabbitMQ — `api`/`worker` se
  conectan al `rabbit` del `docker-compose.yml` raíz de NEXO (issue
  #13/S2-HU-041) enganchándose a su red Docker por nombre externo. La base de
  datos (Postgres) sigue siendo propia. Ver `docs/DECISIONES_TECNICAS.md` #9
  (documenta por qué se revirtió la decisión original de un broker propio) y
  la sección "Puesta en marcha" más abajo (ahora requiere levantar el
  `rabbit` de NEXO antes que este stack).
- Al no poder haber `depends_on` entre proyectos Compose distintos, se subió
  el margen de `conectar_con_reintentos()` de 5 intentos/2s a **30/2s** —
  verificado forzando el peor caso (arrancar este stack antes que el `rabbit`
  de NEXO): conectó en el intento 4 de 30 una vez el broker quedó listo.
- Verificado de punta a punta: mensaje publicado → consumido → confirmado vía
  el broker compartido (RabbitMQ: `publish=1, ack=1`), sin colisión de
  nombres con la topología MassTransit de NEXO (`cubicador.eventos`/
  `cubicador.optimizar`/`cubicacion.dlq` no se cruzan con `CostearLinea`,
  `AutoCosteoSolicitado`, etc. — ver `docs/mensajeria/TOPOLOGIA.md` en la
  raíz de NEXO).

**Alerta al equipo + prueba E2E por cola (2026-08-27, mismo día):**
- `infrastructure/alertas/correo.py::alertar_mensaje_en_dlq` — al caer un
  mensaje en `cubicacion.dlq` tras agotar los 3 reintentos, además del log ya
  existente, ahora se envía un correo al equipo técnico (mismo mecanismo que
  la alerta equivalente de NEXO, #13). 2 pruebas nuevas
  (`tests/worker/test_mover_a_dlq.py`) cubren la conexión worker→alerta y que
  degrada sin romper el worker si falta configuración de SMTP.
- `scripts/ci_docker_smoke.py::flujo_resultado_por_cola` — el criterio
  "trabajo publicado → resultado recibido por cola" (antes solo verificado a
  mano, sección 2.3) ahora corre en cada push/PR: se suscribe a
  `plan-de-cargue.generado` con una cola de prueba propia ANTES de disparar
  `optimizar`, y confirma que el evento real llega por ahí, no solo que el
  efecto quedó en la base de datos.

**Endpoint de NEXO para `crear_desde_linea` — implementado y verificado
(2026-09-02):** el bullet de arriba (`infrastructure/clientes/nexo_client.py`
verificado "contra la API de NEXO... rama `cubicador`") describía una
verificación contra una rama que **nunca se mezcló a `QA`** — quedó
descolgada antes de la reorganización del código por feature. `GET
/api/lineas-solicitud/{id}/cubicacion` no existía en ningún checkout real de
NEXO hasta ahora. Se portó el diseño de esa rama vieja (adaptado a la
estructura actual: CQRS por feature, no por capa) al NEXO real:
`LineasSolicitudCubicacionController` + `ObtenerCubicacionLineaQuery` +
`LineaSolicitudCubicacionAdapter`, con autenticación de servicio-a-servicio
dedicada (`CubicadorApiKeyAuthenticationHandler`, esquema `CubicadorApiKey`
separado de la sesión del portal). `LineaSolicitud.Dimensiones` ya capturaba
las posiciones desde antes — no hizo falta ningún cambio de captura, solo el
endpoint. Verificado con 6 tests de integración HTTP contra SQL Server real
(200/404/401 en sus tres variantes) y con una corrida real de punta a punta
cruzando los dos stacks en Docker: `POST /desde-linea/{id}` → RabbitMQ →
worker → `GET .../cubicacion` (200) → plan armado con el vehículo sugerido
por el cubicador rápido → optimizado, 3/3 unidades cargadas. Detalle
completo en `cubicador/CONTRATO.md` sección 7 y en `NEXO/PENDIENTES.md`.

## CI

`.github/workflows/cubicador-ci.yml` (raíz del repo NEXO, con
`paths: ['cubicador/**']` para no disparar en cambios del backend C#), dos
jobs en cada push/PR a `QA`/`main`:

- **`unit-tests`** — instala dependencias y corre `pytest` (70 pruebas contra
  SQLite en memoria, sin infraestructura externa).
- **`docker-smoke`** — levanta primero el `rabbit` compartido de NEXO
  (`docker compose -f ../docker-compose.yml up -d --wait rabbit`) y luego el
  stack propio (`docker compose up -d --build --wait`: Postgres + api +
  worker), y corre `scripts/ci_docker_smoke.py`, que automatiza el flujo de
  las secciones 2.2-2.4 de `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md`:
  `POST /optimizar` → 202 → espera a que el worker lo procese → `optimizado`
  con `metricas.puntaje == plan.puntaje` (regresión del bug real ya
  corregido); el resultado recibido **por cola**, no solo por HTTP
  (`plan-de-cargue.generado`, sección 2.3 — antes solo verificado a mano); y
  un mensaje con `tipo` inválido → 3 reintentos → cae en `cubicacion.dlq`.
  Falla el job (exit ≠ 0) si el flujo real no llega al resultado esperado, no
  solo si el código no compila/importa.

**Todavía sin verificar automáticamente:**
- El vigilante de timeout (`docs/DECISIONES_TECNICAS.md` #10): requiere
  detener el contenedor `worker` a mitad de un mensaje, lo que acopla el
  script a la topología interna del compose de una forma que no valía la
  pena para CI — sigue siendo procedimiento manual (ver
  `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.4).
- `crear_plan_desde_linea` de punta a punta contra un NEXO real: verificado
  manualmente (ver el bullet fechado 2026-09-02 más abajo), pero el job de
  CI no levanta el repo NEXO — sigue sin automatizarse.

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
curl http://localhost:8081/health
```

El contenedor sigue escuchando en el puerto 8000 puertas adentro, pero se
publica en el host en el **8081** por defecto (`CUBICADOR_HOST_PORT` en
`.env`) — a propósito, para no chocar con el 8080 donde corre el backend
.NET de NEXO. Si 8081 tampoco sirve, cambiá `CUBICADOR_HOST_PORT=8082`.

Swagger interactivo en `http://localhost:8081/docs` — botón "Authorize"
arriba a la derecha, pega ahí tu `CUBICADOR_API_KEY` (sin la palabra
"Bearer", Swagger la agrega sola) y queda aplicado a todos los endpoints.
También hay un contrato OpenAPI ya exportado en
[`docs/openapi.json`](./docs/openapi.json) (importable en Postman/Insomnia o
para generar un cliente), y una guía corta de integración en
[`CONTRATO.md`](./CONTRATO.md).

**Correr las pruebas:**

```bash
pip install -r requirements.txt   # incluye pytest/pytest-asyncio
pytest
```

## Cómo se conecta con NEXO

- **NEXO → este servicio**: NEXO **no llama** a la API REST de este
  servicio (ver `CONTRATO.md` sección 1.1). `optimizar`/`crear-desde-línea`
  van por RabbitMQ (202 inmediato del lado Cubicador, resultado por
  polling). La API REST de este servicio es para un frontend que la
  consuma directo, no para NEXO — autenticación `Authorization: Bearer
  <CUBICADOR_API_KEY>`.
- **Este servicio → NEXO**: un único endpoint de solo lectura, en sentido
  inverso, que NEXO ya expone (`GET /api/lineas-solicitud/{id}/cubicacion`,
  `LineasSolicitudCubicacionController`) para el puente Mesa de Costeo →
  Cubicador (`crear_desde_linea`). Autenticación de servicio-a-servicio
  dedicada: `Authorization: Bearer <NEXO_CALLBACK_API_KEY>` (debe ser
  idéntica a `Cubicador3D__InboundApiKey` del `.env` de NEXO). Implementado
  y verificado de punta a punta — ver `CONTRATO.md` sección 7 y
  `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.9.
- Siguen sin compartir red Docker ni base de datos entre sí como norma —
  cada uno tiene su propio `docker-compose.yml` y todo cruce es por
  RabbitMQ (compartido, ver "Puesta en marcha") o por este único endpoint
  HTTP puntual, nunca por tabla compartida.

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
