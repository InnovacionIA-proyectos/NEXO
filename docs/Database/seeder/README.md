# Seeders de datos — poblado manual, una sola vez

Esta carpeta guarda los datos ya calculados para poblar catálogos que **no se crean
ni se pueblan por migración de EF Core** (este proyecto no usa migraciones — ver
`docs/Database/ESQUEMA_NEXO.sql` y `DOKPLOY.md`, sección "Esquema de base de datos").
## Orden completo para montar una base nueva

1. **Esquema:** correr `docs/Database/ESQUEMA_NEXO.sql` completo, de una sola
   pasada, contra la base vacía. Crea el esquema `db_NEXO` si no existe y todas
   las tablas, índices, llaves foráneas y el trigger.
2. **Datos base:** correr `docs/Database/datos_base_nexo.sql`. Inserta las filas
   que el modelo declara con `HasData` y que sin migraciones nunca se crean
   solas: el usuario de sistema, los 9 mapeos de rol del portal (sin ellos, quien
   entra desde el portal queda sin rol) y la fila de parámetros del score.
3. **Catálogos por CSV** (`BULK INSERT` u otra carga masiva), en este orden:
   1. `municipios.csv`
   2. `pesos.csv`
   3. `reglas_solo_sicetac.csv`
   4. `tarifas_plaza.csv` (necesita los dos primeros: sus `OrigenMunicipioId`,
      `DestinoMunicipioId` y `PesoId` apuntan a esas tablas)
   5. `servicios_conexos.csv`
   6. `alias_elementos.csv`
4. **Catálogos por script** (cada uno crea la tabla si no existe y la carga con
   `MERGE`, sin duplicar):
   1. `docs/Database/377_mapeo_vehiculo_tarifario.sql` (`mapeo_vehiculos.csv`)
   2. `docs/Database/379_rangos_peso_contenedor.sql` (`rangos_peso_contenedor.csv`),
      siempre después del 377, porque tiene llave foránea hacia el mapeo.

Los demás scripts de `docs/Database/` (`304_*`, `352_*`, `356_*`, `361_*`,
`407_*`) son para bases que ya existían antes de esas tablas o columnas. En una
base nueva no hacen falta, porque `ESQUEMA_NEXO.sql` ya las crea; si se corren
igual, no hacen nada porque son idempotentes. `307_*` es un cambio de datos de roles que no aplica a
una base nueva.

Verificado el 2026-10-27 en un SQL Server 2019 vacío (mismo nivel de
compatibilidad 130 y collation `Modern_Spanish_CI_AS` que la base de pruebas):
los pasos 1, 2 y 4 corren sin errores, y el 2 se puede repetir sin duplicar.

Ninguno de estos catálogos tiene un comando ni un endpoint que los vuelva a poblar
en la aplicación — el cálculo/cruce que produjo los datos se hizo una vez, con
consultas directas contra las fuentes, y quedó congelado en el CSV.

Los archivos originales (Excel del negocio, y los CSV intermedios sin normalizar
del todo que salieron en el camino) NO viven aquí — están en `docs/docs_raw/` (raíz
del repo), aparte de estos seeders ya limpios y listos para cargar.

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
  con un adaptador de la app (ver `docs/Database/diccionario-datos-cabinafletes.md`, catálogo
  `municipios`, para el detalle completo del cruce y las excepciones).
- **Filas:** 3.804 (Colombia 1.128, Venezuela 1.424, Ecuador 1.252) — ya aplicadas
  contra `dbInformesAutomatizados_Pruebas.db_NEXO` (2026-09-26).
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `municipios.csv` con `BULK INSERT` (o el método de carga masiva que
  prefiera quien administre esa base) contra `db_NEXO.tbl_municipios`.

## `pesos.csv`

**Poblado UNA SOLA VEZ (S4-HU-10.1) — solo para el arranque inicial.** Esta
tabla va a tener CRUD propio en la aplicación (`api/pesos` —
crear/editar/eliminar/listar, pendiente de construir por backend); este CSV
es solo el punto de partida (las 8 bandas de negocio ya conocidas) para que la
tabla no arranque vacía — pero **no es un catálogo cerrado**: el sistema
externo real permite peso inferior/superior libres por tarifa puntual (ver
banda 8), así que backend debe construir el CRUD de `tbl_pesos` esperando que
se agreguen bandas nuevas con el tiempo, no asumir que siempre serán estas 8.

- **Tabla destino:** `db_NEXO.tbl_pesos` (columnas: `Id`, `PesoMinimo`,
  `PesoMaximo` — `PesoMaximo` vacío = banda abierta, sin tope superior).
- **De dónde salió:** las 7 bandas de negocio conocidas (3-15000, 15001-18000,
  18001-23000, 23001-30000, 30001-34000, PBV 7500-8000, PBV +10500 — fuente:
  `RANGOS PESOS APP ABRIL 26 2025.xlsx` / `FLETES PLAZA.xlsx`) **más 1 banda
  real verificada en el sistema externo en vivo** (14001-20000, ejemplo
  concreto de una tarifa Cartagena→Medellín ya cargada ahí) que no calzaba con
  ninguna de las 7 — se agregó como banda 8 en vez de forzarla dentro de una
  existente.
- **Filas:** 8.
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `pesos.csv` con `BULK INSERT` (o el método de carga masiva que
  prefiera quien administre esa base) contra `db_NEXO.tbl_pesos`.

## `reglas_solo_sicetac.csv`

**Poblado UNA SOLA VEZ, sobre la tabla `tbl_reglas_solo_sicetac` (ya existía,
no la creó esta HU) — sin sincronización periódica.** Extraído de
`docs/docs_raw/fletes-plaza-normalizado-sice.csv` (la hoja "FLETES PLAZA.xlsx"
del negocio, ya normalizada): cada celda cuyo valor literal era "sice" se
convierte en una fila = "para este origen+destino+tipo, la propuesta se
resuelve solo con SICE-TAC".

- **Tabla destino:** `db_NEXO.tbl_reglas_solo_sicetac`. Columnas del CSV: `Id`
  (Guid nuevo por fila — la tabla NO tiene default de BD para esta columna,
  hay que generarlo), `Origen`, `Destino`, `TipoVehiculo`. `CreadoPor` queda
  `NULL` y `CreatedAt`/`UpdatedAt` usan el default de la tabla
  (`sysdatetimeoffset()`) — al cargar con `BULK INSERT`, apuntar solo a estas
  4 columnas explícitamente (lista de columnas en el `INSERT`/formato), no a
  las 7.
- ⚠️ **Cambio de convención importante:** `Origen`/`Destino` aquí son
  **códigos DANE** (ej. `76109000` = Buenaventura), no el texto libre de
  ciudad que usa hoy el resto del sistema para esta misma tabla (ver
  `clsReglaSoloSicetac.cs`, comentario original: "ciudades, normalizadas"). Es
  una decisión explícita de negocio para alinear esta tabla con el catálogo de
  municipios (`tbl_municipios`) — pero como el resto del código (matching de
  reglas contra una solicitud real) probablemente sigue comparando por texto
  libre, backend tiene que revisar si ese matching necesita cambiar también
  antes de que estas reglas realmente apliquen en producción.
- **Filas:** 260 (después de deduplicar por `(Origen, Destino, TipoVehiculo)`,
  la llave única real de la tabla, y de la corrección de códigos de vehículo —
  ver `DECISIONES-TECNICAS.md` para el detalle completo).
- **Corrección aplicada (post-carga inicial):** se quitaron las 88 filas de
  "Devolución de contenedor vacío" (`CNT20/CNT40` Consolidado/Exclusivo — no
  son un tipo de vehículo real), se renombró `C2` → `2` (Turbo/Sencillo son
  código `2` para SICE-TAC, `C2` es solo el código de SAP Tarifario), y se
  renombró `PBV 7500-8000` → `2L3` (equivalente real confirmado para
  SICE-TAC). Ver `DECISIONES-TECNICAS.md` para el razonamiento completo.
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `reglas_solo_sicetac.csv` con `BULK INSERT` (columnas `Id`, `Origen`,
  `Destino`, `TipoVehiculo` únicamente) contra `db_NEXO.tbl_reglas_solo_sicetac`.

## `tarifas_plaza.csv`

**Poblado UNA SOLA VEZ, sobre la tabla nueva `tbl_tarifas_plaza` (S4-HU-10.1)
— sin sincronización periódica.** Extraído de
`docs/docs_raw/fletes-plaza-normalizado-plaza.csv` (celdas con un valor
numérico real, no "sice") — **107 filas** (originalmente 271; se quitaron
164 filas de la hoja "Fletes Nacional y Expo" — `TURBO`/`SENCILLO`/`MINI
MULA`/`MULA` — que venían de una sección del Excel llamada "SICE": son
referencia contra SICE-TAC, no tarifa real de Plaza, y ya están cubiertas en
`reglas_solo_sicetac.csv` — ver `DECISIONES-TECNICAS.md`), todas de origen
colombiano y todas de la hoja "Fletes puertos".

- **Tabla destino:** `db_NEXO.tbl_tarifas_plaza`. Columnas del CSV: `Id`
  (Guid nuevo, igual razón que en `reglas_solo_sicetac.csv`), `LineaNegocio`,
  `ClaseViaje`, `OrigenMunicipioId`, `DestinoMunicipioId`, `ClaseContenedor`,
  `PesoId`, `TipoVehiculo`, `Valor`, `Vigencia`, `DetallesTarifa`. `CreadoPor`
  queda `NULL` y `CreatedAt`/`UpdatedAt` usan el default de la tabla — al
  cargar con `BULK INSERT`, apuntar solo a estas 11 columnas.
- ⚠️ **`OrigenMunicipioId`/`DestinoMunicipioId` son el `Id` real de
  `tbl_municipios` (el autoincremental, ej. `1012` = Buenaventura) — NO el
  código DANE.** Es una FK real (a diferencia de `reglas_solo_sicetac.csv`,
  que sí usa el código DANE como texto porque ahí `Origen`/`Destino` no
  tienen FK declarada). Si alguna vez se recarga este seeder en otra base,
  hay que resolver Origen/Destino contra el `Id` que tenga esa base en ese
  momento, no reusar los números de este CSV tal cual.
- **Una sola "familia" de fila ahora: importación por contenedor** (hoja
  "Fletes puertos", `LineaNegocio="Oferta de Expedición Puerto a interior"`,
  `ClaseViaje="Contenedor"`, `ClaseContenedor="CNT40.HC"`): usan `PesoId`
  (referencia a `tbl_pesos`) **y además** `TipoVehiculo`, derivado de la
  misma banda de peso (`PesoId` 1 y 2 → `"2S2"`, 3 → `"2S3"`, 4 → `"3S2"`,
  5 → `"3S3"`, 6 → `"2L3"`, 7 → `"2"`).
  - Originalmente había también una "familia" de filas nacionales por tipo
    de vehículo (hoja "Nacional y Expo", `ClaseViaje="Carga Suelta"`,
    `TURBO`/`SENCILLO`/`MINI MULA`/`MULA`) — se eliminó por completo (las
    164 filas, no solo Turbo/Sencillo): toda esa hoja viene de una sección
    del Excel llamada "SICE" — es referencia contra SICE-TAC, no tarifa real
    de Plaza, y esas mismas rutas ya están cubiertas en
    `reglas_solo_sicetac.csv`. Ver corrección 2026-10-01 en
    `DECISIONES-TECNICAS.md` para el detalle completo.
  - `LineaNegocio`, `ClaseViaje` y `ClaseContenedor` guardan el **nombre
    completo** (no el `ID`) de `tblSAPLineaNegocio`/`tblSAPClaseViaje`/
    `tblSAPClaseContenedor` (`dbSolucionesTI`), verificados en vivo — a
    propósito texto y no el `ID` numérico: ese `ID` no significa nada dentro
    de Nexo (es otra base de datos, en otro servidor, sin FK real posible),
    así que guardar el nombre deja la fila legible sin tener que consultar
    el catálogo externo.
- **Vigencia:** el Excel original no trae fecha por fila (es una foto fija de
  ~abril 2025) — se cargó `2026-09-30` para las 271 filas como fecha de
  referencia. Backend/negocio debe revisar si esa fecha sigue teniendo
  sentido antes de que estas tarifas se usen de verdad en producción.
- **DetallesTarifa:** casi ninguna fila traía una nota real en el Excel — se
  usó el texto genérico `"Tarifa migrada desde FLETES PLAZA.xlsx (S4-HU-10.1)"`
  para esos casos, y se conservó la nota original cuando sí existía (ej. la
  fila de Bogotá→Barranquilla con la aclaración "INCLUIDOS TOCANCIPA Y
  ZIPAQUIRA").
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `tarifas_plaza.csv` con `BULK INSERT` contra `db_NEXO.tbl_tarifas_plaza`.
  **`tbl_tarifas_plaza_historico` NO se carga manualmente** — nace vacía y se llena
  sola con el trigger `TRG_tarifas_plaza_historico` cuando alguien actualice
  una tarifa después.

## `servicios_conexos.csv`

**Poblado UNA SOLA VEZ (S4-HU-15.1) — sin sincronización periódica.** Si el
Excel de costos 2026 cambia (nueva ciudad, nuevo puerto, nueva ruta), este CSV
**no se entera solo** — hay que regenerar el archivo a mano.

- **Tabla destino:** `db_NEXO.tbl_servicios_conexos` (reestructurada en esta HU
  para soportar dimensiones de precio distintas por categoría — ver
  `ESQUEMA_NEXO.sql` y `docs/Database/diccionario-datos-cabinafletes.md`, catálogo
  `servicios_conexos`, para el detalle completo de columnas).
- **De dónde salió:** cruce de `docs/docs_raw/conexos costos.xlsx` (2 hojas,
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
- **`PrecioVenta` (S6-HU-38.1/#407):** última columna del CSV, vacía en todas
  las filas. El Excel de costos no trae precio de venta; se llena después desde
  la biblioteca (pantalla o importación de Excel). Vacío = el servicio no tiene
  precio de venta todavía y la cotización usa el costo.
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `servicios_conexos.csv` con `BULK INSERT` (o el método de carga
  masiva que prefiera quien administre esa base) contra
  `db_NEXO.tbl_servicios_conexos`. El CSV no trae todas las columnas de la
  tabla (`ActualizadoPor`, `CreatedAt`, `UpdatedAt` toman su valor por
  defecto y `PrecioVenta` va al final de la tabla), así que la carga debe
  emparejar las columnas por nombre (por ejemplo, con un archivo de formato o
  cargando primero a una tabla temporal), no por posición.

## `alias_elementos.csv`

**Poblado UNA SOLA VEZ, sobre la tabla nueva `tbl_alias_elementos`
(S6-HU-27.1/#315, DATA-2) — sin sincronización periódica.** Extraído de
`cubicador/database/datos_raw/diccionario_configuraciones_vehiculo_RAW.csv`
(diccionario de negocio con 7 columnas: 5 tipos de vehículo y 2 tamaños de
contenedor, cada una con las variantes de texto libre que se han visto en
captura real). Cada celda no vacía se convirtió en una fila de alias, con el
código oficial de su columna.

- **Tabla destino:** `db_NEXO.tbl_alias_elementos`. Columnas del CSV: `Alias`,
  `AliasNormalizado`, `TipoElemento`, `Codigo`, `Activo`. `Id` (Guid nuevo por
  fila, igual razón que en `reglas_solo_sicetac.csv`: la tabla no tiene
  default de BD para esta columna) y `CreatedAt` (default de la tabla) no
  vienen en el CSV — al cargar con `BULK INSERT`, apuntar solo a las 5
  columnas del archivo.
- **`AliasNormalizado` es la clave de comparación**, no lo que se muestra: se
  calculó con el mismo algoritmo que ya usa el backend para normalizar texto
  libre (`clsResolucionDeCatalogos.Normalizar` — mayúsculas, sin tildes,
  `_`/`-` convertidos a espacio, espacios repetidos colapsados). Tiene
  restricción `UNIQUE` en la tabla.
- **`Codigo` es texto, no una FK.** Para vehículo es el mismo código que ya
  comparten Nexo y el Cubicador (`TipoVehiculo`: `2L2`, `C2`, `2S2`, `3S3`,
  `C3`); para contenedor es el código de `tblSAPClaseContenedor` (`CNT40.HC`,
  `CNT20.ST`). No apunta a una fila específica de `tbl_configuraciones_vehiculo`
  porque esa tabla no tiene una única fila "oficial" por tipo — ver
  `DECISIONES-TECNICAS.md`.
- **Filas:** 71 (48 de vehículo + 23 de contenedor), sin alias repetidos ni
  choques entre columnas (verificado: ningún `AliasNormalizado` aparece en más
  de un tipo/código).
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `alias_elementos.csv` con `BULK INSERT` (columnas `Alias`,
  `AliasNormalizado`, `TipoElemento`, `Codigo`, `Activo` únicamente) contra
  `db_NEXO.tbl_alias_elementos`.

## `mapeo_vehiculos.csv`

**Poblado UNA SOLA VEZ, sobre la tabla nueva `tbl_mapeo_vehiculo_tarifario`
(S6-HU-37.1/#377) — sin sincronización periódica.** SICE-TAC y Plaza/SAP
nombran el mismo vehículo con códigos distintos; esta tabla dice, por cada
tipo de vehículo de NEXO, con qué código se busca en cada fuente.

- **Tabla destino:** `db_NEXO.tbl_mapeo_vehiculo_tarifario`. Columnas del CSV:
  - `TipoVehiculo`: código del vehículo en NEXO, el que queda después de
    resolver los alias (`tbl_alias_elementos`). Es la llave y no se repite.
  - `CodigoSicetac` y `NombreSicetac`: valor de `CONFIGURACION` en
    `DWSice.dbo.tblRutasSicetac` y cómo lo describe SICE-TAC (incluye el rango
    de peso bruto vehicular).
  - `CodigoPlazaSap` y `NombreTarifasPlaza`: código y nombre del vehículo en
    Plaza/SAP, el mismo para `tblSAPTarifas` y `tbl_tarifas_plaza`. Vacío = el
    vehículo no tiene tarifa de plaza (solo existe en SICE-TAC).
- **Decisiones confirmadas (2026-10-27):**
  - Sencillo (`C2`): en SICE-TAC y en Plaza/SAP se busca como `2`. En las
    tarifas de plaza hay unas 5.000 filas con `2` y solo 14, todas vencidas,
    con `C2`.
  - Turbo: en Plaza/SAP, `2L1`, `2L2` y `2L3` se buscan como `2L2`; en
    SICE-TAC cada uno conserva su código. El `2_7` del análisis del Excel de un
    cliente (2026-10-26) fue una excepción puntual, confirmada con el
    Administrador de Tarifas.
  - Dobletroque (`C3`): `3` en SICE-TAC, `C3` en Plaza/SAP.
- ⚠️ **Pendiente:** el seeder de `tbl_tarifas_plaza` (`tarifas_plaza.csv`) usa
  `3S2` y `2L3`, que no coinciden con `CodigoPlazaSap` de esos dos vehículos
  (vacío y `2L2`). Puede cambiar el código de plaza de esos dos; ver
  `docs/backlog/2026-10-27_PENDIENTES-MAPEO-VEHICULO.md`.
- **De dónde salió:** construido por Sara Calderón (2026-10-27) revisando cada
  tipo de vehículo contra SICE-TAC y Plaza/SAP.
- **Filas:** 15 (8 con código de plaza).
- **Cómo cargarlo:** no se carga con `BULK INSERT`. El script
  `docs/Database/377_mapeo_vehiculo_tarifario.sql` crea la tabla y la carga con
  `MERGE` (idempotente). Hay que correrlo antes de
  `379_rangos_peso_contenedor.sql`, que tiene llave foránea hacia esta tabla.

## `rangos_peso_contenedor.csv`

**Poblado UNA SOLA VEZ, sobre la tabla nueva `tbl_rangos_peso_contenedor`
(S6-HU-37.3/#379) — sin sincronización periódica.** Dice qué tipo de vehículo
corresponde a un contenedor de 20 o 40 pies según su peso.

- **Tabla destino:** `db_NEXO.tbl_rangos_peso_contenedor`. Columnas del CSV:
  `Contenedor` (`20` o `40`, en pies), `TipoVehiculo` y `PesoMaximo` (kg). El
  nombre del vehículo no se repite aquí: está en el mapeo.
- ⚠️ **`TipoVehiculo` es el código de vehículo de NEXO, el mismo de la columna
  `TipoVehiculo` de `tbl_mapeo_vehiculo_tarifario` (#377)** — no el código de
  SICE-TAC ni el de Plaza/SAP. Para buscar en SICE-TAC o en plaza se traduce
  con el mapeo. Por eso el camión de dos ejes aparece como `C2` y no como `2`:
  `2` es como lo llama SICE-TAC, y el mapeo hace esa traducción (`C2` → `2`).
  Los 6 códigos del CSV (`2S2`, `2S3`, `3S2`, `3S3`, `2L3`, `C2`) existen en
  `mapeo_vehiculos.csv`.
- **De dónde salió:** `docs/documentacion/RANGOS-PESO-CONTENEDORES-NORMALIZADO.xlsx`
  (no se versiona), que normaliza la hoja "Fletes puertos" de
  `docs/docs_raw/rangos de peso contenedores.xlsx`. Confirmado como correcto por
  Sara Calderón (2026-10-27).
- **Fila descartada:** el camión de dos ejes en 40 pies no se carga, porque el
  documento original no le pone tope de peso.
- **Cómo se escoge el vehículo:** entre las filas del tamaño de contenedor, la de
  menor `PesoMaximo` que alcance para el peso. En cada tamaño los topes van
  subiendo sin repetirse, así que no hace falta peso mínimo. Si el peso supera
  el tope más alto (34.000 kg en 40 pies, 30.000 kg en 20 pies), ningún vehículo
  aplica.
- **Filas:** 10 (5 de 40 pies y 5 de 20 pies).
- **No son los rangos de SAP** (`tblRangoPeso`/`tblContenedorRangoPeso`, que NEXO usa hoy): se
  compararon el 2026-10-27 y se decidió mantener estos. La comparación y su efecto
  están en `DECISIONES-TECNICAS.md`.
- **Cómo cargarlo:** no se carga con `BULK INSERT`. El script
  `docs/Database/379_rangos_peso_contenedor.sql` crea la tabla y la carga con
  `MERGE` (idempotente). Hay que correrlo después de
  `377_mapeo_vehiculo_tarifario.sql`, porque `TipoVehiculo` tiene llave foránea
  hacia el mapeo.
