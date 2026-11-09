# Base de datos del Cubicador

Definición de la base de datos del Cubicador y su catálogo inicial. La documentación de las
tablas está en [`../docs/database/`](../docs/database/).

| | |
|---|---|
| Servidor | `10.10.40.85,1433` (SQL Server 2019) |
| Base | `dbCubicador_Pruebas` |
| Esquema | `dbo` |
| Tablas | 16: catálogo, recomendación y motor 3D |

## Dónde vive hoy

La base propia ya está creada, pero el ambiente de QA todavía no se ha pasado a ella. Ese corte
está pendiente en la issue #423.

| Ambiente | Base | Esquema | Estado |
|---|---|---|---|
| QA desplegado | `dbTarifario_Nexo_Pruebas` | `dbo_cubicador` | En uso, con `CUBICADOR_DB_ESQUEMA=dbo_cubicador` explícita. Se creó con scripts anteriores; su estructura es igual a la de `esquema/ESQUEMA_CUBICADOR.sql` (solo cambian los nombres generados de algunas PK, FK e índices). |
| Base propia (#327) | `dbCubicador_Pruebas` | `dbo` | Creada con el esquema y el catálogo de esta carpeta. La aplicación todavía no la usa. |
| Local y CI | contenedor de `docker-compose.yml` | `dbo` | Se crea en cada arranque. |

Las tablas `planes_cargue`, `items_plan_cargue`, `colocaciones_cargue` y `vehiculos_cubicacion` que
hay en `dbInformesAutomatizados_Pruebas.dbo_NEXO_prueba` **no son del Cubicador**: son tablas
antiguas de NEXO con el mismo nombre (ver `docs/Database/esquema/ESQUEMA_NEXO.sql` en la raíz del
repositorio).

## Contenido de la carpeta

| Ruta | Qué es |
|---|---|
| `esquema/ESQUEMA_CUBICADOR.sql` | DDL de las 16 tablas: columnas, llaves, `CHECK` e índices. Es la única definición del esquema. |
| `seeder/` | Catálogo inicial: 7 CSV, uno por tabla. |
| `scripts/ejecutar_schema_y_seeders.py` | Crea las tablas y carga los CSV en una base vacía. |

## Documentación

| Documento | Qué es |
|---|---|
| [`docs/database/DICCIONARIO-DATOS.md`](../docs/database/DICCIONARIO-DATOS.md) | Cada tabla: utilidad, de dónde salen sus datos, si tiene seeder y sus columnas (tipo, nulos, valor por defecto, origen y descripción). |
| [`docs/database/ORIGEN-Y-DECISIONES-SEEDER.md`](../docs/database/ORIGEN-Y-DECISIONES-SEEDER.md) | De dónde sale cada dato del catálogo inicial, qué reglas se aplicaron y cómo se calcula el Id de cada fila. |

## Requisitos

- Acceso al servidor desde el equipo donde se ejecuta (red de la empresa o VPN).
- `sqlcmd` en el `PATH`.
- Autenticación de Windows con un usuario que pueda crear tablas en la base.
- Python 3 (solo librería estándar).

## Crear la base

Se hace una sola vez, contra una base vacía. Desde `cubicador/database/`:

```bash
py scripts/ejecutar_schema_y_seeders.py --server 10.10.40.85,1433
```

El script:

1. Aplica `esquema/ESQUEMA_CUBICADOR.sql`: crea las 16 tablas vacías en el esquema `dbo`.
2. Carga los 7 CSV de `seeder/` en el orden de sus llaves foráneas: carrocerías, categorías,
   vehículos, mercancías, relación vehículo-carrocería, reglas de incompatibilidad y parámetros
   del motor.

Las tablas de recomendación y del motor 3D quedan vacías. Los vehículos del motor 3D
(`vehiculos_cubicacion`) los crea la aplicación la primera vez que arranca.

Opciones:

| Opción | Uso |
|---|---|
| `--solo-schema` | Solo crea las tablas. |
| `--solo-datos` | Solo carga los CSV, sobre tablas ya creadas. Se puede repetir: los Id son fijos, así que solo inserta las filas que faltan y no modifica las existentes. |

Para usar otra base, cambie `NOMBRE_BD` al inicio de `scripts/ejecutar_schema_y_seeders.py`.

## Conectar la aplicación

La aplicación (API y worker) lee dos variables de entorno:

| Variable | Valor |
|---|---|
| `DATABASE_URL` | `mssql+aioodbc://<usuario>:<clave>@10.10.40.85:1433/dbCubicador_Pruebas?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes` |
| `CUBICADOR_DB_ESQUEMA` | `dbo`. Es obligatoria: si falta, la aplicación usa `dbo_cubicador`. |

Si la clave tiene `@ : / # ?`, debe ir codificada (`urllib.parse.quote_plus`).

El usuario de la aplicación solo necesita leer y escribir datos (`db_datareader` y
`db_datawriter`). El esquema lo aplica una persona, no la aplicación.

## Cambiar el catálogo inicial

Se editan directamente los CSV de `seeder/`. Una fila nueva lleva un Id calculado con la
fórmula de [`ORIGEN-Y-DECISIONES-SEEDER.md`](../docs/database/ORIGEN-Y-DECISIONES-SEEDER.md)
(«Id de las filas»), para que todas las bases sigan teniendo los mismos Id. Las filas
existentes no cambian de Id, y si se cambia un dato del catálogo se actualizan también los dos
documentos de `docs/database/`.

Esto solo afecta a las bases que se creen después. En una base que ya está en uso, el catálogo
se modifica desde la administración de la aplicación.

## Cambiar la estructura

El esquema no tiene migraciones. Para un cambio de estructura:

1. Edite `esquema/ESQUEMA_CUBICADOR.sql` para que siga describiendo el esquema completo.
2. Escriba el `ALTER` equivalente, de forma que se pueda ejecutar más de una vez sin error (por
   ejemplo, con `IF COL_LENGTH(...) IS NULL`).
3. Aplique ese `ALTER` a mano en cada base existente.

No borre y recree tablas que ya tengan datos reales de recomendaciones o planes de cargue.
