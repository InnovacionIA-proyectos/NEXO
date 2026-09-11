# CI/CD — convención de rutas por servicio (S3-HU-11)

Cada uno de los 4 servicios (API .NET, Cubicador-API, Cubicador-Worker,
RabbitMQ) tiene su propio workflow en `.github/workflows/`, disparado por
cambios que puedan afectarlo — nunca por "cualquier cambio del repo". Este
documento es la referencia de qué ruta dispara qué pipeline, y la convención a
seguir cuando se agrega código nuevo.

## Los 5 archivos

| Archivo | Dispara | Se dispara solo |
|---|---|---|
| `ci-cd-api.yml` | Servicio API (.NET) | Sí (paths propios) |
| `ci-cd-cubicador-api.yml` | Servicio Cubicador-API | Sí — mismos paths que el del worker, ver abajo |
| `ci-cd-cubicador-worker.yml` | Servicio Cubicador-Worker | Sí — mismos paths que el del API, ver abajo |
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
| `cubicador/**` (código, Dockerfiles, `requirements.txt`, `pyproject.toml`, `tests/`, `scripts/`, `docker-compose.yml`) | | ✅ | ✅ | |
| `cubicador/docs/**`, `cubicador/**.md` | | | | |
| `docker-compose.yml` (raíz) | | ✅ | ✅ | ✅ |
| `build/Dockerfile.rabbit` | | ✅ | ✅ | ✅ |

## Los dos pipelines de Cubicador disparan con los MISMOS paths

No es un descuido ni copia-pega: es lo único correcto, y se sigue de tres
hechos verificables del repo.

**1. El gate de pruebas es uno solo, y prueba los dos servicios.** Los dos
workflows llaman al mismo `_cubicador-pruebas.yml`, que corre `pytest -v`
sobre la suite entera —5 de los 11 archivos de test viven en
`cubicador/tests/worker/`— y después un smoke de Docker Compose que levanta
`api` + `worker` + Postgres + RabbitMQ y ejercita el flujo async de punta a
punta. Un cambio que toque solo al worker puede poner en rojo el gate del
pipeline de la API. Si ese pipeline no se dispara, nadie se entera hasta el
próximo cambio bajo `api/` — y para entonces el rojo parece culpa de ese
cambio.

**2. Las dos imágenes contienen todo el árbol.** `cubicador/Dockerfile` y
`cubicador/Dockerfile.worker` hacen ambos `COPY . .`. Cualquier archivo bajo
`cubicador/` cambia las **dos** imágenes. Desplegar una sola deja a la otra
corriendo un commit distinto contra el mismo esquema de base y el mismo
protocolo de cola.

**3. `cubicador/api/` no es exclusivo de la API.** `worker/main.py` importa
`api.dependencias` y `api.dtos.plan_cargue` (que a su vez arrastra
`api/dtos/comun.py`). La separación por carpeta de nivel superior —
`cubicador/api/**` para un pipeline, `cubicador/worker/**` para el otro— es
falsa en este código.

Lo único que de verdad distingue a los dos workflows es **a qué aplicación de
Dokploy le avisan**: `DOKPLOY_DEPLOY_WEBHOOK_URL_CUBICADOR_API` contra
`DOKPLOY_DEPLOY_WEBHOOK_URL_CUBICADOR_WORKER`.

### Por qué esto importa de verdad (no es prolijidad)

Los 3 bugs cerrados en S3-HU-10 (`tests/QA/s3-hu-10-tres-bugs-cubicacion/`)
son el caso de prueba, y **los tres caen en carpetas distintas**:

| Bug | Archivo | Rompía a |
|---|---|---|
| 10.1 (#131) | `cubicador/worker/main.py` | el worker |
| 10.2 (#132) | `cubicador/api/dtos/plan_cargue.py` | **el worker** |
| 10.3 (#133) | `cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py` | los dos |

El 10.2 es el que desarma cualquier convención basada en carpetas: vivía bajo
`cubicador/api/` y el síntoma era que el **worker** mandaba ítems a la DLQ.
Con un filtro que tratara `cubicador/api/**` como exclusivo de la API, ese
arreglo habría desplegado solo la API y dejado al worker con el código roto.

### Lo único que NO dispara nada

`cubicador/docs/**` y cualquier `.md` bajo `cubicador/`. La documentación no
cambia comportamiento, y un README no justifica reiniciar dos servicios en
Pre-Despliegue. (`COPY . .` igual mete esos archivos en la imagen; la
diferencia es irrelevante.)

### Las dos rutas de fuera de `cubicador/` que sí disparan

`docker-compose.yml` (raíz) y `build/Dockerfile.rabbit` son **insumos de
construcción** del job `docker-smoke`: ese job levanta el rabbit compartido
con `docker compose -f docker-compose.yml up -d --build rabbit`, y ese
servicio se construye desde `build/Dockerfile.rabbit`. Romper cualquiera de
los dos rompe el CI de Cubicador, así que los dos pipelines tienen que
enterarse. Comparten esas rutas con `ci-cd-rabbitmq.yml`, que es el que además
redespliega el broker.

## Cómo verificarlo sin empujar un commit

```bash
python scripts/verificar_rutas_ci.py
```

Reimplementa la semántica de `paths:` de GitHub (incluidas las negaciones, donde
gana el último patrón que matchea), la aplica sobre los workflows reales y la
contrasta contra la convención de esta página, archivo por archivo. Sale con
código 1 si algo no se cumple, así que sirve para detectar el día que alguien
"limpie" un filtro y rompa la coordinación sin darse cuenta.

Los huecos conocidos de `ci-cd-api.yml` y `ci-cd-rabbitmq.yml` (issues #134 y
#137) los reporta como avisos, no como fallas: no son parte de S3-HU-11.2/11.3.

## Convención para código nuevo: ¿dónde lo ubico?

Ojo con la distinción: **dónde ubicar un archivo** y **qué pipeline dispara**
son dos preguntas distintas. La segunda ya está resuelta (todo `cubicador/`
dispara los dos). La primera sigue importando para que el código se entienda:

- **`domain/`** — entidades y motor de cubicación. Sin dependencias de
  FastAPI, SQLAlchemy ni aio-pika.
- **`application/`** — casos de uso, los mismos que invoca tanto un endpoint
  HTTP como un mensaje de cola.
- **`infrastructure/`** — repositorios SQLAlchemy, cliente de RabbitMQ,
  cliente HTTP hacia NEXO, alertas.
- **`api/`** — handlers HTTP y los DTOs de request/response. **No es privado
  de la API**: el worker reusa a propósito los DTOs de `api/dtos/` para no
  tener dos definiciones del mismo contrato que puedan divergir (que es
  exactamente el bug que S3-HU-06.2 vigila entre C# y Python).
- **`worker/`** — el bucle de consumo de cola, reintentos y DLQ.

Lo que **no** hay que hacer es duplicar un archivo dentro de `api/` y `worker/`
por separado para "que cada servicio tenga el suyo": dos copias del mismo
contrato divergen, y el CI no lo va a atrapar porque las dos pasan sus propias
pruebas.

## Los otros dos servicios (API .NET, RabbitMQ)

No comparten código con Cubicador, así que sus `paths:` son autocontenidos
(salvo `docker-compose.yml` y `build/Dockerfile.rabbit`, ya explicados). La
única razón por la que `ci-cd-api.yml` incluye `CabinaFletes.Clean.sln` en su
lista (y no solo `src/**`) es que agregar un proyecto nuevo a la solución
cambia ese archivo; sin incluirlo, un `.csproj` nuevo podría no disparar el
pipeline la primera vez que se referencia.
