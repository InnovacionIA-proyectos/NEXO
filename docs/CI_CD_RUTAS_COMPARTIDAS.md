# CI/CD — convención de rutas por servicio (S3-HU-11)

Cada uno de los 4 servicios (API .NET, Cubicador-API, Cubicador-Worker,
RabbitMQ) tiene su propio workflow en `.github/workflows/`, disparado SOLO
por cambios en su propia carpeta/archivos — nunca por "cualquier cambio del
repo". Este documento es la referencia de qué ruta dispara qué pipeline, y
la convención a seguir cuando se agrega código nuevo.

## Los 5 archivos

| Archivo | Dispara | Se dispara solo |
|---|---|---|
| `ci-cd-api.yml` | Servicio API (.NET) | Sí (paths propios) |
| `ci-cd-cubicador-api.yml` | Servicio Cubicador-API | No — ver tabla de abajo |
| `ci-cd-cubicador-worker.yml` | Servicio Cubicador-Worker | No — ver tabla de abajo |
| `ci-cd-rabbitmq.yml` | RabbitMQ | Sí (paths propios) |
| `_cubicador-pruebas.yml` | (nada — reusable) | Llamado por los 2 de Cubicador |

`_cubicador-pruebas.yml` no tiene disparador propio (`on: workflow_call`
únicamente) — existe para que `ci-cd-cubicador-api.yml` y
`ci-cd-cubicador-worker.yml` no dupliquen el mismo texto de pytest+smoke, que
tendría que mantenerse sincronizado a mano en dos archivos (ver la razón
completa en el encabezado de ese archivo).

## Tabla de rutas → pipelines

| Ruta que cambió | `ci-cd-api` | `ci-cd-cubicador-api` | `ci-cd-cubicador-worker` | `ci-cd-rabbitmq` |
|---|:---:|:---:|:---:|:---:|
| `src/**`, `tests/**`, `CabinaFletes.Clean.sln` | ✅ | | | |
| `cubicador/api/**`, `cubicador/Dockerfile` | | ✅ | | |
| `cubicador/worker/**`, `cubicador/Dockerfile.worker` | | | ✅ | |
| `cubicador/domain/**`, `cubicador/application/**`, `cubicador/infrastructure/**` | | ✅ | ✅ | |
| `cubicador/tests/**`, `cubicador/scripts/**`, `cubicador/requirements.txt` | | ✅ | ✅ | |
| `cubicador/docker-compose.yml` | | ✅ | ✅ | |
| `docker-compose.yml` (raíz), `build/Dockerfile.rabbit` | | | | ✅ |

Una ruta que aparece en dos columnas dispara **los dos** pipelines — no es
un error, es el criterio de cierre de la issue #138: un cambio en código
verdaderamente compartido entre Cubicador-API y Cubicador-Worker no puede
desplegar solo uno de los dos y dejar al otro corriendo código viejo contra
el mismo esquema/protocolo.

### Por qué esto importa de verdad (no es solo prolijidad)

Los 3 bugs cerrados en S3-HU-10 (`tests/QA/s3-hu-10-tres-bugs-cubicacion/`)
vivían en `cubicador/infrastructure/persistence/repositorios/
plan_cargue_repo.py` y en el manejo de errores de `cubicador/worker/main.py`
— código que **ejecuta el worker**, pero que la API síncrona de Cubicador
también importa indirectamente (mismos repositorios, mismas entidades). Si
en ese momento hubiera existido un pipeline separado por servicio SIN esta
coordinación de rutas, arreglar el bug habría disparado (con la convención
ingenua "cada carpeta de nivel superior es un servicio") solo
`ci-cd-cubicador-worker`, dejando la imagen de Cubicador-API desplegada con
la versión rota del mismo archivo hasta el siguiente cambio que sí tocara
`cubicador/api/`. Es exactamente el escenario que este documento existe para
prevenir.

## Convención para código nuevo: ¿dónde lo ubico?

**Antes de crear un archivo nuevo bajo `cubicador/`, la pregunta es: ¿lo
importa solo `api/` o solo `worker/`, o lo importan los dos?**

- **Exclusivo de Cubicador-API**: código que solo `api/main.py`/
  `api/routers/*` importan — típicamente handlers HTTP, DTOs de request/
  response, dependencias de FastAPI. Va bajo `cubicador/api/`.
- **Exclusivo de Cubicador-Worker**: código que solo `worker/main.py`
  importa y que la API nunca toca — típicamente lógica de consumo de cola,
  reintentos, DLQ. Va bajo `cubicador/worker/`.
- **Compartido**: TODO lo demás — `domain/` (entidades y motor de
  cubicación), `application/` (casos de uso, los mismos que invoca tanto un
  endpoint HTTP como un mensaje de cola), `infrastructure/` (repositorios
  SQLAlchemy, cliente de RabbitMQ, cliente HTTP hacia NEXO). Si dudas si algo
  es compartido: **si tanto `api/main.py` como `worker/main.py` lo importan
  (directo o transitivo), es compartido** — y por lo tanto va en una de esas
  3 carpetas, nunca duplicado dentro de `api/` y `worker/` por separado.
- **`tests/`, `scripts/`, `requirements.txt`, `docker-compose.yml`**: se
  tratan como compartidos siempre, sin excepción — la suite de pytest y el
  smoke test de Docker Compose ya ejercitan api+worker juntos (no hay forma
  de "solo probar la mitad"), y el `docker-compose.yml` de `cubicador/`
  define los contenedores de LOS DOS servicios en el mismo archivo.

Si un archivo nuevo no encaja claramente en ninguna categoría, el criterio
por defecto es tratarlo como compartido (dispara ambos pipelines) — cuesta
un poco más de CI, pero es el error seguro: el otro lado (un pipeline que
debía dispararse y no lo hizo) es el que de verdad sale caro, como ya pasó
con los bugs de S3-HU-10.

## Los otros dos servicios (API .NET, RabbitMQ)

No comparten código entre sí ni con Cubicador, así que no necesitan esta
coordinación — sus `paths:` son autocontenidos. La única razón por la que
`ci-cd-api.yml` incluye `CabinaFletes.Clean.sln` en su lista (y no solo
`src/**`) es que agregar un proyecto nuevo a la solución cambia ese archivo;
sin incluirlo, un `.csproj` nuevo podría no disparar el pipeline la primera
vez que se referencia.
