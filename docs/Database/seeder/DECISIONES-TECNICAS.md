# Decisiones técnicas por seeder

Este archivo documenta el **razonamiento** detrás de cada seeder de
`docs/Database/seeder/` — qué se normalizó, cómo se resolvió cada caso
ambiguo, y por qué se tomó cada decisión. El `README.md` de esta misma carpeta
documenta el "qué es" y "cómo se carga" de cada CSV; este archivo documenta el
"por qué quedó así".

---

## `municipios.csv`

**Decisión: `Id` autoincremental como llave primaria, no `CodigoDane`.**
El código DANE por sí solo no es único — choca entre Venezuela y Ecuador
(ambos países reusan rangos de código). La llave natural real es
`(CodigoDane, Pais)`, pero se dejó `Id` como PK para que el resto del sistema
(FKs de `tbl_tarifas_plaza`, etc.) no tenga que cargar con una clave
compuesta.

**Decisión: sin sincronización automática.** El cruce contra `tblSAPDANE` +
`tblZonasTarificacionRelacion` + `tblSAPTarifas`/`tblSAPTarifasLog` se corrió
una vez, a mano, con consultas SQL directas contra el datacenter corporativo
(`dbSolucionesTI`) — no hay ningún adaptador de la app que lo mantenga
actualizado. Si el DW agrega una ciudad nueva, este CSV no se entera solo.

---

## `pesos.csv`

**Decisión: no es un catálogo cerrado.** Se partió de las 7 bandas de negocio
ya conocidas (`RANGOS PESOS APP ABRIL 26 2025.xlsx`/`FLETES PLAZA.xlsx`), pero
al verificar contra el sistema externo en vivo apareció una tarifa real
(Cartagena→Medellín) con un rango (14001-20000) que no calzaba con ninguna de
las 7. En vez de forzarla dentro de una banda existente, se agregó como banda
8. Conclusión de diseño: el CRUD de `tbl_pesos` que construya backend debe
esperar que se agreguen bandas nuevas con el tiempo, no asumir que siempre
serán las mismas 8.

---

## `reglas_solo_sicetac.csv`

**Decisión original: `Origen`/`Destino` como código DANE, no texto de
ciudad.** Es un cambio de convención respecto al resto del sistema (que usa
texto libre de ciudad para esta misma tabla) — se hizo a propósito para
alinear con `tbl_municipios`, con la advertencia de que el matching de reglas
contra una solicitud real probablemente sigue comparando por texto, así que
backend debe revisar si ese matching necesita cambiar.

**Corrección 2026-10-01 — códigos de vehículo mal asignados en la carga
inicial:**

- **`C2` → `2`.** Las filas de Turbo/Sencillo se habían cargado con el código
  de **SAP Tarifario** (`C2` = "Rígidos, Turbo o Sencillo" en
  `tblSAPTiposVehiculoTarifario`), pero esta tabla es específicamente para
  reglas de **SICE-TAC** (de ahí el nombre `reglas_solo_sicetac`) — y en la
  nomenclatura de SICE-TAC, Turbo/Sencillo corresponden al código `2`, no
  `C2`. Se verificó que no había colisión de llave única `(Origen, Destino,
  TipoVehiculo)` contra las filas que ya usaban `2` para otro caso (camión
  dos ejes PBV +10500 kg) antes de aplicar el cambio — ninguna ruta tenía
  ambos casos a la vez.
- **`PBV 7500-8000` → `2L3`.** Este valor se había dejado como texto literal
  (no un código de catálogo) porque al armar el seeder original no calzaba
  claro con ningún código — se había probado `2L1` sin confirmar. Confirmado
  ahora: el equivalente real para comparar contra SICE-TAC es `2L3` (el
  equivalente contra SAP Tarifario, para referencia, sería `2L2` — distinto
  código en cada catálogo, consistente con que SICE-TAC y SAP Tarifario no
  comparten exactamente la misma taxonomía de vehículos).
- **Se quitaron las 88 filas de contenedor (`CNT20/Consolidado`,
  `CNT20/Exclusivo`, `CNT40/Consolidado`, `CNT40/Exclusivo`).** Estas filas
  nunca fueron un tipo de vehículo real — venían de la hoja "Devolución de
  contenedor vacío" del Excel original, y se habían cargado en la columna
  `TipoVehiculo` solo porque no había dónde más ponerlas. Se decidió que no
  pertenecen a esta tabla de reglas de vehículo y se eliminaron por completo
  (del CSV y de la base real) en vez de mantenerlas como un caso especial.
- **Filas finales: 260** (348 originales − 88 de contenedor, con `C2` y `PBV
  7500-8000` renombrados en las que quedaron, no removidas).
- Aplicado contra `dbInformesAutomatizados_Pruebas.db_NEXO`: tabla vaciada y
  recargada completa desde el CSV corregido (no fue un `UPDATE` selectivo),
  para no arrastrar ninguna fila vieja con el código incorrecto.

**Nombre original en el Excel (`FLETES PLAZA.xlsx` / `fletes-plaza-normalizado-sice.csv`) → código final en el CSV:**

| Nombre/etiqueta original en el Excel | `TipoVehiculo` final |
|---|---|
| "Camion dos ejes PBV- mas de 10500 kg" | `2` |
| "TURBO" | `2` *(era `C2`, corregido)* |
| "SENCILLO" | `2` *(era `C2`, corregido)* |
| "Camion dos ejes livianos PBV 7500 a 8000 kg" | `2L3` *(era el texto literal `"PBV 7500-8000"`, corregido)* |
| "Tracto camion dos ejes con semiremolque dos ejes hasta 15000 kg" | `2S2` |
| "Tracto camion dos ejes con semiremolque dos ejes desde 15001-18000 kg" | `2S2` |
| "MINI MULA" | `2S2` |
| "Tracto camion dos ejes con semiremolque tres hasta 23000 kg" | `2S3` |
| "Tracto camion tres ejes con semiremolque dos hasta 30000 kg" | `3S2` |
| "MULA" | `3S3` |
| "Tracto camion tres ejes con semiremolque tres hasta 34000 kg" | `3S3` |
| *(hoja "Devolucion cont vacios", categoría "Devolución Contenedor Vacío")* | — eliminadas (no son tipo de vehículo) |

---

## `tarifas_plaza.csv`

**Decisión: FK real a `tbl_municipios.Id`, a diferencia de
`reglas_solo_sicetac.csv`.** Aquí `OrigenMunicipioId`/`DestinoMunicipioId` sí
son FK declaradas contra el `Id` autoincremental de `tbl_municipios` (no el
código DANE como texto) — si este seeder se recarga en otra base, hay que
resolver contra el `Id` que tenga esa base en ese momento, no reusar los
números de este CSV tal cual.

**Decisión: dos "familias" de fila con dimensiones distintas** (por eso
`ClaseContenedor`/`PesoId`/`TipoVehiculo` son todas nullable — nunca se usan
las tres a la vez): 97 filas de importación por contenedor (usan `PesoId` +
`TipoVehiculo` derivado de la banda de peso) y 174 filas nacionales por tipo
de vehículo (usan solo `TipoVehiculo`).

**Corrección 2026-10-01 — 82 filas de `TURBO`/`SENCILLO` eran en realidad
solo-SICE, no tarifas reales de Plaza.** Estas filas venían de una sección
del Excel literalmente llamada **"Nacional y Expo - sección 'SICE'"** — un
valor de referencia/comparación contra SICE-TAC, no un precio real de Plaza.
Se confirmó contra la hoja "PLAZA" original (captura de pantalla del
negocio) que esas mismas rutas exactas (Bogotá→Medellín, Bogotá→Cali,
Bogotá→Cartagena, etc.) están marcadas como `"sice"` ahí — es decir, nunca
debieron cargarse en `tarifas_plaza.csv` con un valor numérico. Se verificó
que las 82 rutas ya estaban cubiertas en `reglas_solo_sicetac.csv` (código
`2`, tras su propia corrección) antes de borrarlas, para no perder el dato de
que esas rutas resuelven por SICE-TAC. **Filas finales: 189** (271 − 82).

También se corrigió **`PBV 7500-8000` → `2L3`** (1 fila), mismo criterio que
en `reglas_solo_sicetac.csv`.

**Corrección 2026-10-01 (segunda ronda) — las 82 filas de "Carga Suelta"
restantes (Mini Mula `2S2` + Mula `3S3`) tenían el mismo problema.** Al
revisar más a fondo se confirmó que **toda** la hoja "Fletes Nacional y
Expo" (de donde salían Turbo/Sencillo/Mini Mula/Mula) y toda la hoja
"Devolución cont vacíos" son solo-SICE — ninguna de las dos tiene un precio
real de Plaza, sin excepción. Se verificó que las 82 rutas de Mini Mula/Mula
también están duplicadas como `"sice"` en `reglas_solo_sicetac.csv`
(código `2S2`/`3S3`), confirmando que tampoco debían estar aquí. Se
eliminaron del seeder. **`tarifas_plaza.csv` queda únicamente con la hoja
"Fletes puertos"** (`ClaseViaje = "Contenedor"`, la que realmente tiene
precio) — **filas finales: 107** (189 − 82).

Regla general para este seeder, de ahora en adelante: **`tarifas_plaza.csv`
solo debe contener filas de la hoja "Fletes puertos" con precio real** —
nada de "Devolución cont vacíos" ni "Fletes Nacional y Expo", porque ambas
hojas son 100% solo-SICE.

**Por qué quedan 107 filas y no 97 (el total exacto de la hoja "Fletes
puertos"):** no es un error ni un duplicado — 5 de las 97 filas originales
traían el destino combinado en una sola celda, `"CAJICA-ZIPAQUIRA-TOCANCIPA"`
(mismo caso que en `reglas_solo_sicetac.csv`), y se separaron en una fila por
cada municipio real (Cajicá, Zipaquirá, Tocancipá) — 5 filas combinadas se
convirtieron en 15 (5 bandas de peso × 3 municipios). Cuenta: 97 (fuente) −
5 (combinadas) + 15 (separadas) = **107**. Verificado contra el CSV final:
hay exactamente 15 filas `Buenaventura → {Cajicá, Zipaquirá, Tocancipá}` (5
por cada destino), en vez de 5 filas hacia un destino que no existe como
municipio único.

**Nombre original en el Excel (hoja "Fletes puertos", la primera del libro `FLETES PLAZA.xlsx`) → código final en el CSV** — estas son las 97 filas de importación por contenedor, donde `TipoVehiculo` se deriva de la banda de peso (`PesoId`):

| Nombre/etiqueta original en el Excel | `TipoVehiculo` final |
|---|---|
| "Camion dos ejes PBV- mas de 10500 kg" | `2` |
| "Camion dos ejes livianos PBV 7500 a 8000 kg" | `2L3` *(era el texto literal `"PBV 7500-8000"`, corregido)* |
| "Tracto camion dos ejes con semiremolque dos ejes hasta 15000 kg" | `2S2` |
| "Tracto camion dos ejes con semiremolque dos ejes desde 15001-18000 kg" | `2S2` |
| "Tracto camion dos ejes con semiremolque tres hasta 23000 kg" | `2S3` |
| "Tracto camion tres ejes con semiremolque dos hasta 30000 kg" | `3S2` |
| "Tracto camion tres ejes con semiremolque tres hasta 34000 kg" | `3S3` |

(La hoja "Fletes Nacional y Expo" completa — "TURBO", "SENCILLO", "MINI MULA" y "MULA" — se eliminó del seeder: las 164 filas resultaron ser solo-SICE, no tarifa real de Plaza, ver corrección de arriba. `tarifas_plaza.csv` solo contiene filas de "Fletes puertos".)

**Decisión: `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` guardan el nombre
completo, no el `ID` numérico** de las tablas SAP correspondientes
(`dbSolucionesTI`) — ese `ID` vive en otra base de datos, en otro servidor,
sin FK real posible desde Nexo, así que guardar el texto deja la fila legible
sin depender de consultar el catálogo externo.

**Decisión: `Vigencia` fija en `2026-09-30` para las 271 filas.** El Excel
original es una foto fija de ~abril 2025 sin fecha por fila — se usó una
fecha de referencia única, pendiente de que backend/negocio confirme si sigue
teniendo sentido antes de usar estas tarifas en producción real.

---

## `servicios_conexos.csv`

**Decisión: una sola tabla reestructurada in-place**, no un catálogo padre +
tabla(s) hija de variantes — para no duplicar `Id`/`Activo`/`CreatedAt`/etc.
entre dos tablas y mantener el modelo más simple.

**Decisión: se eliminó `ReglaCobro` y `RowVersion`.** `CostoUnitario` +
`Unidad` alcanza para los 4 tipos de servicio sin necesitar una fórmula JSON
libre; `RowVersion` no se estaba usando para control de concurrencia real.

**Decisión: `CodigoServicio` usa el código real del Excel cuando existe**
(`PRE_BSAT`, `AVT_AUTO`, etc.); cuando el servicio no trae uno propio
(Almacenamiento, ITR, y los 10 de valor único preexistentes), se usa el mismo
valor de `TipoServicio` como código, para no dejarlo vacío.

**Decisión: `Origen`/`Destino`/`Puerto`/`Ruta`/`TipoMaquina` son texto plano
(`NOT NULL DEFAULT 'NO APLICA'`), no FK a `tbl_municipios`.** Se intentó
primero como FK estricto (incluso solo para los servicios logísticos), pero
se abandonó al encontrar casos reales sin equivalente único en
`tbl_municipios`: "Eje Cafetero" (región, no municipio, en Almacenamiento) y
~36% de las filas de acompañamiento vehicular con peajes/corredores viales en
vez de ciudades (ej. "Peaje De Occidente", "Cali - Yumbo", "Ye De Cienaga").
Un FK real habría bloqueado la carga de esas filas.

**Decisión: regla de resolución de nombres de ciudad.** Se normaliza el
nombre (sin tildes, mayúsculas, espacios de más) y se compara contra
`tbl_municipios.Municipio`; si cruza, se guarda el nombre canónico tal como
está en `tbl_municipios`; si no cruza, se guarda el texto tal cual vino del
Excel. Diccionario de alias manuales para casos donde el Excel usa un nombre
coloquial que no cruza por normalización simple: `Bogota`/`Bogota
D.C.`→`BOGOTA, D.C.`, `Cartagena`→`CARTAGENA DE INDIAS`,
`Santamarta`→`SANTA MARTA`, `Medellin (Normal)`→`MEDELLIN`,
`Guachancipa`→`GACHANCIPA` (probable typo), `Pto Tejada`→`PUERTO TEJADA`,
`Carmen De Bolivar`→`EL CARMEN DE BOLIVAR`, `El Bagre
(Zaragoza)`→`EL BAGRE`, `Granada (Meta)`→`GRANADA`, `Buga`→`GUADALAJARA DE
BUGA`.

**Decisión: ciudades con nombre ambiguo no se desambiguan por
departamento.** Cuando el mismo nombre existe en 2+ departamentos de
`tbl_municipios` (ej. "Rionegro" en Antioquia y Santander, "Armenia" en
Antioquia y Quindío), se guarda el nombre del municipio sin elegir
departamento — como el campo es texto libre (no FK), el texto es idéntico en
ambos casos y la ambigüedad no afecta el dato guardado.

**Decisión: `TipoMaquina` combina "tipo de máquina" + "tipo de movimiento"**
del Excel en una sola columna de texto (ej. `"Maquina 3.5 - 2 Movimientos"`),
en vez de 2 columnas separadas, porque ambos datos juntos son los que
determinan el precio de una fila de ITR.

**Decisión: `Unidad` es un campo nuevo que no viene del Excel.** Se asignó
`'Por estiba'` para los servicios logísticos (Almacenamiento, ITR) y `'Por
unidad'` para todo lo demás (adicionales y los 10 de valor único), como
placeholder de negocio hasta que se defina algo más preciso.

**Decisión: los 10 servicios de valor único preexistentes (Cargue,
Descargue, Stand-by, etc.) se sacaron del seeder.** Son datos ficticios de
demostración de `clsDataSeeder.cs` (no vienen del Excel real de costos), así
que no pertenecen a un seeder de datos de negocio reales — quedan fuera a
propósito.

### Mapeo de columnas por tipo de servicio (Excel → `tbl_servicios_conexos`)

**Logísticos → Almacenamiento (por ciudad):** `TIPO DE SERVICIO` → `CodigoServicio`+`TipoServicio` · `CIUDAD` → `Origen` · `COSTO 2026` → `CostoUnitario` · `VIGENCIA` → `Vigencia` · `Unidad` fija `'Por estiba'` · `Destino`/`Puerto`/`Ruta`/`TipoMaquina` = `'NO APLICA'`.

**Logísticos → ITR (por puerto + máquina + movimiento):** `TIPO DE SERVICIO` → `CodigoServicio`+`TipoServicio` · `CIUDAD` → `Origen` · `PUERTO` → `Puerto` · `TIPO DE MAQUINA`+`TIPO DE MOVIMIENTO` → `TipoMaquina` (combinados) · `COSTO 2026` → `CostoUnitario` · `VIGENCIA` → `Vigencia` · `Unidad` fija `'Por estiba'` · `Destino`/`Ruta` = `'NO APLICA'`.

**Adicionales → Precinto/Barra Satelital (por ruta):** `CODIGO SERVICIO` → `CodigoServicio` · `DESCRIPCIÓN SERVICIO` → `TipoServicio` · `RUTA` → `Ruta` · `COSTO 2026` → `CostoUnitario` · `VIGENCIA` → `Vigencia` · `Unidad` fija `'Por unidad'` · `Origen`/`Destino`/`Puerto`/`TipoMaquina` = `'NO APLICA'`.

**Adicionales → Acompañamiento vehicular (por origen-destino):** `CODIGO SERVICIO` → `CodigoServicio` · `DESCRIPCIÓN SERVICIO` → `TipoServicio` · `ORIGEN` → `Origen` · `DESTINO` → `Destino` · `COSTO 2026` → `CostoUnitario` · `VIGENCIA` → `Vigencia` · `Unidad` fija `'Por unidad'` · `Puerto`/`Ruta`/`TipoMaquina` = `'NO APLICA'`.

**Valor único (los 10 preexistentes, fuera del seeder pero documentado por si se vuelven a necesitar):** `Nombre` → `CodigoServicio`+`TipoServicio` · `ValorBase` → `CostoUnitario` · `VigenteHasta` → `Vigencia` · `Categoria`/`Activo` se conservan · `ReglaCobro` eliminado · todas las columnas de dimensión = `'NO APLICA'`.
