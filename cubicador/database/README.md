# Base de datos del motor de selección de vehículo — Cubicador

Esquema y datos iniciales para las 9 tablas descritas en
`cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`. Es una base
**propia del Cubicador**, aparte de `db_NEXO` y de la BD de Cotifly.

## Contenido de esta carpeta

| Archivo/carpeta | Qué es |
|---|---|
| `DICCIONARIO-DATOS.md` | Para qué sirve cada tabla, en lenguaje llano — qué representa, por qué existe, cómo se relaciona con las demás. |
| `esquema/ESQUEMA_CUBICADOR.sql` | Fuente de la verdad del esquema — crea la base `cubicador` y las 9 tablas, con sus FK, `CHECK` e índices. |
| `scripts/ejecutar_schema_y_seeders.py` | Aplica el `.sql` de arriba y carga los 7 CSV de `seeder/` en el orden correcto. |
| `seeder/` | Los 7 CSV con los datos iniciales (vehículos, carrocerías, mercancías, etc.) + el script que los generó a partir de los Excel. |
| `datos_raw/` | Los 3 Excel fuente originales, más un volcado crudo de cada hoja a CSV, sin normalizar — para auditar contra qué datos reales se tomó cada decisión de `seeder/`. |

## Prerrequisitos

- **SQL Server 2019** accesible desde donde corras el script (puede ser local, Docker, o un servidor remoto).
- **`sqlcmd`** instalado y en el `PATH`.
- **Autenticación de Windows** contra ese servidor (el script usa `-E`, igual que el resto del proyecto NEXO). Si el servidor que vas a usar requiere usuario/clave SQL en vez de Windows, hay que ajustar los argumentos de `sqlcmd` dentro de `scripts/ejecutar_schema_y_seeders.py` (agregar `-U`/`-P`).
- **Python 3** con la librería estándar (no usa nada externo, solo `csv`, `subprocess`, `argparse`, `pathlib`).

## Cómo correrlo

Primera vez, contra una base vacía (crea el esquema **y** carga los datos):

```bash
py scripts/ejecutar_schema_y_seeders.py --server <host>
# ejemplo: py scripts/ejecutar_schema_y_seeders.py --server localhost
# ejemplo con puerto: py scripts/ejecutar_schema_y_seeders.py --server localhost,1433
```

Solo el esquema, sin tocar datos (por ejemplo, para revisar que las tablas quedaron bien antes de cargar):

```bash
py scripts/ejecutar_schema_y_seeders.py --server <host> --solo-schema
```

Solo cargar los CSV, si el esquema ya existe (por ejemplo, reconstruiste a mano o volviste a correr después de vaciar las tablas):

```bash
py scripts/ejecutar_schema_y_seeders.py --server <host> --solo-datos
```

(Los comandos de arriba se corren desde `cubicador/database/`, la carpeta raíz de este README — no desde dentro de `scripts/`.)

## Si necesitas regenerar los CSV del seeder

Si cambia algo en los Excel fuente o en las reglas de normalización
(categorías, catálogo de carrocerías, etc.), no se edita el CSV a mano —
se ajusta `seeder/_generar_seeders.py` y se vuelve a correr:

```bash
cd seeder
py _generar_seeders.py
```

Esto reescribe los 7 CSV desde cero (con Id nuevos — no es incremental).
Si ya cargaste datos en una base real con los Id anteriores, hay que volver
a aplicar el esquema contra una base limpia antes de recargar, para no
duplicar filas con Id distintos pero mismo contenido.

Para regenerar los volcados crudos de `datos_raw/` (si cambian los Excel
fuente):

```bash
cd datos_raw
py _extraer_raw.py
```

## Advertencias conocidas, sin confirmar contra un servidor real todavía

Esto se armó sin tener un SQL Server 2019 real contra el cual probar en
este entorno — antes de confiar en una carga de producción, verificar:

1. **Tildes/ñ en los textos** (`ComentarioCondicion`, `Motivo`,
   `NombreMercancia`, etc.) — se pasan como argumento `-Q` a `sqlcmd` vía
   `subprocess`, y ya hubo problemas de encoding con `sqlcmd` en este mismo
   proyecto antes. Revisar la primera carga con cuidado.
2. **El esquema no es incremental.** Si ya existen las tablas, el script
   falla al intentar crearlas de nuevo (mismo criterio que
   `ESQUEMA_NEXO.sql`: se corre una sola vez contra una base vacía;
   cualquier cambio de ahí en adelante se hace a mano, editando
   `esquema/ESQUEMA_CUBICADOR.sql` y aplicando el cambio directamente
   contra la base existente).
