# Despliegue en Dokploy

Este repo tiene dos stacks independientes, cada uno como una app "Docker Compose" (o "Dockerfile") separada en Dokploy. Comparten el broker RabbitMQ del stack `src/` a través de una red Docker externa.

## 1. Variables de entorno

Copia las variables de `.env.dokploy` a la sección **Environment** de cada app en Dokploy (no subas ese archivo con secretos reales; es solo la plantilla). Cubre RabbitMQ, el backend .NET (`src/`) y Cubicador (`api` + `worker` + Postgres).

Puntos clave:
- `ConnectionStrings__Nexo` y las demás cadenas de conexión son las únicas rutas a bases de datos: no hay nada hardcodeado en el código, todo llega por variables de entorno.
- `Cubicador3D__InboundApiKey` (backend) y `NEXO_CALLBACK_API_KEY` (Cubicador) deben tener el **mismo valor** — es la llamada de Cubicador de vuelta hacia NEXO.
- `NEXO_BASE_URL` (Cubicador) debe apuntar a la URL pública/interna donde Dokploy expone la app del backend.

## 2. Orden de despliegue

1. **Stack `src/`** (backend + RabbitMQ): usa `docker-compose.yml` de la raíz. Publica la red interna como `nexo_internal` (nombre fijado por `name: nexo` en la cabecera del archivo).
2. **Stack `cubicador/`**: usa `cubicador/docker-compose.yml`. Se conecta a `nexo_internal` como red externa para hablarle a `rabbit` del paso 1 — por eso el stack de `src/` debe estar arriba (al menos el servicio `rabbit`) antes o al mismo tiempo que este.

Si Dokploy no soporta declarar la dependencia entre proyectos Compose, el `worker` de Cubicador reintenta la conexión a RabbitMQ con margen generoso al arrancar, así que un breve desfase de arranque no es crítico — pero no debe quedar levantado sin RabbitMQ disponible por mucho tiempo (ver `restart: unless-stopped`).

## 3. Servicios y puertos

| Stack | Servicio | Dockerfile | Puerto interno | Puerto host (var) |
|---|---|---|---|---|
| `src/` | `api` | `src/Nexo.Api/Dockerfile` | 8080 | `API_PORT` (8080) |
| `src/` | `rabbit` | `build/Dockerfile.rabbit` | 5672 / 15672 | `RABBITMQ_AMQP_PORT` / `RABBITMQ_UI_PORT` |
| `cubicador/` | `api` | `cubicador/Dockerfile` | 8000 | `CUBICADOR_HOST_PORT` (8081) |
| `cubicador/` | `worker` | `cubicador/Dockerfile.worker` | — (sin puerto HTTP) | — |
| `cubicador/` | `db` | imagen `postgres:16-alpine` | 5432 | 5433 (fijo) |

## 4. Verificación post-despliegue

- Backend: `GET /health` en el puerto de `api` (`src/`) → 200.
- Cubicador: `GET /health` en el puerto de `api` (`cubicador/`) → 200.
- RabbitMQ: consola de administración en `RABBITMQ_UI_PORT` (usuario/clave de `.env.dokploy`).
- Revisar logs del `worker` de Cubicador: debe conectar a RabbitMQ sin quedarse reintentando indefinidamente.

## 5. Migraciones de base de datos

El backend aplica migraciones de EF Core al arrancar si `MIGRATE_ON_STARTUP=true`. En el primer despliegue a una base nueva, déjalo en `true`; para despliegues posteriores sobre la misma base, puede quedar en `false` si prefieres aplicar migraciones manualmente.
