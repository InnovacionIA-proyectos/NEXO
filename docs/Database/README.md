# Base de datos de NEXO

Definición de la base de datos de NEXO y sus datos iniciales. La documentación de las tablas está
en [`docs/documentacion/database/`](../documentacion/database/).

| | |
|---|---|
| Servidor | `10.10.40.85,1433` (SQL Server 2019) |
| Base | `dbTarifario_Nexo_Pruebas` |
| Esquema | `dbo` |
| Tablas | 46 |

## Contenido de la carpeta

| Ruta | Qué es |
|---|---|
| `esquema/ESQUEMA_NEXO.sql` | DDL de las 46 tablas: columnas, llaves, `CHECK`, índices y trigger. Es la única definición del esquema. |
| `seeder/` | Datos iniciales: `datos_base_nexo.sql` (usuario de sistema, roles del portal y parámetros del score) y un CSV por cada catálogo. |
| `scripts/ejecutar_schema_y_seeders.py` | Crea las tablas y carga los datos iniciales en una base vacía. |

## Documentación

| Documento | Qué es |
|---|---|
| [`DICCIONARIO-DATOS.md`](../documentacion/database/DICCIONARIO-DATOS.md) | Cada tabla: para qué sirve, sus columnas, llaves e índices y de dónde salen sus datos. |
| [`ORIGEN-Y-DECISIONES-SEEDER.md`](../documentacion/database/ORIGEN-Y-DECISIONES-SEEDER.md) | De dónde sale cada dato inicial y qué reglas se aplicaron. |

## Requisitos

- Acceso al servidor desde el equipo donde se ejecuta (red de la empresa o VPN).
- `sqlcmd` en el `PATH`.
- Autenticación de Windows con un usuario que pueda crear tablas en la base.
- Python 3 (solo librería estándar).

## Crear la base

Se hace una sola vez, contra una base vacía. Desde `docs/Database/`:

```bash
py scripts/ejecutar_schema_y_seeders.py --server 10.10.40.85,1433
```

El script:

1. Aplica `esquema/ESQUEMA_NEXO.sql`: crea las 46 tablas en el esquema `dbo`, con sus índices,
   llaves foráneas y el trigger de histórico de tarifas de plaza.
2. Aplica `seeder/datos_base_nexo.sql`: usuario de sistema, los 9 mapeos de rol del portal y los
   parámetros del score.
3. Carga los CSV de `seeder/` en el orden de sus llaves foráneas: municipios, bandas de peso, reglas
   Solo-SICETAC, tarifas de plaza, servicios conexos, alias, mapeo de vehículos, rangos de peso de
   contenedor, catálogo de carrocerías y los términos generales del diccionario del cliente.

Las tablas de solicitudes, cotizaciones y demás historial quedan vacías. Las tablas que la
aplicación llena sola (rutas, configuraciones de vehículo, clientes, usuarios) se completan al
usarla.

Opciones:

| Opción | Uso |
|---|---|
| `--solo-schema` | Solo crea las tablas. |
| `--solo-datos` | Solo carga los datos iniciales, sobre tablas ya creadas. Se puede repetir: `datos_base_nexo.sql` inserta solo lo que falta y cada CSV se carga solo si su tabla está vacía. |

Para usar otra base, cambie `NOMBRE_BD` al inicio de `scripts/ejecutar_schema_y_seeders.py`.

**Precios de venta de los servicios conexos.** No se versionan: `servicios_conexos.csv` trae la
columna `PrecioVenta` vacía. Si existe `seeder/servicios_conexos_precios.local.csv` (archivo local,
ignorado por git), el script lo carga en su lugar. Ver [`ORIGEN-Y-DECISIONES-SEEDER.md`](../documentacion/database/ORIGEN-Y-DECISIONES-SEEDER.md).
## Conectar la aplicación

La aplicación lee dos variables de entorno:

| Variable | Valor |
|---|---|
| `ConnectionStrings__Nexo` | Cadena de conexión a `dbTarifario_Nexo_Pruebas` con el usuario de la aplicación. No se escribe en el repositorio. |
| `Database__Esquema` | `dbo`. Es obligatoria: el código no tiene valor por defecto. |

El usuario de la aplicación solo necesita leer y escribir datos (`db_datareader` y
`db_datawriter`). El esquema lo aplica una persona, no la aplicación.

## Cambiar un catálogo inicial

Se edita el CSV correspondiente de `seeder/` (o `datos_base_nexo.sql`) y se documenta el cambio en
[`ORIGEN-Y-DECISIONES-SEEDER.md`](../documentacion/database/ORIGEN-Y-DECISIONES-SEEDER.md). Esto solo afecta a las bases que se creen después;
en una base en uso, el catálogo se modifica desde la aplicación o con un script puntual.

Los CSV van en UTF-8 sin BOM y con las columnas en el mismo orden que su tabla en
`ESQUEMA_NEXO.sql`. La excepción es `mapeo_vehiculos.csv`: lo leen las pruebas del backend por
posición, igual que `rangos_peso_contenedor.csv`, así que en esos dos se conservan las columnas y su
orden actual.

## Cambiar la estructura

El esquema no tiene migraciones. Para un cambio de estructura:

1. Edite `esquema/ESQUEMA_NEXO.sql` para que siga describiendo el esquema completo.
2. Escriba el `ALTER` equivalente, de forma que se pueda ejecutar más de una vez sin error.
3. Aplique ese `ALTER` a mano en cada base existente.

En `ESQUEMA_NEXO.sql` la columna nueva va en el lugar que le corresponde por lógica; en una base
existente el `ALTER` la agrega al final de la tabla. La aplicación lee las columnas por nombre, así
que esa diferencia de orden no la afecta.

No borre y recree tablas que ya tengan datos de operación.
