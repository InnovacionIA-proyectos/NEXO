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
| `tarifa-plaza-tblSAPTarifas-2022-09-01_a_hoy.csv` | `dbSolucionesTI.dbo.tblSAPTarifas` (servidor `tbsdatacenter`) | 2026-09-01 | 23.546 | 5,5 MB |
| `historico-sap-tblSAPTarifasLog-2022-09-01_a_hoy.csv` | `dbSolucionesTI.dbo.tblSAPTarifasLog` (servidor `tbsdatacenter`) | 2026-09-01 | 25.303 | 4,0 MB |
| `catalogo-clientes-BUT000-organizaciones.csv` | `DWCubosOperacionesEBS.dbo.BUT000` (servidor `10.10.40.24`) | 2026-09-01 | 38.985 | 3,4 MB |

Formato: CSV, separador `,`, codificación UTF-8 con BOM (`sqlcmd -f o:65001`).

## Por qué el piso SICE-TAC es una muestra y no la tabla completa

`tblRutasSicetac` **no tiene histórico real**: de sus 2.096.003 filas, 2.096.002
pertenecen a un único período (`PERIODO=202608`, el mes vigente al momento de la
extracción) y 1 fila es basura (`PERIODO=0`). No es una tabla que acumule fotos
mensuales — cada mes reemplaza al anterior. Esto corrige una suposición inicial
equivocada de esta misma sesión de trabajo (se pensó que `PERIODO` daría
profundidad histórica; no es así).

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
  `10.10.40.24` usado para los demás DW de "Cubos" — no un servidor aparte SQL
  Server 2008 R2 como se creyó inicialmente en esta sesión.

## Consultas SQL reproducibles

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
--   horas de recorrido máximas, y la ruta con 567 candidatos (ver script completo
--   ejecutado en esta sesión si se necesita reproducir exacto).
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
  documentó cómo funciona (ver arriba y el hilo de esta sesión), pero el cruce real
  entre fuentes es trabajo del EDA (S2-HU-11).
- No se aplicó `AjusteVigenciaPlaza` — se entrega `VIGENCIA` cruda, a propósito.
- No se resolvió si `tblSAPTarifasLog` es "tarifa ganada" o "log de auditoría del
  catálogo" — queda pendiente de confirmar con negocio.
- No se decide aquí si esto se convierte en un DW/cubo — eso es post-EDA.
