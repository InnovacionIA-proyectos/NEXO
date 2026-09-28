# Seeders de datos — poblado manual, una sola vez

Esta carpeta guarda los datos ya calculados para poblar catálogos que **no se crean
ni se pueblan por migración de EF Core** (este proyecto no usa migraciones — ver
`docs/Database/ESQUEMA_NEXO.sql` y `DOKPLOY.md`, sección "Esquema de base de datos").
El flujo para cualquier catálogo de esta carpeta es siempre el mismo, en dos pasos
manuales:

1. Crear la tabla corriendo `docs/Database/ESQUEMA_NEXO.sql` contra la base.
2. Poblarla con el CSV correspondiente de esta carpeta (`BULK INSERT` u otra forma
   de carga masiva).

Ninguno de estos catálogos tiene un comando ni un endpoint que los vuelva a poblar
en la aplicación — el cálculo/cruce que produjo los datos se hizo una vez, con
consultas directas contra las fuentes, y quedó congelado en el CSV.

## `municipios.csv`

**Poblado UNA SOLA VEZ (S4-HU-09.1) — sin sincronización periódica.** Si el Data
Warehouse corporativo (`tblSAPDANE`/`tblZonasTarificacionRelacion`/Tarifario SAP)
agrega una ciudad nueva o cambia una zona después de esta foto, este CSV **no se
entera solo** — hay que volver a correr el cruce a mano y regenerar el archivo. No
existe ningún proceso automático que lo mantenga sincronizado.

- **Tabla destino:** `db_NEXO.tbl_municipios` (columnas: `Id`, `CodigoDane`,
  `Municipio`, `Departamento`, `Pais`, `NombreTarifasPlaza`, `ZonaTarificacion` — ver
  `ESQUEMA_NEXO.sql` para los tipos).
- **De dónde salió:** cruce de `tblSAPDANE` (códigos DANE/DIVIPOLA) +
  `tblZonasTarificacionRelacion` + `tblSAPTarifas`/`tblSAPTarifasLog` del datacenter
  corporativo (`dbSolucionesTI`), corrido una vez con consultas SQL directas — no
  con un adaptador de la app (ver `docs/diccionario-datos-cabinafletes.md`, catálogo
  `municipios`, para el detalle completo del cruce y las excepciones).
- **Filas:** 3.804 (Colombia 1.128, Venezuela 1.424, Ecuador 1.252) — ya aplicadas
  contra `dbInformesAutomatizados_Pruebas.db_NEXO` (2026-09-26).
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `municipios.csv` con `BULK INSERT` (o el método de carga masiva que
  prefiera quien administre esa base) contra `db_NEXO.tbl_municipios`.

## `servicios_conexos.csv`

**Poblado UNA SOLA VEZ (S4-HU-15.1) — sin sincronización periódica.** Si el
Excel de costos 2026 cambia (nueva ciudad, nuevo puerto, nueva ruta), este CSV
**no se entera solo** — hay que regenerar el archivo a mano.

- **Tabla destino:** `db_NEXO.tbl_servicios_conexos` (reestructurada en esta HU
  para soportar dimensiones de precio distintas por categoría — ver
  `ESQUEMA_NEXO.sql` y `docs/diccionario-datos-cabinafletes.md`, catálogo
  `servicios_conexos`, para el detalle completo de columnas).
- **De dónde salió:** cruce de `docs_raw/conexos costos.xlsx` (2 hojas,
  4 tablas: Almacenamiento por ciudad, ITR por puerto+máquina+movimiento,
  Precinto por ruta, Acompañamiento vehicular por origen-destino) contra
  `tbl_municipios`, más los 10 servicios de valor único ya sembrados hoy
  (Cargue, Descargue, Stand-by, Candado satelital, Almacenamiento,
  Distribución multipunto, ITR, Escolta, Reexpedición, Montacargas), que pasan
  a ser su propia variante "genérica" (`Origen`/`Destino`/`Puerto`/`Ruta`/
  `TipoMaquina` = `'NO APLICA'`) dentro de esta misma tabla.
- **Filas:** 1.346 (10 valor-único + 9 Almacenamiento + 21 ITR + 5 Precinto por
  ruta + 1.301 Acompañamiento vehicular por origen-destino) — ya aplicadas
  contra `dbInformesAutomatizados_Pruebas.db_NEXO` (2026-09-28). La tabla
  anterior se limpió por completo (incluyendo ~217 filas de ruido de pruebas
  automatizadas tipo `Dup <guid>` y las filas dependientes de
  `tbl_cotizacion_conexos` que las referenciaban — datos de test, no de
  negocio real).
- ⚠️ **`Origen`/`Destino` son texto plano, NO FK a `tbl_municipios`.** El Excel
  de acompañamiento vehicular mezcla ciudades reales con peajes/corredores
  viales (ej. "Peaje De Occidente", "Cali - Yumbo", "Ye De Cienaga") que no
  corresponden a un único municipio. La regla aplicada: si el nombre cruza
  (normalizado, sin tildes/mayúsculas, con un pequeño diccionario de alias para
  casos como "Bogota"→"BOGOTA, D.C." o "Cartagena"→"CARTAGENA DE INDIAS") se
  guarda el nombre canónico de `tbl_municipios`; si no cruza, se guarda el
  texto tal cual viene del Excel. Del total de filas de acompañamiento
  vehicular, ~55% cruzó limpio, ~36% no tenía equivalente real (peaje/corredor)
  y quedó como texto crudo, ~9% tenía nombre ambiguo (mismo nombre en 2+
  departamentos, ej. "Rionegro" en Antioquia y Santander) resuelto guardando
  solo el nombre del municipio sin desambiguar departamento (no aplica aquí
  porque no es FK).
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `servicios_conexos.csv` con `BULK INSERT` (o el método de carga
  masiva que prefiera quien administre esa base) contra
  `db_NEXO.tbl_servicios_conexos`.

### Mapeo de columnas (`conexos costos.xlsx` → `tbl_servicios_conexos`)

Cada una de las 4 tablas del Excel (2 por hoja) trae sus propias columnas de
origen; ninguna trae las 13 columnas de la tabla destino, así que las que no
aplican para esa tabla se rellenan con el valor fijo indicado.

**Hoja "SERVICIOS LOGISTICOS", tabla 1 — Almacenamiento (por ciudad):**

| Columna Excel | → Columna destino |
|---|---|
| `TIPO DE SERVICIO` (siempre "Almacenamiento") | `CodigoServicio` y `TipoServicio` (mismo valor, no trae código propio) |
| `CIUDAD` | `Origen` (nombre canónico de `tbl_municipios` si cruza) |
| `COSTO 2026` | `CostoUnitario` |
| `VIGENCIA` | `Vigencia` |
| *(no viene del Excel)* | `Unidad` = `'Por estiba'`, `Categoria` = `'logistico'` |
| *(no aplica)* | `Destino`, `Puerto`, `Ruta`, `TipoMaquina` = `'NO APLICA'` |

**Hoja "SERVICIOS LOGISTICOS", tabla 2 — ITR (por puerto + máquina + movimiento):**

| Columna Excel | → Columna destino |
|---|---|
| `TIPO DE SERVICIO` (siempre "ITR") | `CodigoServicio` y `TipoServicio` |
| `CIUDAD` | `Origen` (nombre canónico si cruza) |
| `PUERTO` | `Puerto` (tal cual, ej. `Todos`, `Tcbuen - Sprbun`, `Aguadulce`) |
| `TIPO DE MAQUINA` + `TIPO DE MOVIMIENTO` | `TipoMaquina`, combinadas en un solo texto: `"{máquina} - {movimiento}"` (ej. `"Maquina 3.5 - 2 Movimientos"`) |
| `COSTO 2026` | `CostoUnitario` |
| `VIGENCIA` | `Vigencia` |
| *(no viene del Excel)* | `Unidad` = `'Por estiba'`, `Categoria` = `'logistico'` |
| *(no aplica)* | `Destino`, `Ruta` = `'NO APLICA'` |

**Hoja "SERVICIOS ADICIONALES", tabla 1 — por ruta (hoy solo `PRE_BSAT`):**

| Columna Excel | → Columna destino |
|---|---|
| `CODIGO SERVICIO` | `CodigoServicio` |
| `DESCRIPCIÓN SERVICIO` | `TipoServicio` |
| `RUTA` | `Ruta` (tal cual, ej. `Urbano`, `Colombia a Ecuador`) |
| `COSTO 2026` | `CostoUnitario` |
| `VIGENCIA` | `Vigencia` |
| *(no viene del Excel)* | `Unidad` = `'Por unidad'`, `Categoria` = `'adicional'` |
| *(no aplica)* | `Origen`, `Destino`, `Puerto`, `TipoMaquina` = `'NO APLICA'` |

**Hoja "SERVICIOS ADICIONALES", tabla 2 — por origen-destino (acompañamiento vehicular, `AVT_*`/`AVP_*`):**

| Columna Excel | → Columna destino |
|---|---|
| `CODIGO SERVICIO` | `CodigoServicio` |
| `DESCRIPCIÓN SERVICIO` | `TipoServicio` |
| `ORIGEN` | `Origen` (nombre canónico si cruza, texto crudo si no) |
| `DESTINO` | `Destino` (misma regla) |
| `COSTO 2026` | `CostoUnitario` |
| `VIGENCIA` | `Vigencia` |
| *(no viene del Excel)* | `Unidad` = `'Por unidad'`, `Categoria` = `'adicional'` |
| *(no aplica)* | `Puerto`, `Ruta`, `TipoMaquina` = `'NO APLICA'` |

**Los 10 servicios ya sembrados de valor único** (de `clsDataSeeder.cs`: Cargue,
Descargue, Stand-by, Candado satelital, Almacenamiento, Distribución
multipunto, ITR, Escolta, Reexpedición, Montacargas):

| Columna vieja | → Columna nueva |
|---|---|
| `Nombre` | `CodigoServicio` y `TipoServicio` (mismo valor) |
| `ValorBase` | `CostoUnitario` |
| `VigenteHasta` | `Vigencia` |
| `Categoria`, `Activo` | se conservan igual |
| *(columna eliminada)* | `ReglaCobro` — ya no se guarda esa fórmula JSON, `Costo`+`Unidad` la reemplaza |
| *(columna eliminada)* | `RowVersion` — no se usaba |
| *(no existía)* | `Unidad` = `'Por unidad'` (placeholder), `Origen`/`Destino`/`Puerto`/`Ruta`/`TipoMaquina` = `'NO APLICA'` |

### Decisiones tomadas durante el cruce (y por qué)

1. **Una sola tabla reestructurada in-place**, no un catálogo padre +
   tabla(s) hija de variantes — para no duplicar `Id`/`Activo`/`CreatedAt`/etc.
   entre dos tablas y mantener el CRUD más simple.
2. **Se eliminó `ReglaCobro`** (el JSON `{"tipo":"...","valor":...}`): con
   `CostoUnitario` + `Unidad` alcanza para los 4 tipos de servicio, no se
   necesitaba una fórmula libre.
3. **Se eliminó `RowVersion`**: no se estaba usando para control de
   concurrencia real.
4. **Solo se conservó `ActualizadoPor`** (se descartó agregar un `CreadoPor`
   aparte) para no duplicar el mismo propósito con dos columnas.
5. **`CodigoServicio`**: se usa el código real del Excel (`PRE_BSAT`,
   `AVT_AUTO`, etc.) cuando existe; cuando el servicio no trae uno propio
   (Almacenamiento, ITR, y los 10 de valor único), se usa el mismo valor de
   `TipoServicio` como código, para no dejarlo vacío.
6. **`Origen`/`Destino`/`Puerto`/`Ruta`/`TipoMaquina` son texto plano
   (`NOT NULL DEFAULT 'NO APLICA'`), no FK a `tbl_municipios`.** Se intentó
   primero como FK estricto (incluso solo para los servicios logísticos), pero
   se abandonó al encontrar casos reales sin equivalente único en
   `tbl_municipios`: "Eje Cafetero" (región, no municipio, en Almacenamiento) y
   ~36% de las filas de acompañamiento vehicular con peajes/corredores viales
   en vez de ciudades (ej. "Peaje De Occidente", "Cali - Yumbo", "Ye De
   Cienaga"). Un FK real habría bloqueado la carga de esas filas.
7. **Regla de resolución de nombres de ciudad** (aplicada a `Origen`/`Destino`
   en logísticos y adicionales por igual): se normaliza el nombre (sin
   tildes, mayúsculas, espacios de más) y se compara contra
   `tbl_municipios.Municipio`; si cruza, se guarda el nombre canónico tal como
   está en `tbl_municipios`; si no cruza, se guarda el texto tal cual vino del
   Excel. Se agregó un diccionario de ~10 alias manuales para casos donde el
   Excel usa un nombre coloquial que no cruza por normalización simple:
   `Bogota`/`Bogota D.C.`→`BOGOTA, D.C.`, `Cartagena`→`CARTAGENA DE INDIAS`,
   `Santamarta`→`SANTA MARTA`, `Medellin (Normal)`→`MEDELLIN`,
   `Guachancipa`→`GACHANCIPA` (probable typo), `Pto Tejada`→`PUERTO TEJADA`,
   `Carmen De Bolivar`→`EL CARMEN DE BOLIVAR`, `El Bagre
   (Zaragoza)`→`EL BAGRE`, `Granada (Meta)`→`GRANADA`,
   `Buga`→`GUADALAJARA DE BUGA`.
8. **Ciudades con nombre ambiguo** (el mismo nombre existe en 2+ departamentos
   de `tbl_municipios`, ej. "Rionegro" en Antioquia y Santander, "Armenia" en
   Antioquia y Quindío): como el campo es texto libre (no FK), se guarda el
   nombre del municipio sin desambiguar el departamento — el texto es
   idéntico en ambos casos, así que la ambigüedad no afecta el dato guardado.
9. **`TipoMaquina` combina "tipo de máquina" + "tipo de movimiento"** del
   Excel en una sola columna de texto (en vez de 2 columnas separadas), porque
   ambos datos juntos son los que determinan el precio de una fila de ITR.
10. **`Unidad` es un campo nuevo que no viene del Excel**: se asignó
    `'Por estiba'` para los servicios logísticos (Almacenamiento, ITR) y
    `'Por unidad'` para todo lo demás (adicionales y los 10 de valor único),
    como placeholder de negocio hasta que se defina algo más preciso.
11. **Los 10 servicios ya sembrados de valor único no se tocan en su
    contenido** — solo se remapean a las columnas nuevas (ver tabla arriba) y
    quedan con `'NO APLICA'` en las 5 columnas de dimensión, funcionando como
    su propia variante genérica dentro de la misma tabla.
