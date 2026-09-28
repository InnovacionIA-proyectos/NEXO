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
