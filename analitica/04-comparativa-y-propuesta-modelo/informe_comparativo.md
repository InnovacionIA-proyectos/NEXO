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
| **Qué es** | Extracto plano de viajes ya ejecutados, entregado directo por el negocio | Set consolidado de S2-HU-10: BD propia (CabinaFletes) + piso SICE-TAC + plaza SAP + clientes |
| **Volumen** | 41.237 filas, 13 meses (abril 2025 → abril 2026, último mes parcial) | 13 solicitudes reales de CabinaFletes (prueba manual del usuario, sin intención de representatividad) + ~2 millones de filas de contexto de mercado (SICE-TAC/SAP), pero solo como catálogo, no ligadas a volumen operativo real |
| **Variable objetivo** | `Flete Producción FO` (precio pagado al transportista) — completa, confiable, foco confirmado del futuro modelo | `ValorFlete` de cotización — solo 13 casos, y no necesariamente el mismo concepto que `Flete Producción FO` (cotización vs. histórico ejecutado) |
| **Contexto de mercado** | Ninguno — no hay SICE-TAC ni SAP en este extracto | Es su aporte central: piso SICE-TAC y plaza SAP, con la lógica de cruce por ruta ya validada parcialmente (ver §3) |
| **Qué NO tiene** | Contexto de mercado, estado de viajes no concluidos, tarifa de venta confiable | Volumen real, y su lado propio no puede crecer mientras CabinaFletes no tenga producción |

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
| **Sesgos / representatividad** | Sesgo de selección y supervivencia **confirmado**: el set solo contiene FOs `Concluido`/`Ejecutado`, excluye 8.034 `Anulado` y 115.216 `En tratamiento` a nivel compañía (H-004 EDA-1); alcance geográfico limitado al corredor Buenaventura (L-06 EDA-1); concentración de clientes baja y saludable, HHI=331 (H-010 EDA-1) | Volumen real bajísimo (13 solicitudes) y **sin intención de cobertura/representatividad** — el usuario las creó a mano con datos al azar solo para probar el mecanismo (L-06 EDA-2, memoria del proyecto); piso SICE-TAC del extracto es una sola foto de un mes, no serie histórica (L-07 EDA-2) |

**Lectura combinada:** los defectos de EDA-1 son de un set con volumen real (errores puntuales, columnas no confiables, sesgo de cobertura) — se pueden acotar y excluir sin perder el set completo. Los de EDA-2 son estructurales del volumen mismo (13 casos de prueba) — no se pueden "limpiar", solo declarar, hasta que exista producción real.

---

## 3. Validación del mecanismo de cruce (aporte específico de EDA-2)

Antes de evaluar viabilidad de objetivos, vale aislar qué tan bien funciona el cruce propio↔mercado que EDA-2 fue diseñado para probar — es el insumo que haría posible cualquier objetivo que combine ambas fuentes:

- **Contra plaza SAP (Tarifario):** funciona. 32/32 líneas `Fuente=Tarifario` encuentran comparación real por ruta normalizada, con una mediana de +30,6% por encima de la mediana SAP (H-005 EDA-2). El mecanismo de traducción de códigos (`LineaNegocio`/`ClaseViaje`) también está confirmado y documentado (H-003 EDA-2).
- **Contra el piso SICE-TAC:** no reproducible con este extracto. 0 de 12 líneas `Fuente=Sicetac` válidas encuentran candidato comparable — no porque el mecanismo falle, sino porque la muestra de 151 filas de `piso-sicetac` no fue diseñada para cubrir las rutas de estas 13 solicitudes de prueba (H-005, H-007 EDA-2).
- **Resolución de ciudad→zona:** el mecanismo real del backend (`tblsapdane`, `tblZonasTarificacionRelacion`) no está en ninguno de los 4 extractos — solo se pudo aproximar con *fuzzy matching* (91,7% de cobertura estimada, H-006 EDA-2), que no es el mecanismo real, solo un estimado práctico.

**Conclusión:** la lógica de cruce con SAP ya está validada y es reutilizable. La lógica de cruce con SICE-TAC sigue sin poder probarse a esta escala — no es una falla, es una limitación de la muestra disponible (ver §7).

---

## 4. Viabilidad por objetivo de predicción

| # | Objetivo | Viabilidad | Evidencia |
|---|---|---|---|
| 1 | Predecir `Flete Producción FO` con variables operativas del histórico (peso, zona/distancia, tipo de vehículo, flota, línea de negocio, componente temporal) | **Viable hoy** | EDA-1: 41.237 filas, grano definido (H-001), variable objetivo confiable, relaciones ya exploradas en el bivariado. Matices: solo aplica a viajes que llegan a concluirse (H-004) y solo al corredor Buenaventura (L-06 EDA-1) |
| 2 | Enriquecer el objetivo 1 con contexto de mercado vigente por fecha (piso SICE-TAC / plaza SAP) | **No viable hoy** | El cruce con SAP funciona (H-005 EDA-2) pero el de SICE-TAC no es reproducible con la muestra actual (H-005/H-007 EDA-2); además el piso SICE-TAC disponible es una foto de un solo mes (L-07 EDA-2), no una serie que se pueda alinear a los 13 meses de EDA-1; faltan las tablas reales de zona (H-003/H-004 EDA-2) |
| 3 | Predecir o asistir la cotización de CabinaFletes (ej. sugerir `ValorFlete` al vendedor) | **No viable hoy** — el dato no soporta predicción útil | Solo 13 solicitudes de prueba manual, sin intención de representatividad (confirmado por el usuario); `ValorMercancia` y `Peso` tienen valores puestos al azar sin forma de separarlos programáticamente del dato real (H-002 EDA-2) |
| 4 | Predecir cancelación o probabilidad de que un viaje no se concrete | **No viable con ninguna fuente actual** — el dato no soporta predicción útil | EDA-1 solo contiene FOs `Concluido`/`Ejecutado` (H-004 EDA-1, sesgo de selección confirmado contra `FactFWOFUFO`); EDA-2 no aporta estados de abandono de mercado real |
| 5 | Predecir margen o rentabilidad por ruta/cliente | **No viable con el dato actual** — el dato no soporta predicción útil | `Tarifa Venta al Cliente` no confiable: 72,2% idéntica a `Flete Producción FO`, confirmado como mal capturada (H-002 EDA-1) |
| 6 | Modelo único que una EDA-1 + EDA-2 (volumen + variable objetivo + contexto de mercado, simultáneos) | **No viable hoy** | El volumen real de EDA-2 (13 casos) no alcanza para entrenar nada, y su `ValorFlete` (cotización) no equivale necesariamente a `Flete Producción FO` (histórico ejecutado) — la unión útil requeriría una extracción nueva (se detalla en el paso de recomendación de captura de datos) |

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

Deseable pero **no disponible hoy** (ver objetivo 2 en §4): piso SICE-TAC / plaza SAP vigente en la fecha del viaje, como variable de contexto de mercado.

---

## 7. Limitaciones consolidadas

| Limitación | Fuente(s) | Evidencia |
|---|---|---|
| Sesgo de selección y supervivencia — solo viajes concluidos | EDA-1 | H-004 EDA-1, L-04/L-05 EDA-1 |
| Alcance geográfico limitado al corredor Buenaventura | EDA-1 | L-06 EDA-1 |
| `Tarifa Venta al Cliente` / margen no confiables | EDA-1 | H-002 EDA-1, L-03 EDA-1 |
| `Distancia Total` no es medición real, solo de tabla/zona | EDA-1 | H-011 EDA-1, L-07 EDA-1 |
| Volumen real de CabinaFletes bajísimo y sin intención de representatividad | EDA-2 | L-06 EDA-2, memoria del proyecto |
| Piso SICE-TAC disponible es una sola foto de un mes, sin profundidad histórica confirmada | EDA-2 | L-07 EDA-2 |
| Traducción de códigos de negocio incompleta (`ClaseContenedor` sin mapeo fijo) | EDA-2 | H-003 EDA-2, L-03 EDA-2 |
| Vigencia real de tarifa SAP no aplicada (`AjusteVigenciaPlaza` ausente) | EDA-2 | L-02 EDA-2 |
| Semántica de `tblSAPTarifasLog` (¿tarifa aplicada o auditoría de ediciones?) no confirmada con negocio | EDA-2 | H-010 EDA-2, L-05 EDA-2 |
| Resolución real de ciudad→zona (`tblsapdane`/`tblZonasTarificacionRelacion`) no disponible, solo aproximada | EDA-2 | H-004/H-006 EDA-2, L-02/L-10 EDA-2 |

---

## Próximos pasos (fuera del alcance de este documento)

1. Propuesta de modelo(s) con objetivo, grano, features y supuestos formalizados como ficha de modelo.
2. Recomendación de captura de datos futura (qué extracción nueva habilitaría el objetivo 2 y 6 de §4).
3. Declaración formal, como entregable propio, de los objetivos donde el dato no soporta predicción útil (ya identificados en §4, objetivos 2 a 6).
