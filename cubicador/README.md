# Cubicador

Microservicio propio, contenerizado, que reemplaza al módulo interno
"Cubicación 3D" de NEXO (`Application/Cubicacion3D`,
`Infrastructure/Cubicacion3D/Motor`). Trae **completo** el cubicador que
originalmente vivía en `cabinaFletes`: el cubicador rápido (evaluador SICCE
por criterios/veto) y el motor de bin-packing 3D (planes de cargue,
optimización, ajuste manual), con su propia persistencia.

No es un servicio de cómputo sin estado: es dueño de todo el dominio
Cubicación — vehículos, planes de cargue, ítems, colocaciones y parámetros —
con su propia base de datos. NEXO deja de tener esta lógica y pasa a ser un
consumidor vía REST (operaciones simples) y RabbitMQ (operaciones pesadas:
optimizar, crear plan desde línea).

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
- **pytest** — 39 pruebas: 33 de reglas de negocio del motor + 4 de humo end-to-end de la API + 2 de regresión del worker (desempaque del mensaje de RabbitMQ)

## Estado actual (léelo antes de asumir que algo funciona)

**Verificado con pytest (sin Docker):**
- El port completo del motor (`domain/motor/*`) — las 31 reglas de
  `docs/testDocumentation/REGLAS_DE_NEGOCIO.md` pasan sin ajustes.
- El flujo completo API → Application → Infrastructure → Domain contra
  SQLite: crear vehículo (con siembra de los 9 presets) → crear plan → agregar
  ítem → optimizar → leer resultado → ajustar manualmente una colocación. Ver
  `tests/api/test_smoke.py`.
- Autenticación por Bearer estático rechazando llamadas sin la key.

**Verificado con Docker Compose real (Postgres + RabbitMQ + api + worker, los 4 contenedores sanos):**
- El mismo flujo de arriba, pero contra **PostgreSQL real** vía `docker
  compose up -d` (con healthchecks: `api`/`worker` esperan a que `db` y
  `rabbitmq` estén `healthy` antes de arrancar — sin eso, arrancaban antes de
  que Postgres terminara su init y se caían).
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

## CI

`.github/workflows/ci.yml`, dos jobs en cada push/PR a `main`:

- **`unit-tests`** — instala dependencias y corre `pytest` (39 pruebas contra
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

**Con Docker Compose (api + worker + rabbitmq + Postgres):**

```bash
cp .env.example .env   # completar POSTGRES_PASSWORD, RABBITMQ_DEFAULT_USER/PASS, CUBICADOR_API_KEY, NEXO_CALLBACK_API_KEY, NEXO_BASE_URL
docker compose up -d
curl http://localhost:8000/health
```

**Correr las pruebas:**

```bash
pip install -r requirements.txt   # incluye pytest/pytest-asyncio
pytest
```

## Cómo se conecta con NEXO

- **NEXO → este servicio**: REST síncrono para todo salvo optimizar/crear-desde-línea, que van por RabbitMQ (202 inmediato, resultado por polling — ver arriba). Autenticación: `Authorization: Bearer <CUBICADOR_API_KEY>`.
- **Este servicio → NEXO**: un único endpoint de solo lectura que NEXO debe exponer (`GET /api/lineas-solicitud/{id}/cubicacion`) para el puente Mesa de Costeo → Cubicador. Autenticación: `Authorization: Bearer <NEXO_CALLBACK_API_KEY>`.
- Ninguno de los dos servicios comparte red Docker ni base de datos — todo cruce es por API o mensaje, nunca por tabla compartida.

## Alcance explícitamente fuera de esta primera versión

- Paridad automatizada de 26 casos dorados contra el motor C# — no aplica: el
  motor va a cambiar de diseño más adelante, y comparar contra una versión que
  se sabe temporal no aporta valor duradero.
- `POST /api/cubicacion/costear` (cubicar + estimar flete) — no se migró en
  NEXO tampoco: depende de un método de Biblioteca (`EstimarFleteAsync`) que
  nunca se migró a su puerto. Gap pre-existente, ajeno al Cubicador.
- Frontend del Cubicador 3D — sigue pendiente aparte, en ningún repo todavía.
