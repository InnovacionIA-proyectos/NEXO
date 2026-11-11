# Despliegue en Dokploy

NEXO se despliega en Dokploy como **4 apps de tipo "Application" (Dockerfile)**, no como "Docker Compose". Cada Dockerfile
del repositorio es una app, y las 4 comparten la red interna `dokploy-network` que Dokploy crea para las apps de un mismo
proyecto:

| App en Dokploy | Dockerfile | Puerto interno | Notas |
|---|---|---|---|
| `nexo-rabbit` | `build/Dockerfile.rabbit` | 5672 (AMQP) / 15672 (consola) | Los puertos se exponen en la pestaña de puertos y dominios de la app. |
| `nexo-api` | `src/Nexo.Api/Dockerfile` | 8080 | API de NEXO. |
| `cubicador-api` | `cubicador/Dockerfile` | 8000 | API del Cubicador. |
| `cubicador-worker` | `cubicador/Dockerfile.worker` | — (sin HTTP) | Consume de RabbitMQ y llama a `nexo-api`. |

Las bases de datos no son apps de Dokploy: NEXO y el Cubicador usan el SQL Server corporativo (`10.10.40.85`). El Cubicador
ya no tiene Postgres propio.

Los `docker-compose.yml` de la raíz y de `cubicador/` sirven para desarrollo local, **no para Dokploy**. Como en Dokploy no
corren como Compose, nadie arma las variables compuestas que sí arma el `docker-compose.yml` (por ejemplo
`ConnectionStrings__RabbitMq` a partir de usuario y clave sueltos): cada app recibe en su pestaña **Environment** el nombre
final de cada variable, tal como la lee el proceso dentro del contenedor.

Para un ambiente nuevo (por ejemplo, producción), revisar además [`docs/despliegue/CHECKLIST_DESPLIEGUE.md`](docs/despliegue/CHECKLIST_DESPLIEGUE.md):
compuerta de paridad, llaves entre servicios, IA y base de datos.

## 1. Variables de entorno

[`.env.dokploy`](.env.dokploy) trae, agrupadas por app, las variables de cada pestaña **Environment**. Es una plantilla:
nunca se suben valores reales.

**`nexo-api`**
- **Obligatorias en Producción** (la app no arranca sin ellas): `ConnectionStrings__Nexo`, `Database__Esquema`,
  `ConnectionStrings__RabbitMq`, `ConnectionStrings__IdentidadPortal` y `Cors__OrigenesPermitidos`.
- Las cadenas del Data Warehouse (`ConnectionStrings__DwSice`, `DbSolucionesTi`, `DwCubos`) son opcionales: si faltan, el
  módulo que las usa se degrada.
- `Database__MigrateOnStartup` y `Database__SeedOnStartup` siempre en `false` (ver la sección 5).
- `Anthropic__*` es opcional: sin `Anthropic__ApiKey` la app arranca y las funciones de IA responden `503`; cada función nace
  apagada. La llave se escribe solo en Dokploy. Configuración, verificación y rotación en
  [`docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md`](docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md).
- Compuerta de paridad del plan de cargue: `Cubicacion__ParidadVerificada`, `ParidadEvidencia` y `ParidadFecha`, las tres
  juntas (ver el checklist).

**`cubicador-api` y `cubicador-worker`**
- `DATABASE_URL` con el formato `mssql+aioodbc://<usuario>:<clave>@10.10.40.85:1433/<base>?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes`.
  Si la clave trae `@ : / # ?`, va codificada.
- `CUBICADOR_DB_ESQUEMA` es **obligatoria**: si falta, el Cubicador usa `dbo_cubicador`. Va con el esquema de la base que se
  use (ver `cubicador/database/README.md`).
- `CUBICADOR_API_KEY_NEXO` y `CUBICADOR_API_KEY_COTIFLY`: una llave por sistema para `POST /recomendaciones`. La de Cotifly la
  genera Cotifly y es distinta de la de NEXO.
- Modo sombra de la IA (`IA_SOMBRA_HABILITADA`, `ANTHROPIC_*`): nace apagado. Ver la guía de la llave de Anthropic, sección 8.
- **El Cubicador no valida sus variables al arrancar.** Si falta `DATABASE_URL` arranca contra un SQLite local que se pierde
  en cada despliegue, y si falta `CUBICADOR_RABBITMQ_URL` intenta `amqp://guest:guest@localhost`. Que el contenedor siga
  arriba no significa que quedó bien configurado: siempre revisar `/health` y los logs.

**Valores que deben coincidir entre apps**

| En `nexo-api` | En el Cubicador |
|---|---|
| `Cubicador3D__RecomendacionesApiKey` | `CUBICADOR_API_KEY_NEXO` (`cubicador-api`): recomendación de vehículo |
| `Cubicador3D__OutboundApiKey` | `CUBICADOR_API_KEY` (`cubicador-api`): administración de catálogos |
| `Cubicador3D__InboundApiKey` | `NEXO_CALLBACK_API_KEY` (`cubicador-api` y `cubicador-worker`) |
| Usuario y clave de `ConnectionStrings__RabbitMq` | Los de `CUBICADOR_RABBITMQ_URL`, iguales a `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS` de `nexo-rabbit` |

## 2. Hostnames internos y orden de creación

Ninguna app se alcanza por un nombre fijo del repositorio (`rabbit`, `api`…): Dokploy asigna a cada app un hostname interno,
que se ve en la pestaña **Domains/Advanced** de la app ya creada. Por eso se crean en este orden:

1. `nexo-rabbit`. Su hostname va en `ConnectionStrings__RabbitMq` (`nexo-api`) y en `CUBICADOR_RABBITMQ_URL` (Cubicador).
2. `nexo-api`. Su hostname va en `NEXO_BASE_URL` del Cubicador.
3. `cubicador-api`. Su hostname va en `Cubicador3D__OutboundBaseUrl` de `nexo-api` (redesplegar `nexo-api`).
4. `cubicador-worker`, con las mismas variables de base, broker y NEXO que `cubicador-api`.

Un desfase breve al arrancar no es crítico: el worker reintenta la conexión a RabbitMQ con margen.

## 3. Verificación después de desplegar

- `nexo-api`: `GET /health` responde 200.
- `cubicador-api`: `GET /health` responde 200, y los logs muestran que conectó al SQL Server y al RabbitMQ reales, no a
  SQLite ni a `guest:guest`.
- `cubicador-worker`: los logs muestran que conectó a RabbitMQ y no se queda reintentando.
- `nexo-rabbit`: consola de administración en el puerto 15672, con `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS`.
- Si se configuró la llave de Anthropic: verificar la salida a `api.anthropic.com` desde el contenedor (guía de la llave,
  sección 5).

## 4. Base de datos de NEXO

El proyecto no usa migraciones de EF Core. El esquema se crea una sola vez, contra una base vacía, con
`database/esquema/ESQUEMA_NEXO.sql`, y los catálogos iniciales se cargan con los CSV de `database/seeder/`. El script
`database/scripts/ejecutar_schema_y_seeders.py` hace las dos cosas en orden; el detalle está en
[`database/README.md`](database/README.md).

- `ESQUEMA_NEXO.sql` crea las tablas en el esquema `dbo` de la base propia (el QA actual usa `db_NEXO`; ver «Ambientes» en
  `database/README.md`), fija `ANSI_NULLS` y `QUOTED_IDENTIFIER` para los índices filtrados y crea los índices antes que las
  llaves foráneas.
- `seeder/datos_base_nexo.sql` carga el usuario de sistema, el mapeo de roles del portal y los parámetros del puntaje, sin los
  cuales la app no funciona.
- La carga es de una sola vez: si la fuente original de un catálogo cambia después, el CSV no se actualiza solo.
- `Database__MigrateOnStartup` va **siempre en `false`**, porque no hay migraciones. `Database__SeedOnStartup` va **siempre en
  `false`** en Producción, porque `clsDataSeeder.cs` carga datos ficticios de demostración.

## 5. Base de datos del Cubicador

Igual que NEXO: el esquema y el catálogo inicial se crean una sola vez con `cubicador/database/esquema/ESQUEMA_CUBICADOR.sql`
y los CSV de `cubicador/database/seeder/`, usando `cubicador/database/scripts/ejecutar_schema_y_seeders.py`. Ver
[`cubicador/database/README.md`](cubicador/database/README.md). La app solo lee y escribe datos: el usuario de la aplicación
necesita `db_datareader` y `db_datawriter`.
