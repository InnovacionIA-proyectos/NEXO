# Base de datos del motor de selección de vehículo — Cubicador

Esquema y datos iniciales para las 9 tablas descritas en
`cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`.

**No es una base dedicada.** Vive como su propio esquema SQL,
**`dbo_cubicador`**, dentro de la base **compartida** `dbTarifario_Nexo_Pruebas`
(servidor `10.10.40.85,1433`) — ahí conviven otras tablas de NEXO que no
tienen nada que ver con esto (`CorreoDestinatarios`, `Correos`,
`MargenAjustes`, etc.), separadas por el esquema. No es `db_NEXO` ni la BD
de Cotifly. Ya está aplicado y cargado contra esa base real.

## Contenido de esta carpeta

| Archivo/carpeta | Qué es |
|---|---|
| `DICCIONARIO-DATOS.md` | Para qué sirve cada tabla, en lenguaje llano — qué representa, por qué existe, cómo se relaciona con las demás. |
| `esquema/ESQUEMA_CUBICADOR.sql` | Fuente de la verdad del esquema — crea el esquema `dbo_cubicador` (si no existe) y las 9 tablas dentro de él, con sus FK, `CHECK` e índices. |
| `scripts/ejecutar_schema_y_seeders.py` | Aplica el `.sql` de arriba y carga los 7 CSV de `seeder/` en el orden correcto, contra `dbTarifario_Nexo_Pruebas`/`dbo_cubicador`. |
| `seeder/` | Los 7 CSV con los datos iniciales (vehículos, carrocerías, mercancías, etc.), el script que los generó, y `ORIGEN-Y-DECISIONES.md` (de dónde salió cada dato y qué se decidió). |
| `datos_raw/` | Los 3 Excel fuente originales, más un volcado crudo de cada hoja a CSV, sin normalizar — para auditar contra qué datos reales se tomó cada decisión de `seeder/`. |

## Prerrequisitos

- **SQL Server 2019+** accesible desde donde corras el script. Contra el
  real ya se probó y funciona: `10.10.40.85,1433`, base
  `dbTarifario_Nexo_Pruebas`.
- **`sqlcmd`** instalado y en el `PATH` (versión usada aquí: 17.0.1000.7).
- **Autenticación de Windows** contra ese servidor (el script usa `-E -C`,
  igual que el resto del proyecto NEXO). Si el servidor que vas a usar
  requiere usuario/clave SQL en vez de Windows, hay que ajustar los
  argumentos de `sqlcmd` dentro de `scripts/ejecutar_schema_y_seeders.py`
  (agregar `-U`/`-P`).
- **Python 3** con la librería estándar (no usa nada externo, solo `csv`,
  `subprocess`, `argparse`, `pathlib`).
- Si vas a apuntar a una base/esquema distinto al ya configurado, ajusta
  `NOMBRE_BD`/`NOMBRE_ESQUEMA_SQL` al inicio de
  `scripts/ejecutar_schema_y_seeders.py`.

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

Esto reescribe los 7 CSV desde cero. Los Id son **determinísticos**
(`uuid5` sobre la clave de negocio, ver `ORIGEN-Y-DECISIONES.md`) — el
mismo vehículo/carrocería/mercancía siempre genera el mismo Id, así que
`ejecutar_schema_y_seeders.py --solo-datos` se puede volver a correr sobre
una base ya cargada sin duplicar filas (salta cualquier Id que ya exista).

**Ojo:** esa idempotencia es solo para **filas nuevas**. Si cambiaste el
*contenido* de una fila que ya existía (ej. le cambiaste la categoría o
agregaste una columna nueva a la tabla), el Id sigue siendo el mismo y el
script la salta sin actualizarla — para ese caso hay que recrear las
tablas desde cero (ver "Cambios de estructura" abajo).

Para regenerar los volcados crudos de `datos_raw/` (si cambian los Excel
fuente):

```bash
cd datos_raw
py _extraer_raw.py
```

## Cambios de estructura (columnas nuevas, tablas que ya tienen datos)

El esquema no es incremental (mismo criterio que `ESQUEMA_NEXO.sql`): si
las tablas ya existen, `--solo-schema` falla al intentar crearlas de
nuevo. Si vas a cambiar la estructura de una tabla que ya tiene datos
reales cargados, lo más simple (y lo que se hizo la última vez) es:

1. Borrar las 9 tablas de `dbo_cubicador` en orden que respete las FK
   (hijas antes que padres: `recomendacionVehiculoMercancia`,
   `recomendacionVehiculo`, `relacionVehiculosCarroceria`,
   `reglasIncompatibilidad`, `tiposMercancia`, `categoriaMercancia`,
   `TiposCarroceria`, `vehiculos`, `parametrosMotor`), con
   `DROP TABLE IF EXISTS [dbo_cubicador].[tabla];` por cada una.
2. Editar `esquema/ESQUEMA_CUBICADOR.sql` con la estructura nueva.
3. `py scripts/ejecutar_schema_y_seeders.py --server <host> --solo-schema`
4. Revisar los CSV regenerados (ver sección de arriba) antes de cargarlos.
5. `py scripts/ejecutar_schema_y_seeders.py --server <host> --solo-datos`

Esto es seguro mientras las tablas solo tengan data semilla (como ahora) —
si en el futuro ya hay recomendaciones reales guardadas, borrar deja de
ser una opción sin pensarlo dos veces.

## Notas de la primera carga real (confirmado 2026-10-08)

Ya se aplicó y cargó contra el servidor real
(`10.10.40.85,1433`/`dbTarifario_Nexo_Pruebas`/`dbo_cubicador`), sin
problemas de encoding con tildes/ñ (se probó explícitamente con "Niñera").
Un problema real que sí apareció y ya está corregido: `valor_sql()` en
`scripts/ejecutar_schema_y_seeders.py` adivinaba si un valor era numérico
mirando el texto (`float(texto)`) — códigos de negocio que son puros
dígitos (ej. `CodigoVehiculo="2"`) se mandaban sin comillas, y al mezclarse
en el mismo `INSERT` con filas de texto real (`"2L2"`), SQL Server forzaba
toda la columna a `int` y tronaba. Se corrigió declarando explícitamente
qué columnas son numéricas por tabla (`COLUMNAS_NUMERICAS`/
`COLUMNAS_BOOLEANAS`) en vez de adivinar por contenido — ver el comentario
en esa función para el detalle.
