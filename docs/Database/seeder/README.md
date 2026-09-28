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

Los archivos originales (Excel del negocio, y los CSV intermedios sin normalizar
del todo que salieron en el camino) NO viven aquí — están en `docs_raw/` (raíz
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
  con un adaptador de la app (ver `docs/diccionario-datos-cabinafletes.md`, catálogo
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
`docs_raw/fletes-plaza-normalizado-sice.csv` (la hoja "FLETES PLAZA.xlsx"
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
- **Filas:** 348 (después de deduplicar por `(Origen, Destino, TipoVehiculo)`,
  la llave única real de la tabla).
- **Excepciones aplicadas al construir el CSV:**
  - Las 80 filas de "Devolución de contenedor vacío" no traían un tipo de
    vehículo real (solo tamaño de contenedor + modalidad) — se cargó la
    categoría (ej. `"CNT20/Consolidado"`) en la columna
    `TipoVehiculo` tal cual, aunque semánticamente no sea un tipo de
    vehículo — decisión explícita de negocio.
  - Las 33 filas de la banda de peso "PBV 7500-8000 kg" tampoco tenían un
    código de vehículo confirmado en ningún catálogo — se cargaron con
    `TipoVehiculo = "PBV 7500-8000"`, un valor inventado (no viene de
    `tblSAPTiposVehiculoTarifario` ni de ningún otro catálogo real) que marca
    que es un tipo de vehículo pendiente de dar de alta formalmente.
  - 2 destinos que en el Excel original venían combinados en un solo texto
    ("cajica-zipaquira-tocancipa", "PEREIRA/ARMENIA") se separaron en una fila
    de regla por cada municipio real.

> **Nota para quien arme el catálogo/cruce real de tipos de vehículo
> (`tblSAPTiposVehiculoTarifario` + `tblTipoVehiculosSice` de `DWSice`):**
> además de los tipos de vehículo "normales" (2, 2S2, 2S3, 3S2, 3S3, C2...),
> ese trabajo tiene que contemplar también estos 2 casos que aparecen en
> `reglas_solo_sicetac` pero NO son un tipo de vehículo de ningún catálogo
> existente:
> - Los 4 valores de contenedor/modalidad (`"CNT20/Consolidado"`,
>   `"CNT20/Exclusivo"`, `"CNT40/Consolidado"`,
>   `"CNT40/Exclusivo"`).
> - `"PBV 7500-8000"` — un tipo de vehículo real de negocio (camión
>   liviano PBV 7500-8000 kg) que no calzó con ningún código existente al
>   momento de armar este seeder.
- **Cómo cargarlo en una base nueva:** después de correr `ESQUEMA_NEXO.sql`,
  cargar `reglas_solo_sicetac.csv` con `BULK INSERT` (columnas `Id`, `Origen`,
  `Destino`, `TipoVehiculo` únicamente) contra `db_NEXO.tbl_reglas_solo_sicetac`.

## `tarifas_plaza.csv`

**Poblado UNA SOLA VEZ, sobre la tabla nueva `tbl_tarifas_plaza` (S4-HU-10.1)
— sin sincronización periódica.** Extraído de
`docs_raw/fletes-plaza-normalizado-plaza.csv` (celdas con un valor
numérico real, no "sice") — 271 filas, todas de origen colombiano.

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
- **Dos "familias" de fila, con dimensiones distintas** (por eso
  `ClaseContenedor`/`PesoId`/`TipoVehiculo` son todas `NULL`-ables — una
  tarifa real usa unas u otras, nunca todas a la vez):
  - **97 filas de importación por contenedor** (hoja "Fletes puertos",
    `LineaNegocio="Oferta de Expedición Puerto a interior"`,
    `ClaseViaje="Contenedor"`, `ClaseContenedor="CNT40.HC"`): usan `PesoId`
    (referencia a `tbl_pesos`) **y además** `TipoVehiculo`, derivado de la
    misma banda de peso (`PesoId` 1 y 2 → `"2S2"`, 3 → `"2S3"`, 4 → `"3S2"`,
    5 → `"3S3"`, 6 → `"PBV 7500-8000"`, 7 → `"2"` — mismo criterio que en
    `reglas_solo_sicetac.csv`) — para que `TipoVehiculo` quede lleno en las
    271 filas, no solo en las nacionales.
  - **174 filas nacionales por tipo de vehículo** (hoja "Nacional y Expo",
    sección "SICE" de columnas numéricas — ver nota de la inversión de
    nombres en el análisis original —, `ClaseViaje="Carga Suelta"`): usan
    `TipoVehiculo` y dejan `PesoId`/`ClaseContenedor` en `NULL` (no aplica,
    no son tarifas de contenedor).
    - `LineaNegocio` se resolvió fila por fila: `"Oferta de Expedición
      Interior a Puerto"` si el destino es un puerto (Buenaventura/
      Cartagena/Barranquilla/Santa Marta — devuelve mercancía nacional hacia
      el puerto para exportar), `"Oferta de Expedición Nacional"` en
      cualquier otro caso.
    - `TipoVehiculo`: **Mini Mula → `"2S2"`, Mula → `"3S3"`** (códigos reales
      de `tblSAPTiposVehiculoTarifario`, sin ambigüedad). **Turbo y
      Sencillo se guardan tal cual (`"TURBO"`/`"SENCILLO"`), NO como el
      código oficial `C2`** — el catálogo real agrupa ambos bajo el mismo
      código `C2` ("Rígidos, Turbo o Sencillo"), pero Turbo y Sencillo
      tienen precios distintos para la misma ruta; usar `C2` para los dos
      los habría colapsado en una sola fila y perdido uno de los dos
      precios. Backend debe decidir después si formaliza `TURBO`/`SENCILLO`
      como códigos propios en el catálogo de tipos de vehículo.
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
