# Dataset de tarifas de plaza provisionales, validado por negocio

Rama: `32-s2-hu-092-set-de-datos-provisionales-realistas-validado-por-el-negocio`.

Este documento cubre construir y validar el dataset. Conectar ese dataset al
motor de costeo real (un valor `FuenteFlete.Provisional` o equivalente) queda
fuera de alcance aquí, como trabajo aparte — hoy `ReglaDeCosteo` no consume
este dataset. La validación de las reglas contra esta fuente provisional
también queda pendiente, como trabajo aparte.

## Archivos

- **[`dataset-tarifas-plaza-provisionales.json`](./dataset-tarifas-plaza-provisionales.json)**
  — estructurado, para cuando el motor de costeo lo consuma.
- **[`dataset-tarifas-plaza-provisionales.xlsx`](./dataset-tarifas-plaza-provisionales.xlsx)**
  — documento de revisión para negocio (hojas: Leame, Núcleo, Vigencia vencida,
  Valores extremos, Sin plaza, Hallazgo vehículo vacío, **Aprobación**).

**210 filas en total, todas extraídas de datos reales — nada inventado:** 150 en el
núcleo "utilizable" + 60 casos borde documentados.

## Origen documentado

- **Servidor**: `tbsdatacenter`, base `dbSolucionesTI` — autenticación integrada de
  Windows (`Trusted_Connection=True`), usuario de solo lectura del sistema operativo.
- **Tablas fuente**: `dbo.tblSAPTarifas` (22.965 filas, tarifa de plaza "vigente"
  según SAP) y `dbo.tblSAPTarifasLog` (24.819 filas, histórico de cambios — se usó
  solo para medir frecuencia real de rutas y para el caso "sin plaza").
- **Catálogos usados para nombres legibles**: `dbo.tblSAPTiposVehiculoTarifario`
  (10 tipos de vehículo, específico de tarifario — **no** `tblSAPTiposVehiculo`,
  que es de otro dominio y no resuelve estos códigos) y `dbo.tblSAPLineaNegocio`
  (6 líneas de negocio).
- **Fecha de extracción**: 2026-08-27, con `sqlcmd` (Windows Auth, salida Unicode
  `-u` para no romper tildes).
- **Herramienta**: consultas SQL documentadas íntegras más abajo — cualquiera con
  acceso a `dbSolucionesTI` puede re-ejecutarlas y regenerar el dataset.

## Hallazgo central (mide toda la tabla, no solo la muestra)

| Métrica sobre `tblSAPTarifas` completa (22.965 filas) | Valor |
|---|---|
| Vigencias ya vencidas hoy | **21.994 (95,8%)** |
| Vigencias con fecha absurda (ej. año 3999) | 2 |
| Vigencias realmente normales (usables tal cual) | 969 (4,2%) |
| Valor vacío/nulo | 3 |
| Rutas distintas | 2.489 |
| Vehículo vacío o con código sin catálogo resoluble | 3.918 (17,1%) |

**El 95,8% de lo que SAP marca como tarifa de plaza "vigente" ya está vencido en la
fecha de hoy.** No es un caso aislado — es la situación dominante en la data real, y
es la razón de negocio detrás de esta HU: casi ningún dato en vivo sirve tal cual sin
curaduría, de ahí que se necesite un set "provisional" seleccionado y aprobado a
mano.

## Metodología de muestreo

### Núcleo "utilizable" (150 filas)

De las 969 filas con vigencia real (no vencida, no absurda) y valor no vacío, se
tomaron las de rutas más frecuentes (cruzando contra la frecuencia real en
`tblSAPTarifasLog`), con un tope de **4 filas por ruta** para no dejar que una sola
ruta hiperfrecuente (ej. Buenaventura→Bogotá, con 833 apariciones históricas) copara
todo el cupo y perder variedad real.

Resultado: **47 rutas distintas**, **5 tipos de vehículo** (Tractomula, Liviano
+10.5t, Minimula, Mini Mula Poderosa, Liviano 4t) más filas sin vehículo, **4 líneas
de negocio**, valores entre **$700.000 y $9.949.761** (promedio $4.637.389).

### Casos borde (60 filas, todas reales, ninguna simulada)

| Categoría | Filas | Cómo se extrajo |
|---|---|---|
| Vigencia vencida | 30 | 15 más recientemente vencidas (ORDER BY vigencia DESC) + 15 vencidas hace más tiempo (ORDER BY vigencia ASC, desde 2000-01-01 para no mezclar con las absurdas) |
| Vigencia absurda | 2 | Las únicas 2 filas con vigencia > 2030 en toda la tabla (año 3999, ruta Cartagena↔Ebéjico en ambos sentidos) |
| Valor vacío/nulo | 3 | Las únicas 3 filas de toda la tabla — las 3 son de línea "Devolución Contenedor" |
| Valor máximo | 5 | Los 5 valores más altos reales (hasta $18.000.000, Puerto Triunfo↔Puerto Carreño, tractomula) |
| Valor mínimo | 5 | Los 5 valores más bajos reales por encima de 0 ($100.000, movimientos cortos intra-ciudad) |
| Sin plaza | 15 | Las 15 rutas más frecuentes en el histórico (`tblSAPTarifasLog`) que **hoy no tienen ninguna fila** en `tblSAPTarifas` — ausencia real, no un valor nulo. La más frecuente: Cúcuta↔Buenaventura, 59 apariciones históricas |

No se encontró ninguna fila con `Valor` no numérico (texto en vez de número) en toda
la tabla — a diferencia del dataset de pliego anterior, aquí ese subcaso
específico de "malformado" no existe en los datos reales, así que no se incluyó
inventado.

## Detalle fila por fila de los casos borde

Cada tabla trae el `Id` real de `dbo.tblSAPTarifas` (columna `Id`, autonumérica) —
con eso cualquiera puede ir a la base y verificar la fila exacta. "Raro" siempre
significa lo mismo: **el motor de costeo real (`PropuestaTarifaAdapter`) rechazaría
o trataría distinto esta fila si la encontrara hoy**, y por eso vale la pena tenerla
de ejemplo en el dataset.

### Vigencia vencida (30 filas) — por qué son raras
SAP las tiene cargadas como si fueran una tarifa de plaza normal, pero su columna
`Vigencia` ya pasó. El criterio es el mismo que usa `PropuestaTarifaAdapter.cs` para
decidir si una tarifa de plaza es usable (`Vigencia >= hoy`) — estas 30 lo
incumplen, 15 por poco (`recien_vencida`, vencieron en los últimos días) y 15 desde
hace más tiempo (`vencida_hace_mucho`, hasta 2 años).

| Id | Origen | Destino | Vehículo | Valor | Vigencia | Subcaso |
|---|---|---|---|---|---|---|
| 12699 | ZTC_BARRANQUILLA | ZTC_CARTAGENA | (sin vehículo) | $996.676 | 2026-08-25 | recien_vencida |
| 13008 | ZC_BUENAVENTURA | ZTC_BARRANQUILLA | (sin vehículo) | $10.414.661 | 2026-08-25 | recien_vencida |
| 16769 | ZC_BUENAVENTURA | ZC_BARBOSA (ANT) | Liviano (capacidad 4 ton) | $2.000.000 | 2026-08-25 | recien_vencida |
| 14704 | ZC_BUENAVENTURA | ZC_VALLEDUPAR | (sin vehículo) | $7.000.000 | 2026-08-25 | recien_vencida |
| 12367 | ZTC_CARTAGENA | ZTC_BOGOTA | (sin vehículo) | $5.884.679 | 2026-08-25 | recien_vencida |
| 12366 | ZTC_CARTAGENA | ZTC_BOGOTA | (sin vehículo) | $4.363.670 | 2026-08-25 | recien_vencida |
| 12711 | ZTC_BOGOTA | ZTC_BOGOTA | Liviano mas 10.5 ton (capacidad 8.5 ton) | $400.000 | 2026-08-25 | recien_vencida |
| 15520 | ZC_BUENAVENTURA | ZTC_VABURRA | (sin vehículo) | $5.500.000 | 2026-08-24 | recien_vencida |
| 11233 | ZC_BUENAVENTURA | ZTC_CALI | Liviano mas 10.5 ton (capacidad 8.5 ton) | $1.100.000 | 2026-08-24 | recien_vencida |
| 11260 | ZC_BUENAVENTURA | ZTC_BUCARAMANGA | Liviano (capacidad 4 ton) | $3.300.000 | 2026-08-24 | recien_vencida |
| 11272 | ZC_BUENAVENTURA | ZTC_CALOTO | Liviano (capacidad 4 ton) | $900.000 | 2026-08-24 | recien_vencida |
| 11277 | ZC_BUENAVENTURA | ZTC_ORIENTE_ANT | Liviano mas 10.5 ton (capacidad 8.5 ton) | $2.750.000 | 2026-08-24 | recien_vencida |
| 30216 | ZTC_BOGOTA | ZC_SONSON | Liviano mas 10.5 ton (capacidad 8.5 ton) | $2.700.000 | 2026-08-24 | recien_vencida |
| 11590 | ZTC_BOGOTA | ZTC_NORTE_BOGOTA | Minimula | $1.200.000 | 2026-08-24 | recien_vencida |
| 26587 | ZC_BUENAVENTURA | ZC_YOTOCO | (sin vehículo) | $1.000.000 | 2026-08-22 | recien_vencida |
| 18977 | ZTC_CALI | ZC_BUENAVENTURA | Dobletroque | $1.800.000 | 2024-01-31 | vencida_hace_mucho |
| 18978 | ZTC_CALI | ZC_BUENAVENTURA | Dobletroque | $1.800.000 | 2024-01-31 | vencida_hace_mucho |
| 18979 | ZTC_CALI | ZC_BUENAVENTURA | Dobletroque | $1.800.000 | 2024-01-31 | vencida_hace_mucho |
| 18980 | ZTC_CALI | ZC_BUENAVENTURA | Dobletroque | $1.800.000 | 2024-01-31 | vencida_hace_mucho |
| 18981 | ZTC_CALOTO | ZC_BUENAVENTURA | Dobletroque | $2.000.000 | 2024-01-31 | vencida_hace_mucho |
| 18982 | ZTC_CALOTO | ZC_BUENAVENTURA | Dobletroque | $2.000.000 | 2024-01-31 | vencida_hace_mucho |
| 18983 | ZTC_CALOTO | ZC_BUENAVENTURA | Dobletroque | $2.000.000 | 2024-01-31 | vencida_hace_mucho |
| 18984 | ZTC_CALOTO | ZC_BUENAVENTURA | Dobletroque | $2.000.000 | 2024-01-31 | vencida_hace_mucho |
| 14543 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $6.000.000 | 2024-03-10 | vencida_hace_mucho |
| 14544 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $7.000.000 | 2024-03-10 | vencida_hace_mucho |
| 14546 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $5.000.000 | 2024-03-10 | vencida_hace_mucho |
| 14547 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $7.000.000 | 2024-03-10 | vencida_hace_mucho |
| 14548 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $7.000.000 | 2024-03-10 | vencida_hace_mucho |
| 26096 | ZC_BUENAVENTURA | ZTC_BARRANCABERMEJA | (sin vehículo) | $7.000.000 | 2024-03-10 | vencida_hace_mucho |
| 13536 | ZTC_BUCARAMANGA | ZTC_BOGOTA | Tractomula | $3.500.000 | 2024-04-03 | vencida_hace_mucho |

**¿Hace cuánto caducó el resto de la tabla completa (no solo esta muestra)?** Medido
sobre las 21.994 filas vencidas reales, a fecha de extracción (2026-08-27):

| Hace cuánto caducaron | Filas | % del total vencido |
|---|---|---|
| Últimos 7 días | 95 | 0,4% |
| Entre 8 y 30 días | 538 | 2,4% |
| Entre 1 y 3 meses | 2.591 | 11,8% |
| Entre 3 y 6 meses | 2.065 | 9,4% |
| Entre 6 meses y 1 año | 5.173 | 23,5% |
| Entre 1 y 2 años (desde 2024-01-31, la fecha más antigua de toda la tabla) | 11.508 | 52,4% |

Más de la mitad de las filas vencidas llevan así entre 1 y 2 años — no es descuido
reciente, es una situación vieja y estructural: `tblSAPTarifas` no tiene ningún
registro anterior al 2024-01-31.

### Vigencia absurda (2 filas) — por qué son raras
`Vigencia = 3999-12-31` no es una fecha real de negocio — es un dato de captura mal
cargado (probablemente un valor por defecto o un error de digitación). Son las
únicas 2 filas de toda la tabla con vigencia posterior a 2030, y son una pareja
simétrica (misma ruta en ambos sentidos, cargada el mismo día).

| Id | Origen | Destino | Valor | Vigencia |
|---|---|---|---|---|
| 32657 | ZTC_CARTAGENA | ZC_EBEJICO | $5.000.000 | 3999-12-31 |
| 32658 | ZC_EBEJICO | ZTC_CARTAGENA | $3.001.178 | 3999-12-31 |

### Valor vacío/nulo (3 filas) — por qué son raras
La fila existe (tiene Id, ruta, línea de negocio, vigencia) pero el campo `Valor`
viene vacío — no hay con qué costear esa combinación aunque "exista". Las 3 son de
la misma línea de negocio, **Devolución Contenedor**, lo que sugiere que el vacío no
es al azar sino un patrón de captura de esa línea en particular.

| Id | Origen | Destino | Línea de negocio | Vigencia |
|---|---|---|---|---|
| 26012 | ZTC_BOGOTA | ZTC_BOGOTA | Oferta de Expedición Devolución Contenedor | 2025-05-25 |
| 26397 | ZTC_DUITAMA | ZTC_BOGOTA | Oferta de Expedición Devolución Contenedor | 2025-07-10 |
| 32669 | ZTC_NORTE_BOGOTA | ZTC_BOGOTA | Oferta de Expedición Devolución Contenedor | 2026-08-30 |

### Valor máximo real (5 filas) — por qué son raras
No tienen nada mal cargado — son raras porque son los **valores más altos que
existen en toda la tabla** (hasta $18.000.000), y sirven para probar que el dataset
no se queda corto de rango si alguien prueba una ruta larga/pesada con tractomula.

| Id | Origen | Destino | Vehículo | Valor | Vigencia |
|---|---|---|---|---|---|
| 30965 | ZC_PTO TRIUNFO | ZC_PTO CARRENO | Tractomula | $18.000.000 | 2026-03-10 |
| 30449 | ZTC_CARTAGENA | ZC_IPIALES | (sin vehículo) | $15.700.000 | 2026-07-10 |
| 18296 | ZTC_BUCARAMANGA | ZC_PTO CARRENO | Tractomula | $15.000.000 | 2024-08-31 |
| 18297 | ZTC_BUCARAMANGA | ZC_PTO CARRENO | Tractomula | $15.000.000 | 2024-08-31 |
| 21553 | ZTC_BARRANQUILLA | ZTC_URABA_ANT | Tractomula | $15.000.000 | 2025-01-17 |

### Valor mínimo real (5 filas) — por qué son raras
El extremo opuesto: los **valores más bajos** de toda la tabla por encima de cero
($100.000), todos movimientos cortos o intra-ciudad (ej. Santa Marta↔Santa Marta,
Barranquilla↔Barranquilla). Sirven para probar el otro borde del rango de valores.

| Id | Origen | Destino | Vehículo | Valor | Vigencia |
|---|---|---|---|---|---|
| 14334 | ZC_SANTA MARTA | ZC_SANTA MARTA | (sin vehículo) | $100.000 | 2026-05-10 |
| 20496 | ZC_ARJONA | ZTC_CARTAGENA | (sin vehículo) | $100.000 | 2026-07-10 |
| 21054 | ZTC_BARRANQUILLA | ZTC_BARRANQUILLA | (sin vehículo) | $100.000 | 2026-08-30 |
| 21239 | ZTC_BARRANQUILLA | ZTC_BARRANQUILLA | (sin vehículo) | $100.000 | 2026-08-30 |
| 23064 | ZC_PONEDERA | ZC_BARRANQUILLA | (sin vehículo) | $100.000 | 2025-01-31 |

### Sin plaza (15 rutas) — por qué son raras
Estas no tienen `Id` porque **no existe ninguna fila** para esa ruta en
`tblSAPTarifas` hoy — la rareza no es un dato mal cargado, es una ausencia total. Son
rutas que sí aparecieron con frecuencia real en el histórico (`tblSAPTarifasLog`),
así que no son rutas inventadas ni raras en sí mismas — es que la tarifa vigente para
ellas simplemente no está cargada en este momento.

| Origen | Destino | Frecuencia histórica (tblSAPTarifasLog) |
|---|---|---|
| ZC_CUCUTA | ZC_BUENAVENTURA | 59 |
| ZC_CUCUTA | ZC_CARTAGENA (BOL) | 56 |
| ZC_BARRANQUILLA | ZC_CARTAGENA (BOL) | 40 |
| ZC_BARRANQUILLA | ZC_BARRANQUILLA | 31 |
| ZC_BUCARAMANGA | ZC_BUENAVENTURA | 31 |
| ZC_VILLAVICENCIO | ZC_BUENAVENTURA | 29 |
| ZC_BUCARAMANGA | ZC_CARTAGENA (BOL) | 29 |
| ZC_BARRANQUILLA | ZC_BUENAVENTURA | 26 |
| ZC_CARTAGENA (BOL) | ZC_BARRANQUILLA | 25 |
| ZC_BUENAVENTURA | ZC_CUCUTA | 23 |
| ZC_BUENAVENTURA | ZC_BUCARAMANGA | 22 |
| ZC_CARTAGENA (BOL) | ZC_BUCARAMANGA | 22 |
| ZC_VALLEDUPAR | ZC_CARTAGENA (BOL) | 21 |
| ZC_MEDELLIN | ZC_BARRANQUILLA | 17 |
| ZC_PEREIRA | ZC_BUENAVENTURA | 17 |

## Hallazgo adicional (no pedido explícitamente, pero relevante)

**3.918 de 22.965 filas (17,1%) tienen el campo de vehículo vacío o con un código que
no resuelve contra ningún catálogo** (se probó primero contra `tblSAPTiposVehiculo` —
tabla equivocada, es de otro dominio — y luego contra la correcta,
`tblSAPTiposVehiculoTarifario`). Documentado en la hoja "Hallazgo - vehículo vacío"
del Excel, con conteo completo por código y una muestra de 5 filas reales. No forma
parte del núcleo ni de los casos borde "oficiales" de este trabajo, pero es
información que
el Administrador de Fletes debería conocer al revisar.

## Consultas SQL exactas (reproducibles)

**Núcleo:**
```sql
;WITH Frecuencia AS (
  SELECT Origen, Destino, COUNT(*) AS Apariciones
  FROM dbo.tblSAPTarifasLog GROUP BY Origen, Destino
)
SELECT t.Id, t.Origen, t.Destino, t.TipoVehiculo, v.CODIGO AS VehiculoCodigo, v.DESCRIPCION AS VehiculoDescripcion,
  t.TipoCarroseria, t.LineaNegocio, ln.DESCRIPCION AS LineaNegocioDescripcion,
  t.Valor, CONVERT(varchar(10), t.Vigencia, 23) AS Vigencia,
  t.RangoPesoInf, t.RangoPesoSup, t.ClaseViaje, t.ClaseContenedor, t.TamanoContenedor,
  t.Consolidado, t.Exclusivo, t.Devolucion, ISNULL(f.Apariciones, 0) AS FrecuenciaRuta
FROM dbo.tblSAPTarifas t
LEFT JOIN dbo.tblSAPTiposVehiculoTarifario v ON v.ID = TRY_CAST(t.TipoVehiculo AS INT)
LEFT JOIN dbo.tblSAPLineaNegocio ln ON ln.ID = t.LineaNegocio
LEFT JOIN Frecuencia f ON f.Origen = t.Origen AND f.Destino = t.Destino
WHERE t.Vigencia >= CAST(GETDATE() AS DATE) AND t.Vigencia <= '2030-01-01'
  AND t.Valor IS NOT NULL AND LTRIM(RTRIM(t.Valor)) <> ''
ORDER BY FrecuenciaRuta DESC, t.Id;
```

**Vigencia vencida (recién vencida + vencida hace mucho):**
```sql
SELECT TOP 15 ... FROM dbo.tblSAPTarifas t ...
WHERE t.Vigencia < CAST(GETDATE() AS DATE) AND t.Vigencia > '2020-01-01'
ORDER BY t.Vigencia DESC;   -- recién vencida

SELECT TOP 15 ... WHERE t.Vigencia < CAST(GETDATE() AS DATE) AND t.Vigencia > '2000-01-01'
ORDER BY t.Vigencia ASC;    -- vencida hace mucho
```

**Valores extremos y vacíos:**
```sql
SELECT ... WHERE t.Vigencia > '2030-01-01';                                   -- absurda
SELECT ... WHERE t.Valor IS NULL OR LTRIM(RTRIM(t.Valor)) = '';               -- vacío
SELECT TOP 5 ... WHERE TRY_CAST(t.Valor AS DECIMAL(18,2)) IS NOT NULL
  ORDER BY TRY_CAST(t.Valor AS DECIMAL(18,2)) DESC;                          -- máximo
SELECT TOP 5 ... WHERE TRY_CAST(t.Valor AS DECIMAL(18,2)) > 0
  ORDER BY TRY_CAST(t.Valor AS DECIMAL(18,2)) ASC;                           -- mínimo
```

**Sin plaza:**
```sql
SELECT TOP 15 l.Origen, l.Destino, COUNT(*) AS FrecuenciaHistorica
FROM dbo.tblSAPTarifasLog l
WHERE NOT EXISTS (SELECT 1 FROM dbo.tblSAPTarifas t WHERE t.Origen = l.Origen AND t.Destino = l.Destino)
GROUP BY l.Origen, l.Destino ORDER BY FrecuenciaHistorica DESC;
```

Las consultas completas (con todas las columnas) quedan en el historial de esta
sesión; las de arriba son la versión resumida para lectura rápida. Cualquier persona
con acceso de solo lectura a `dbSolucionesTI` puede pedir las versiones completas o
reescribirlas siguiendo el mismo patrón.

## Validación del negocio (acta)

Según lo acordado con el equipo: **este dataset se autoaprueba internamente**, sin
un Administrador de Fletes externo formal identificado a la fecha de este documento.
La hoja **"Aprobación"** del `.xlsx` tiene el formato de acta (nombre/rol, fecha,
decisión, observaciones) lista para llenar cuando el equipo la revise formalmente —
por ahora queda **pendiente de marcar** esa hoja.

| Campo | Valor |
|---|---|
| Aprobado por | *(pendiente — autoaprobación del equipo, ver hoja "Aprobación" del Excel)* |
| Fecha de revisión | *(pendiente)* |
| Decisión | *(pendiente)* |

## Insumo Gherkin

Nuevo escenario en
[`docs/contrato-integracion/escenarios.feature`](../contrato-integracion/escenarios.feature):
`Propuesta calculada con datos de plaza provisionales realistas`. No existía en el
repo (se confirmó por búsqueda exhaustiva antes de escribirlo). Usa este dataset
como `Dado`, y queda etiquetado dejando explícito que el mecanismo real de consumo
(que `ReglaDeCosteo`/`PropuestaTarifaAdapter` lean de esta fuente) depende de
trabajo aparte, todavía no construido.

## Fuera de alcance (declarado)

- No se agrega `FuenteFlete.Provisional` ni ningún cambio al motor de costeo real
  — queda como trabajo aparte.
- No se valida este dataset contra `ReglaDeCosteo` — esa regla no tiene hoy ninguna
  noción de "fuente provisional" a la que aplicarse.
- La aprobación es autoaprobación del equipo, según lo acordado explícitamente —
  no se simula la firma de un tercero que no existe.
