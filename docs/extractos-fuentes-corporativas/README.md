# S2-HU-10 · Habilitador B8 (parte 1) — Contexto de mercado, extracción puntual

Extracción **puntual** (una sola vez, no sincronización) desde los 4 data warehouses
de solo lectura de la compañía, para dar contexto de mercado al histórico propio de
CabinaFletes (el otro lado de este cruce es `docs/analitica/DICCIONARIO_DATOS.md`,
S2-HU-10.1 / issue #35). El EDA de S2-HU-11 se hace sobre ambos sets.

**Qué es esto y qué NO es:** al igual que su HU hermana, esto es una foto única,
cruda y documentada — no un DW ni un cubo propio, y no impone grano ni variable
objetivo. Esas decisiones son del EDA (S2-HU-11).

## Fecha de corte y volumen (criterio de cierre)

| Archivo | Fuente real | Fecha de corte | Filas | Peso |
|---|---|---|---|---|
| `piso-sicetac-tblRutasSicetac-muestra-202608.csv` | `DWSice.dbo.tblRutasSicetac` (servidor `10.10.40.24`) | 2026-09-01 | 151 (muestra, ver abajo) | 12 KB |
| `tarifa-plaza-tblSAPTarifas-2022-09-01_a_hoy.csv` | `dbSolucionesTI.dbo.tblSAPTarifas` (servidor `tbsdatacenter`) | 2026-09-02 (re-extraído, ver corrección abajo) | 23.020 | 6,8 MB |
| `historico-sap-tblSAPTarifasLog-2022-09-01_a_hoy.csv` | `dbSolucionesTI.dbo.tblSAPTarifasLog` (servidor `tbsdatacenter`) | 2026-09-02 (re-extraído) | 25.318 | 5,1 MB |
| `catalogo-clientes-BUT000-organizaciones.csv` | `DWCubosOperacionesEBS.dbo.BUT000` (servidor `10.10.40.24`) | 2026-09-02 (re-extraído) | 38.995 | 4,5 MB |

Formato: CSV, separador `,`, codificación UTF-8 con BOM. `piso-sicetac` se generó con
`sqlcmd -f o:65001` (sin cambios, nunca tuvo el problema de comillas de abajo).
Los otros 3 se **regeneraron** con PowerShell + ADO.NET + `Export-Csv` (ver
"Corrección de comillas RFC4180" más abajo) — las filas variaron levemente
frente al corte original (2026-09-01) porque son tablas compartidas en
producción que siguen recibiendo escrituras en vivo, no por error de la
extracción.

## Corrección de comillas RFC4180 (hallazgo real de QA, S2-HU-10.2)

**El problema, encontrado por QA:** 3 de los 4 CSV (`tarifa-plaza`,
`historico-sap`, `catalogo-clientes`) se generaron originalmente con `sqlcmd`
uniendo columnas por `,` **sin envolver en comillas los campos de texto
libre**. Cualquier valor con una coma o un salto de línea embebido
desalineaba las columnas de ahí en adelante para cualquier parser CSV
estándar (`pandas`, Excel). Verificado y cuantificado:

| Archivo | Filas físicas con conteo de columnas incorrecto | % |
|---|---|---|
| `tarifa-plaza` | 3.363 de 23.546 | 14,3% |
| `historico-sap` | 4.611 de 25.303 | 18,2% |
| `catalogo-clientes` | 323 de 38.985 | 0,8% |
| `piso-sicetac` | 0 de 151 | 0% (nunca tuvo el problema) |

**Causa raíz identificada:** en `tblSAPTarifas`/`tblSAPTarifasLog`, la
columna `DETALLESTARIFA` (notas de tarifa en texto libre, hasta 1.270
caracteres) trae comas y saltos de línea reales en 2.453 y 490 filas
respectivamente; y la descripción de vehículo compartida por catálogo
(`tblSAPTiposVehiculoTarifario.DESCRIPCION = "Rígidos, Turbo o Sencillo"`)
tiene una coma y se repite en miles de filas por `JOIN`. En `catalogo-clientes`,
la razón social (`NAME_ORG1`/`NAME_ORG2`) ocasionalmente trae comas.

**Corrección aplicada:** se regeneraron los 3 archivos con PowerShell +
ADO.NET (`System.Data.SqlClient`) + `Export-Csv -NoTypeInformation -Encoding UTF8`,
que aplica el escapado RFC4180 correcto automáticamente (comillas alrededor de
cada campo, comillas internas duplicadas) — en vez de construir el CSV a mano.
Cada archivo se releyó con `Import-Csv` inmediatamente después de escribirlo
para confirmar que el número de filas y columnas cuadra exacto, cero
desalineación. `piso-sicetac` no se tocó (nunca tuvo el problema, y su
período vigente ya rotó de `202608` a uno posterior — no se puede re-extraer
el mismo período histórico).

**Efecto secundario del cambio de herramienta**: `DEVOLUCION`, `CONSOLIDADO` y
`EXCLUSIVO` (banderas booleanas) ahora se ven como `True`/`False` en vez de
`1`/`0` en `tarifa-plaza` y `historico-sap` — es solo un cambio de formato de
`Export-Csv`, no un cambio de dato.

**Lección para futuras extracciones de este tipo:** `sqlcmd` con `-s ","` NO
es un generador de CSV real — solo concatena columnas con el separador, sin
escapar nada. Para cualquier columna de texto libre, usar `Export-Csv` de
PowerShell (o una librería CSV real) en vez de construir el archivo a mano
con `sqlcmd`.

## Qué trae cada archivo (columnas y para qué sirven)

### `piso-sicetac-tblRutasSicetac-muestra-202608.csv`

| Columna | Para qué sirve |
|---|---|
| `ID` | Identificador de la fila en origen, para verificarla contra la tabla real si hace falta |
| `PERIODO` | Mes de vigencia de la matriz (`AAAAMM`) — en este extracto siempre `202608` |
| `CONFIGURACION` | Configuración de ejes del vehículo (ej. `CA`, `3S3`, `2_5`) — una de las causas de que una misma ruta tenga múltiples valores |
| `VALOR` | El piso regulatorio en pesos, el dato central de esta fuente |
| `NOMBRERUTA` | Texto libre `"ORIGEN - DESTINO"`, tal como lo usa el motor de matching por `LIKE` |
| `NOMBRETIPOCARGA` | Tipo de carga (Sin carga, Granel Sólido, General, Carga Refrigerada, Contenedor cargado/vacío, Granel líquido) |
| `NOMBREUNIDADTRANSPORTE` | Unidad de transporte (Estibas, Furgón, Estacas, Plataforma, Furgón refrigerado, Volco, Portacontenedores, Tanque) |
| `ORIGEN` / `DESTINO` | Código de municipio DANE de origen y destino |
| `VALORHORA` | Componente del valor que se cobra por hora de recorrido (0 cuando la tarifa es fija) |
| `VALORSICECONHORA` | Valor total ya incluyendo el componente por hora |
| `HORASRECORRIDO` / `HORASPACTADAS` | Horas reales estimadas de viaje vs. horas pactadas contractualmente |
| `CONDICIONCARGA` | `CARGADO` o `VACIO` |
| `VIAESTANDAR` | Si la vía es estándar (`SI`) o no (`NO`) — afecta el valor |
| `Muestra` | Columna agregada en esta extracción: `nucleo` (fila representativa) o una de las 5 categorías `rara_*` documentadas más abajo |

### `tarifa-plaza-tblSAPTarifas-2022-09-01_a_hoy.csv`

| Columna | Para qué sirve |
|---|---|
| `ID` | Identificador único de la tarifa en SAP — es la llave que enlaza con `ID_TARIFA` en el histórico |
| `LINEANEGOCIO` / `LineaNegocioDescripcion` | Código y nombre de la línea de negocio — se cruza con el mismo campo capturado en `analitica.lineas_solicitud` |
| `ORIGEN` / `DESTINO` | Código DANE o zona comercial (`ZC_`/`ZTC_`) |
| `VALOR` | El valor de la tarifa en pesos |
| `VIGENCIA` | Fecha hasta la que aplica esa tarifa — cruda, sin la corrección local `AjusteVigenciaPlaza` |
| `CLASECONTENEDOR` / `TAMANOCONTENEDOR` | Si aplica a contenedor: su clase y tamaño (20/40 pies) |
| `RANGOPESOINF` / `RANGOPESOSUP` | Rango de peso al que aplica la tarifa |
| `TIPOVEHICULO` / `VehiculoDescripcion` | Código y nombre del tipo de vehículo (ej. Tractomula, Minimula) |
| `TIPOCARROSERIA` | Tipo de carrocería |
| `PESOLIMITE` | Peso límite adicional |
| `FECHACREACION` / `USUARIOCREACION` | Auditoría de cuándo y quién cargó la tarifa |
| `CLASEVIAJE` | Determina qué lógica de matching aplica en producción: con este campo presente se activa el matching con escalado a zonas; sin él, el matching simple |
| `DEVOLUCION` | Si es una tarifa de devolución (ej. de contenedor vacío) |
| `FECHAMODIFICACION` / `USUARIOMODIFICACION` | Auditoría de la última edición |
| `DETALLESTARIFA` | Detalle adicional en texto libre |
| `CONSOLIDADO` / `EXCLUSIVO` | Banderas de condición del servicio |

### `historico-sap-tblSAPTarifasLog-2022-09-01_a_hoy.csv`

Mismas columnas que `tblSAPTarifas` (salvo `FECHAMODIFICACION`, `USUARIOMODIFICACION`,
`DETALLESTARIFA`, `CONSOLIDADO`, `EXCLUSIVO`, que no existen aquí porque cada
cambio genera una fila nueva en vez de editar una existente), más:

| Columna | Para qué sirve |
|---|---|
| `ID_TARIFA` | Enlaza cada fila del histórico con la fila de `tblSAPTarifas` que la originó |

### `catalogo-clientes-BUT000-organizaciones.csv`

| Columna | Para qué sirve |
|---|---|
| `PARTNER` | Identificador del socio de negocio (cliente) en SAP |
| `TYPE` | Tipo de socio — en este extracto siempre `2` (organización), ya filtrado |
| `BU_GROUP` | Grupo de agrupación del partner |
| `NAME_ORG1` / `NAME_ORG2` | Razón social, hasta 2 líneas |
| `TAXNUM` / `TAXNUMXL` | NIT — la llave de cruce contra el catálogo propio de clientes (`analitica.clientes`) |
| `TEL_NUMBER` | Teléfono de contacto |
| `CRDAT` | Fecha de creación del registro en SAP |
| `VALID_TO` | Hasta cuándo es válido el registro (normalmente sin vencimiento) |

## Por qué el piso SICE-TAC es una muestra y no la tabla completa

`tblRutasSicetac` **no tiene histórico real**: de sus 2.096.003 filas, 2.096.002
pertenecen a un único período (`PERIODO=202608`, el mes vigente al momento de la
extracción) y 1 fila es basura (`PERIODO=0`). No es una tabla que acumule fotos
mensuales — cada mes reemplaza al anterior. `PERIODO` no aporta profundidad
histórica, contrario a lo que su nombre podría sugerir.

La tabla completa (esa única foto) pesa **824 MB** — es la matriz completa de
ruta × vehículo × condición de carga de todo el país, y GitHub rechaza archivos de
más de 100 MB. Se extrajo en su lugar una **muestra representativa de 151 filas**:

- **126 filas "núcleo"**: 6 filas por cada una de las 21 combinaciones reales que
  existen entre tipo de carga, condición (cargado/vacío) y unidad de transporte
  (no las 112 combinaciones teóricas — muchas no aplican, ej. `TANQUE` solo
  aparece con `Granel líquido`).
- **25 filas raras, documentadas** (columna `Muestra`):
  - `rara_ambiguedad_567_candidatos_misma_ruta` (5 filas): la ruta
    Villavicencio↔Buenaventura, en vacío y sin carga, tiene **567 filas
    distintas** en la tabla real (varían por `CONFIGURACION` de ejes y unidad de
    transporte). El motor de propuesta (`PropuestaTarifaAdapter.CandidatoSicetacAsync`)
    hace `LIKE` sobre el texto de la ruta sin filtrar por configuración ni
    unidad — o sea que en producción, para esta ruta, el desempate
    (`SeleccionarSice`) tiene que elegir entre cientos de candidatos con valores
    que van de $1.551.332 a mucho más. No es un caso aislado de 2-3 filas
    ambiguas como se podría suponer — la escala real es mucho mayor.
  - `rara_valor_maximo` (5 filas): los valores más altos reales de toda la tabla
    (hasta $20.822.184, Tumaco↔Santa Marta con carga refrigerada).
  - `rara_valor_minimo` (5 filas): los más bajos por encima de cero ($3.614,
    movimientos intra-municipio en Yumbo).
  - `rara_via_no_estandar` (5 filas): filas con `VIAESTANDAR='NO'`.
  - `rara_horas_recorrido_maximas` (5 filas): las rutas con más horas de
    recorrido reales (hasta 84,78 horas, Puerto Carreño↔Florida Valle).

No se encontraron rutas en bucle (`ORIGEN=DESTINO`), valores nulos/en cero, ni
horas en cero en la tabla completa — a diferencia de `tblSAPTarifas` (ver
`docs/tarifas-plaza-provisionales/README.md`), esta tabla no tiene ese tipo de
suciedad.

## `tblSAPTarifas` y `tblSAPTarifasLog` — completas, no muestreadas

A diferencia del piso SICE-TAC, estas dos sí tienen profundidad histórica real
(`VIGENCIA` desde 2024-01-31) y son de un tamaño manejable (23K y 25K filas), así
que se extrajeron **completas** dentro de la ventana de 4 años solicitada — que en
la práctica cubre el 100% de ambas tablas, porque ninguna tiene registros
anteriores a 2024-01-31 (hallazgo ya documentado en la HU-09.2 anterior).

**Vigencia cruda**: se extrajo la columna `VIGENCIA` tal cual la trae SAP, **sin
aplicar la corrección local** `AjusteVigenciaPlaza` (mecanismo de CabinaFletes que
sobre-escribe vigencias puntuales conocidas como mal cargadas). Decisión explícita
del negocio: quien consuma este extracto debe saber que algunas vigencias aquí
pueden estar mal cargadas en el origen (SAP), tal como se documentó en
`docs/tarifas-plaza-provisionales/README.md` (95,8% de `tblSAPTarifas` completa
está vencida a la fecha de hoy).

**Pendiente de confirmar con negocio** (no se pudo resolver solo con el código):
si una fila en `tblSAPTarifasLog` representa una tarifa que **realmente se aplicó
a un envío** ("tarifa ganada") o si es simplemente un registro de auditoría de
cada vez que se edita el catálogo `tblSAPTarifas`. El código solo confirma que
`tblSAPTarifasLog` es la fuente que el sistema usa y etiqueta explícitamente como
`"historico_sap"` (`PropuestaTarifaAdapter.cs:198`), y que un análisis previo del
equipo (HU-09.2) la usó como proxy de frecuencia real de uso por ruta — pero el
motivo de negocio detrás de cada inserción no está confirmado.

## Catálogo de clientes — solo organizaciones

De los 130.933 socios de negocio en `BUT000`, se extrajeron solo los 38.985 con
`TYPE='2'` (organización/empresa). Se excluyeron los 91.701 de `TYPE='1'` (persona
natural — en su mayoría conductores/contactos individuales, sin valor como
"cliente" para el contexto de mercado que pide esta HU) y los 31 de `TYPE='3'`
(grupo).

## Llaves de cruce contra el histórico propio (criterio de cierre)

| Dimensión | Cómo cruza | Confiabilidad |
|---|---|---|
| **Cliente** | `BUT000.TAXNUM` (NIT) ↔ NIT del catálogo `analitica.clientes` (S2-HU-10.1) | Alta — mismo dato (NIT), sin normalización necesaria |
| **Vehículo/servicio** | `tblSAPTarifas.LINEANEGOCIO` / `CLASEVIAJE` / `CLASECONTENEDOR` ↔ los mismos campos en `analitica.lineas_solicitud` | Alta — esos campos en CabinaFletes ya se capturan como "catálogo replicado de SAP TM" (confirmado en `docs/analitica/DICCIONARIO_DATOS.md`), son los mismos códigos |
| **Ruta** | `tblSAPTarifas.ORIGEN`/`DESTINO` usa código DANE o texto de zona (`ZC_`/`ZTC_`); `tblRutasSicetac.NOMBRERUTA` es texto libre `"ORIGEN - DESTINO"`; `analitica.rutas` (propio) también es texto libre sin código DANE | Media — no hay atajo: cualquier cruce de ruta necesita la misma normalización de nombre de municipio → código DANE que ya usa `TarifarioSapAdapter.CodigosDaneAsync` en el motor de producción. No se replicó esa lógica aquí (decisión: extraer crudo, documentar, dejar el cruce al EDA) |
| **Período** | `tblSAPTarifas`/`tblSAPTarifasLog.VIGENCIA` (fecha) vs `tblRutasSicetac.PERIODO` (entero `AAAAMM`, un solo valor: `202608`) vs fechas de `analitica.cotizaciones`/`analitica.tarifas_historicas` | Alta para SAP (fecha real comparable). Limitada para SICE-TAC: solo se puede cruzar contra situaciones de **agosto 2026** exactamente, no contra períodos anteriores |

## Salvaguardas aplicadas

- **Solo lectura verificada a nivel de motor** (`fn_my_permissions`, no solo por
  disciplina de la consulta):
  - `consultascubos` en `DWSice`: solo `SELECT`. ✅
  - Cuenta Windows en `dbSolucionesTI`: solo `SELECT`. ✅
  - **`consultascubos` en `DWCubosOperacionesEBS` (de donde sale el catálogo de
    clientes): tiene `SELECT`, `INSERT`, `UPDATE`, `DELETE`.** ⚠️ No es de solo
    lectura a nivel de motor — la extracción de este archivo solo ejecutó
    `SELECT`, pero la cuenta en sí podría escribir. **Hallazgo para reportar**:
    valdría la pena pedirle a IT que restrinja esta cuenta a solo lectura.
- **Cero impacto reportado**: todas las consultas fueron acotadas (filtro por
  período/vigencia, columnas explícitas, sin `SELECT *` sobre las tablas grandes).
  La única tabla de escala (`tblRutasSicetac`, 2M filas) no se volcó completa —
  se cambió a muestra representativa apenas se detectó el tamaño (824 MB) antes de
  cualquier intento de commit.
- **Ventana de ejecución**: se corrió en horario laboral normal (no en ventana de
  bajo uso nocturna/fin de semana), decisión explícita del negocio dado el bajo
  volumen real de las consultas ejecutadas (ninguna tocó más de 2M filas, y la más
  grande se abortó y se re-hizo acotada).
- **Servidor de `DwSice`**: confirmado con el negocio que es el mismo servidor
  `10.10.40.24` usado para los demás DW de "Cubos", no un servidor aparte.

## Consultas SQL reproducibles

**Importante para reproducir `tarifa-plaza`, `historico-sap` y `catalogo-clientes`:**
las consultas SELECT de abajo siguen siendo las correctas, pero **no las
ejecutes con `sqlcmd -s ","` redirigido a un `.csv`** (así se generó
originalmente el bug de comillas documentado arriba). Ejecuta el `SELECT` vía
PowerShell + ADO.NET y expórtalo con `Export-Csv -NoTypeInformation -Encoding UTF8`,
que sí escapa comas/comillas/saltos de línea correctamente. `piso-sicetac` sí
se puede seguir generando con `sqlcmd` (nunca tuvo el problema, sus campos son
todos cortos y sin comas).

**Piso SICE-TAC (muestra):**
```sql
;WITH Base AS (
  SELECT * FROM dbo.tblRutasSicetac WHERE PERIODO = 202608
),
Estratos AS (
  SELECT *, ROW_NUMBER() OVER (PARTITION BY NOMBRETIPOCARGA, CONDICIONCARGA, NOMBREUNIDADTRANSPORTE ORDER BY NEWID()) rn
  FROM Base
)
SELECT ID, PERIODO, CONFIGURACION, VALOR, NOMBRERUTA, NOMBRETIPOCARGA, NOMBREUNIDADTRANSPORTE,
  ORIGEN, DESTINO, VALORHORA, VALORSICECONHORA, HORASRECORRIDO, HORASPACTADAS, CONDICIONCARGA, VIAESTANDAR
FROM Estratos WHERE rn <= 6;
-- + 5 filas cada una para: valor máximo, valor mínimo, vía no estándar,
--   horas de recorrido máximas, y la ruta con 567 candidatos (ver sección de
--   hallazgos más arriba para el detalle completo de cada categoría).
```

**Tarifa de plaza:**
```sql
SELECT t.*, v.DESCRIPCION AS VehiculoDescripcion, ln.DESCRIPCION AS LineaNegocioDescripcion
FROM dbo.tblSAPTarifas t
LEFT JOIN dbo.tblSAPTiposVehiculoTarifario v ON v.ID = TRY_CAST(t.TIPOVEHICULO AS INT)
LEFT JOIN dbo.tblSAPLineaNegocio ln ON ln.ID = t.LINEANEGOCIO
WHERE t.VIGENCIA >= '2022-09-01';
```

**Histórico SAP:** igual patrón, sobre `dbo.tblSAPTarifasLog`.

**Catálogo de clientes:**
```sql
SELECT PARTNER, TYPE, BU_GROUP, NAME_ORG1, NAME_ORG2, TAXNUM, TAXNUMXL, TEL_NUMBER, CRDAT, VALID_TO
FROM dbo.BUT000 WHERE TYPE = '2';
```

## Fuera de alcance (declarado)

- No se replicó la lógica de matching de zonas/DANE de `TarifarioSapAdapter` — se
  documentó cómo funciona más arriba en este documento, pero el cruce real entre
  fuentes es trabajo del EDA (S2-HU-11).
- No se aplicó `AjusteVigenciaPlaza` — se entrega `VIGENCIA` cruda, a propósito.
- No se resolvió si `tblSAPTarifasLog` es "tarifa ganada" o "log de auditoría del
  catálogo" — queda pendiente de confirmar con negocio.
- No se decide aquí si esto se convierte en un DW/cubo — eso es post-EDA.
