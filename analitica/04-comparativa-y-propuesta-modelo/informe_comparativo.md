# Informe comparativo — EDA-1 (Viajes-Btura) vs. EDA-2 (extracción 4 fuentes)

**Alcance de este documento:** solo el **informe comparativo** (primer punto
del criterio de cierre de S2-HU-11 parte 3). La propuesta de modelo(s) con
sus supuestos, la recomendación de captura de datos futura y la declaración
formal de no-viabilidad se abordan en un paso siguiente — ver
[`README.md`](README.md) de esta carpeta para el estado de cada uno.

Fuentes de este informe: `../02-eda-viajes-btura/reportes/hallazgos.md` y
`limitaciones.md` (EDA-1); `../03-eda-extraccion-4-fuentes/reportes/hallazgos.md`
y `limitaciones.md` (EDA-2). Cada afirmación cita el ID de hallazgo (`H-xx`) o
limitación (`L-xx`) de donde sale — no se repite evidencia nueva aquí, se
contrasta la ya verificada en ambos EDA.

---

## 1. Qué aporta cada fuente

| | EDA-1 — Viajes-Btura (histórico) | EDA-2 — extracción 4 fuentes (`v1.0.0-piloto`) |
|---|---|---|
| **Qué es** | Extracto plano de viajes ya ejecutados, entregado directo por el negocio | Set consolidado de S2-HU-10: BD propia (Nexo) + piso SICE-TAC + plaza SAP + clientes |
| **Volumen** | 41.237 filas, 13 meses (abril 2025 → abril 2026, último mes parcial) | 13 solicitudes reales de Nexo (prueba manual del usuario, sin intención de representatividad) + ~2 millones de filas de contexto de mercado (SICE-TAC/SAP), pero solo como catálogo, no ligadas a volumen operativo real |
| **Variable objetivo** | `Flete Producción FO` (precio pagado al transportista) — completa, confiable, foco confirmado del futuro modelo | `ValorFlete` de cotización — solo 13 casos, y no necesariamente el mismo concepto que `Flete Producción FO` (cotización vs. histórico ejecutado) |
| **Contexto de mercado** | Ninguno — no hay SICE-TAC ni SAP en este extracto | Es su aporte central: piso SICE-TAC y plaza SAP, con la lógica de cruce por ruta ya validada exacta en ambos lados (ver §3) |
| **Qué NO tiene** | Contexto de mercado, estado de viajes no concluidos, tarifa de venta confiable | Volumen real, y su lado propio no puede crecer mientras Nexo no tenga producción |

**Lectura combinada:** las dos fuentes son complementarias, no sustitutas — EDA-1 tiene volumen y variable objetivo pero sin mercado; EDA-2 tiene la lógica de mercado pero sin volumen. Ninguna de las dos, por separado, alcanza para lo que en última instancia se necesita (variable objetivo + volumen + contexto de mercado, alineados en el tiempo). Ver §4 para el detalle de qué es viable con cada una y con la unión.

---

## 2. Comparativa de calidad de datos

Mismas dimensiones de calidad que exige el criterio de cierre del plan de EDA-2 (completitud, unicidad, validez, consistencia, exactitud, sesgos), lado a lado:

| Dimensión | EDA-1 (Viajes-Btura) | EDA-2 (4 fuentes) |
|---|---|---|
| **Completitud** | Sin hallazgos relevantes de vacíos — extracto de 17 columnas, en general poblado | Huecos altos y ya explicados: 92,3% de `solicitudes.Modalidad`/`ProbabilidadCierre`/`ValorEstimado`, 97,7% de `lineas_solicitud.Tipologia` (L-11 EDA-2, hueco conocido de la fase piloto) |
| **Unicidad** | `FO` no es clave única — 128 valores repetidos, es carga consolidada de varios clientes, **confirmado** contra `FactFWOFUFO` (H-001 EDA-1) | Fila fantasma tipo `"------"` en 14/17 archivos crudos, corregida en carga (H-001 EDA-2); 4 ID repetidos en `piso-sicetac_raras`, explicado (H-012 EDA-2) |
| **Validez** | `Distancia Total` es valor de tabla/zona, no medición real — confirmado por desviación estándar = 0 en rutas de alto volumen (H-011 EDA-1) | `TAXNUMXL` 100% nula y `VALID_TO` 100% valor centinela (`9999-12-31`) — sin señal en este extracto (H-009 EDA-2) |
| **Consistencia** | `Tarifa Venta al Cliente` = `Flete Producción FO` exacto en 72,2% de las filas — confirmado como mal capturada en el sistema origen (H-002 EDA-1) | `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` no son "la misma llave" entre lado propio y SAP sin traducción — comparación directa da 0% de match (H-003 EDA-2); 5/18 cotizaciones huérfanas hacia `solicitudes` (H-013 EDA-2) |
| **Exactitud** | 5 FO con flete de $39,6M–$46,8M, error de captura confirmado contra `tblSAPTarifas` (H-005 EDA-1); `$800.000` es valor de relleno del sistema, no tarifa real (H-006 EDA-1) | `ValorMercancia`, `Peso` y 3 líneas `Fuente=Manual` de `cotizacion_lineas` confirmadas por el usuario como valores puestos al azar, sin relación con la realidad (H-002 EDA-2) |
| **Sesgos / representatividad** | Sesgo de selección y supervivencia **confirmado**: el set solo contiene FOs `Concluido`/`Ejecutado`, excluye 8.034 `Anulado` y 115.216 `En tratamiento` a nivel compañía (H-004 EDA-1); alcance geográfico limitado al corredor Buenaventura (L-05 EDA-1); concentración de clientes baja y saludable, HHI=331 (H-010 EDA-1) | Volumen real bajísimo (13 solicitudes) y **sin intención de cobertura/representatividad** — el usuario las creó a mano con datos al azar solo para probar el mecanismo (L-06 EDA-2, memoria del proyecto); el piso SICE-TAC ya cuenta con 27 períodos históricos reales disponibles (`Historicos_SICE/`), pero solo se procesó y verificó el de agosto 2026 (L-07 EDA-2) |

**Lectura combinada:** los defectos de EDA-1 son de un set con volumen real (errores puntuales, columnas no confiables, sesgo de cobertura) — se pueden acotar y excluir sin perder el set completo. Los de EDA-2 son estructurales del volumen mismo (13 casos de prueba) — no se pueden "limpiar", solo declarar, hasta que exista producción real.

---

## 3. Validación del mecanismo de cruce (aporte específico de EDA-2)

Antes de evaluar viabilidad de objetivos, vale aislar qué tan bien funciona el cruce propio↔mercado que EDA-2 fue diseñado para probar — es el insumo que haría posible cualquier objetivo que combine ambas fuentes:

- **Contra plaza SAP (Tarifario):** funciona, y mejor de lo esperado. El motor real (`PropuestaTarifaAdapter.CandidatoSapAsync`) no compara contra un promedio — propone siempre la tarifa SAP vigente **más alta** entre las candidatas. Reproduciendo esa regla exacta (vigencia a la fecha real de cotización + rango de peso + máximo), **28 de 32 líneas `Fuente=Tarifario` (87,5%) coinciden centavo a centavo** con el `ValorFlete` real (H-005 EDA-2). El salto de 20 a 28 vino de descubrir que `tarifa-plaza` tiene 2 tablas de precio paralelas para el área de Bogotá (`ZTC_BOGOTA` genérica y `ZTC_NORTE_BOGOTA` para municipios satélite del norte, con precios sistemáticamente más altos) — el valor exacto de 2 cotizaciones aparece bajo la zona Norte, probablemente porque su destino real era un municipio satélite simplificado a "Bogotá" en el catálogo de rutas (no confirmado con certeza). Ni la aproximación por nombre ni el catálogo real de ciudad→zona pueden anticipar esto, porque ambos resuelven por el nombre de ciudad recibido, no por una localidad más específica (H-005/H-006 EDA-2, L-02/L-10 EDA-2). Las 4 restantes (una sola ruta, Bucaramanga→Bogotá) no son `vigencia` ni zona — su valor exacto aparece en una tarifa real de Devolución Contenedor, y el usuario confirmó directamente que se capturó a mano desde un Excel de referencia al armar la prueba, no la generó el motor en vivo. Con eso, **las 32 de 32 líneas `Fuente=Tarifario` quedan con una explicación** (28 por el algoritmo, 4 por captura manual confirmada). El mecanismo de traducción de códigos (`LineaNegocio`/`ClaseViaje`/`ClaseContenedor`) también está confirmado y verificado al 100% contra los catálogos reales (H-003 EDA-2).
- **Contra el piso SICE-TAC:** también funciona, igual de bien que SAP. El motor real (`SeleccionarSice`) prioriza vía estándar, luego menos horas, luego el valor **más bajo** entre las candidatas — al revés de SAP, que toma la más alta. Con el extracto real completo de agosto 2026 (~2,1 millones de filas, el mismo cubo que usa el sistema, conseguido por el usuario — no la muestra curada de 151 filas), reproduciendo esa regla exacta, **las 12 de 12 líneas `Fuente=Sicetac` (100%) coinciden centavo a centavo** con el `ValorFlete` real (H-005 EDA-2). El "0 de 12" original nunca fue una falla del mecanismo — era 100% limitación de la muestra curada, ahora superada para este período. Pendiente: repetir esta misma verificación en los otros 26 períodos históricos ya disponibles (`Historicos_SICE/`) para cubrir una ventana de tiempo comparable a la de EDA-1 (L-07 EDA-2).
- **Resolución de ciudad→zona:** ya no es aproximación. Con `tblSAPDANE`/`tblZonasTarificacionRelacion` conseguidas, se reprodujo el mecanismo real del backend: 21 de 24 ciudades (87,5%) resuelven, y de paso se encontraron 3 correcciones reales sobre lo que daba la aproximación anterior (*fuzzy matching*) con alta confianza — Mosquera, Soacha y Rionegro resolvían a un código aislado equivocado en vez de su zona metropolitana real (H-004/H-006 EDA-2).

**Conclusión (revisada):** la lógica de cruce funciona en los dos lados — se reprodujo la regla exacta que usa cada motor real (SAP toma siempre el techo permitido; SICE-TAC, la opción más eficiente) y eso explica los patrones que antes parecían "cobrar de más" o "no verificable": no son errores ni márgenes ocultos, son los diseños de cada motor funcionando como se espera. La pregunta que queda abierta ya no es técnica, es de negocio: ¿esas políticas de precios son las que se quieren? Lo único que sigue pendiente es de escala, no de mecanismo: extender la verificación de SICE-TAC (ya probada al 100% en agosto 2026) a los otros 26 períodos históricos disponibles, para tener una serie comparable a los 13 meses de EDA-1 (ver §7).

---

## 4. Viabilidad por objetivo de predicción

| # | Objetivo | Viabilidad | Evidencia |
|---|---|---|---|
| 1 | Predecir `Flete Producción FO` con variables operativas del histórico (peso, zona/distancia, tipo de vehículo, flota, línea de negocio, componente temporal) | **Viable hoy** | EDA-1: 41.237 filas, grano definido (H-001), variable objetivo confiable, relaciones ya exploradas en el bivariado. Matices: solo aplica a viajes que llegan a concluirse (H-004) y solo al corredor Buenaventura (L-05 EDA-1) |
| 2 | Enriquecer el objetivo 1 con contexto de mercado vigente por fecha (piso SICE-TAC / plaza SAP) | **No viable hoy, pero ya no por el mecanismo** | El cruce con SAP (87,5% por el algoritmo + 4 líneas de captura manual confirmada, 32/32 explicadas, H-005 EDA-2) y con SICE-TAC (100%, H-005 EDA-2) ya se reprodujeron exactos — el mecanismo de cruce está resuelto en ambos lados. Lo que sigue bloqueando este objetivo es puro alcance de datos: de los 27 períodos históricos reales de SICE-TAC ya disponibles (`Historicos_SICE/`), solo se procesó y verificó el de agosto 2026 — falta repetir ese procesamiento en los períodos que cubren los 13 meses de EDA-1 (abril 2025-abril 2026) para tener una serie de contexto de mercado alineable (L-07 EDA-2) |
| 3 | Predecir o asistir la cotización de Nexo (ej. sugerir `ValorFlete` al vendedor) | **No viable hoy** — el dato no soporta predicción útil | Solo 13 solicitudes de prueba manual, sin intención de representatividad (confirmado por el usuario); `ValorMercancia` y `Peso` tienen valores puestos al azar sin forma de separarlos programáticamente del dato real (H-002 EDA-2) |
| 4 | Modelo único que una EDA-1 + EDA-2 (volumen + variable objetivo + contexto de mercado, simultáneos) | **No viable hoy** | El volumen real de EDA-2 (13 casos) no alcanza para entrenar nada, y su `ValorFlete` (cotización) no equivale necesariamente a `Flete Producción FO` (histórico ejecutado) — la unión útil requeriría una extracción nueva (se detalla en el paso de recomendación de captura de datos) |

---

## 5. Grano recomendado

**`FO` + `BP Cliente` + `Peso Neto` + `Destino`** — único sobre las 41.237 filas de EDA-1, y confirmado (no solo hipótesis) contra `FactFWOFUFO`: corresponde al nivel `FWO` (contrato con el cliente), la verdadera unidad de negocio dentro de un `FO` que puede agrupar carga consolidada de varios clientes (H-001 EDA-1). Cualquier modelo sobre `Flete Producción FO` debe entrenar y predecir a este grano, no a nivel `FO` crudo.

---

## 6. Variables candidatas

Para el objetivo hoy viable (predecir `Flete Producción FO`, §4 #1):

- `Peso Neto` (confirmado en KG contra `FactFWOFUFO`)
- `Distancia Total` — válida como variable de tarifa/zona, **no** como medición real del viaje (H-011 EDA-1)
- Zona de tarificación de origen/destino, agrupando ciudades satélite (reduce ~113/122 ciudades a ~15-20 zonas) en vez de la ciudad cruda (H-008 EDA-1) — pendiente conseguir el mapeo completo
- Tipo de vehículo: `ID Medio Transporte FO` + `Clase Equipo FOP` — las 2 dimensiones reales tras eliminar la redundancia de las 5 columnas de vehículo/equipo (H-007 EDA-1)
- Flota (Propio/Tercero)
- Línea de negocio / Clase Expedición FWO
- Componente temporal explícito (mes o régimen antes/después de marzo 2026) — necesario por el salto de +16,7% confirmado en esa fecha (H-003 EDA-1)

Deseable, con el mecanismo ya confirmado en ambos lados (SAP y SICE-TAC, ver §3), pero **aún no procesado a la escala de los 13 meses de EDA-1** (ver objetivo 2 en §4): piso SICE-TAC / plaza SAP vigente en la fecha del viaje, como variable de contexto de mercado.

---

## 7. Limitaciones consolidadas

| Limitación | Fuente(s) | Evidencia |
|---|---|---|
| Sesgo de selección y supervivencia — solo viajes concluidos | EDA-1 | H-004 EDA-1, L-03/L-04 EDA-1 |
| Alcance geográfico limitado al corredor Buenaventura | EDA-1 | L-05 EDA-1 |
| `Tarifa Venta al Cliente` / margen no confiables | EDA-1 | H-002 EDA-1 |
| `Distancia Total` no es medición real, solo de tabla/zona | EDA-1 | H-011 EDA-1, L-06 EDA-1 |
| Volumen real de Nexo bajísimo y sin intención de representatividad | EDA-2 | L-06 EDA-2, memoria del proyecto |
| Vigencia histórica real de tarifa SAP no aplicada (`AjusteVigenciaPlaza` ausente; `vigencia` es mutable, solo se ve el valor actual, no una foto del día de cada cotización) | EDA-2 | L-02 EDA-2 |
| `ConfigVehiculoId` vacío en el 100% de las cotizaciones `Fuente=Tarifario` — impide filtrar SAP por tipo de vehículo exacto en esas líneas | EDA-2 | L-13 EDA-2 (nuevo) |
