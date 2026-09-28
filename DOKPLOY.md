# Despliegue en Dokploy

El despliegue real se hace con **5 apps tipo "Application" (Dockerfile) separadas** en Dokploy — no como "Docker Compose". Cada Dockerfile del repo se registra como su propia app, y las 5 comparten la red interna `dokploy-network` que Dokploy crea automáticamente para las apps de un mismo proyecto:

| App en Dokploy | Dockerfile / imagen |
|---|---|
| `nexo-rabbit` | `build/Dockerfile.rabbit` |
| `nexo-api` | `src/Nexo.Api/Dockerfile` |
| `cubicador-db` | imagen `postgres:16-alpine` (sin build; base de datos administrada o Application con esa imagen) |
| `cubicador-api` | `cubicador/Dockerfile` |
| `cubicador-worker` | `cubicador/Dockerfile.worker` |

Los `docker-compose.yml` de la raíz y de `cubicador/` siguen existiendo en el repo (sirven para desarrollo local con `docker compose up`), pero **no se usan para el despliegue en Dokploy**: al no correr como Compose, Dokploy no arma por ti la traducción de nombres de variables ni las URLs compuestas que el propio `docker-compose.yml` construye (por ejemplo `ConnectionStrings__RabbitMq` a partir de `RABBITMQ_USER`/`RABBITMQ_PASSWORD`, o `DATABASE_URL` a partir de las piezas de Postgres). Cada app debe recibir, en su pestaña **Environment**, el nombre final y real de cada variable — exactamente como las lee el proceso dentro del contenedor.

## 1. Variables de entorno

`.env.dokploy` en la raíz del repo trae, agrupadas por app de Dokploy (no por stack), las variables exactas que va cada una en su pestaña **Environment**. No subas ese archivo con valores reales — es solo la plantilla, con los secretos y hosts como placeholders entre `<...>`.

Puntos clave:
- Dos variables de `nexo-api` son **nuevas frente a la versión anterior de este archivo y bloquean el arranque en Producción** si faltan (ver `clsProgram.cs` / `clsEsquemaNexo.cs`):
  - `Database__Esquema` — antes la armaba únicamente `docker-compose.yml`.
  - `ConnectionStrings__RabbitMq` — antes la armaba únicamente `docker-compose.yml`, a partir de `RABBITMQ_USER`/`RABBITMQ_PASSWORD` sueltas.
- Las imágenes oficiales exigen sus propios nombres de variable, distintos a los que usaba el `.env.dokploy` anterior: RabbitMQ necesita `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS` (no `RABBITMQ_USER`/`RABBITMQ_PASSWORD`); Postgres necesita `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` explícitos (antes venían hardcodeados como literales `cubicador`/`cubicador` en el compose de `cubicador/`).
- `ConnectionStrings__Nexo` y las demás cadenas de conexión SQL Server son las únicas rutas a bases de datos: no hay nada hardcodeado en el código, todo llega por variables de entorno.
- `Cubicador3D__InboundApiKey` (`nexo-api`) y `NEXO_CALLBACK_API_KEY` (`cubicador-api`/`cubicador-worker`) deben tener el **mismo valor** — es la llamada de Cubicador de vuelta hacia NEXO. Igual para `Cubicador3D__OutboundApiKey` (`nexo-api`) y `CUBICADOR_API_KEY` (`cubicador-api`).
- `NEXO_BASE_URL` (`cubicador-api`/`cubicador-worker`) y `ConnectionStrings__RabbitMq` / `CUBICADOR_RABBITMQ_URL` / `DATABASE_URL` necesitan el **hostname interno que Dokploy le asigna a cada app** (ver punto 2) — no `localhost` ni el nombre de servicio de un `docker-compose.yml`.
- **Cubicador no tiene validación de arranque** (`cubicador/api/dependencias.py`, `cubicador/infrastructure/persistence/db.py` leen las variables con `os.environ.get(...)` y un valor por defecto silencioso): si `DATABASE_URL` o `CUBICADOR_RABBITMQ_URL` quedan sin definir, el contenedor **arranca igual** contra un SQLite local (se pierde en cada redeploy) o intenta `amqp://guest:guest@localhost:5672/` sin avisar. Que el contenedor no truene no significa que quedó bien configurado — verificar siempre `/health` y logs tras el primer despliegue.

## 2. Hostnames internos entre apps

Como no hay `docker-compose.yml` de por medio, ninguna app se alcanza por un nombre de servicio fijo del repo (`rabbit`, `db`, etc.). Dokploy pone todas las apps de un mismo proyecto en su red interna `dokploy-network` y les asigna un hostname interno propio — revísalo en la pestaña **Domains/Advanced** de cada app ya creada en Dokploy.

Antes de completar las variables de las apps que dependen de otra, crea primero la app de la que dependen para conocer su hostname:

1. Crea y despliega `nexo-rabbit` y `cubicador-db` primero (no dependen de nadie).
2. Toma sus hostnames internos y complétalos en `ConnectionStrings__RabbitMq` (`nexo-api`), y en `CUBICADOR_RABBITMQ_URL` / `DATABASE_URL` (`cubicador-api`, `cubicador-worker`).
3. Crea y despliega `nexo-api`. Toma su hostname interno y complétalo en `NEXO_BASE_URL` / `Cubicador3D__OutboundBaseUrl` donde aplique.
4. Crea y despliega `cubicador-api`, y toma su hostname para `Cubicador3D__OutboundBaseUrl` en `nexo-api` si aún no estaba.
5. Crea y despliega `cubicador-worker` (mismas variables que `cubicador-api`).

Un desfase breve de arranque no es crítico: `cubicador-worker` reintenta la conexión a RabbitMQ con margen generoso.

## 3. Servicios y puertos

| App Dokploy | Dockerfile / imagen | Puerto interno | Notas |
|---|---|---|---|
| `nexo-rabbit` | `build/Dockerfile.rabbit` | 5672 (AMQP) / 15672 (consola) | Puertos se exponen desde la pestaña de puertos/dominios de la app, no por variable de entorno. |
| `nexo-api` | `src/Nexo.Api/Dockerfile` | 8080 | — |
| `cubicador-db` | imagen `postgres:16-alpine` | 5432 | Sin Dockerfile propio en el repo. |
| `cubicador-api` | `cubicador/Dockerfile` | 8000 | — |
| `cubicador-worker` | `cubicador/Dockerfile.worker` | — (sin puerto HTTP) | Solo consume de RabbitMQ y llama a `nexo-api`/`cubicador-db`. |

## 4. Verificación post-despliegue

- `nexo-api`: `GET /health` → 200.
- `cubicador-api`: `GET /health` → 200. **No basta con que el contenedor siga arriba** — revisar logs para confirmar que sí conectó a Postgres/RabbitMQ reales y no a los defaults silenciosos de SQLite/`guest:guest` (ver punto 1).
- `nexo-rabbit`: consola de administración en el puerto 15672 (usuario/clave = `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` de `.env.dokploy`).
- Logs de `cubicador-worker`: debe conectar a RabbitMQ sin quedarse reintentando indefinidamente.

## 5. Esquema de base de datos

Este proyecto no usa migraciones de EF Core — el esquema se crea/actualiza corriendo `docs/Database/ESQUEMA_NEXO.sql` a mano contra la base (una sola vez, en el primer despliegue contra una base nueva y vacía). `Database__MigrateOnStartup` debe quedar **siempre en `false`**: no hay migraciones que aplicar. `Database__SeedOnStartup` debe quedar **siempre en `false`** en Producción: `clsDataSeeder.cs` es 100% datos ficticios de demo.

Después de crear el esquema, hay catálogos que se pueblan igual de manual, con los datos ya calculados (no por migración ni por ningún comando/endpoint de la app) — ver `docs/Database/seeder/README.md` para la lista completa y cómo cargar cada CSV (`BULK INSERT`). Es poblado de **una sola vez**: si la fuente original cambia después, el CSV no se entera solo.
