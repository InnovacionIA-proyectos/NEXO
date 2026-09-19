# Recomendación de captura de datos futura

Consolida en un solo documento todo lo que se identificó como necesario a lo largo de ambos EDA y de la propuesta de modelos — qué ya se consiguió, qué falta pedir, qué falta mapear/traducir, y qué falta confirmar con negocio. Ver `hallazgos.md`/`limitaciones.md` de ambos EDA y `propuesta_modelos.md` para el detalle y la evidencia de cada punto (H-0xx/L-0x).

---

## 1. Ya conseguido hasta ahora — no volver a pedirlo

| Tabla/catálogo | Para qué sirve | Estado |
|---|---|---|
| `tblSAPClaseContenedor` | Traduce `ClaseContenedor` | ✅ Obtenida y verificada (H-003/L-03 EDA-2) |
| `tblsapdane` | Códigos DANE por ciudad | ✅ Obtenida y verificada (H-004/H-006 EDA-2) |
| `tblZonasTarificacionRelacion` | Agrupa ciudades en zonas de tarificación | ✅ Obtenida y verificada (H-004/H-006/H-008 EDA-1, L-10 EDA-2) |
| `tblSAPTiposVehiculoTarifario` | Traduce tipo de vehículo propio→SAP | ✅ Obtenida y verificada |
| `tblSAPTiposCarroseria` | Traduce `TipoCarroceria` | ✅ Obtenida y verificada (H-017/L-12 EDA-2) |
| `tblSAPLineaNegocio` / `tblSAPClaseViaje` | Confirman la traducción fija ya conocida de `LineaNegocio`/`ClaseViaje` | ✅ Obtenida, reconfirma el diccionario del backend |
| `tblRangoPeso` / `tblContenedorRangoPeso` | Exploradas como posible sustituto del vehículo faltante | ✅ Obtenida — no sirvió para ese propósito, pero confirmó que esas rutas tarifican por peso, no por vehículo |
| `tblParametros` | Parámetros generales del sistema | ✅ Obtenida — sin uso identificado para este EDA |
| Histórico oficial de SICE-TAC (excels de la página oficial, `Historicos_SICE/`) | Insumo para el Modelo 1 (pronóstico) | ✅ Descargado (26 archivos, ene-2025 a sep-2026) — **pendiente de evaluar si resuelve la profundidad histórica** (ver §2) |

---

## 2. Pendiente de conseguir — tablas o campos que siguen faltando

| Qué falta | Para qué | Evidencia |
|---|---|---|
| **Vigencia histórica real de `tarifa-plaza`** (no solo el valor actual de `VIGENCIA`, que es mutable) | Sin esto, no se puede saber qué tarifa SAP estaba vigente en la fecha exacta de un viaje pasado — bloquea la validación de §7 | L-02 EDA-2, confirmado con evidencia directa en H-005 (el campo `vigencia` solo muestra el valor actual, no una foto histórica; aun así, las 32/32 líneas Tarifario terminaron explicadas por otras vías — 28 por el algoritmo real, incluyendo la zona `ZTC_NORTE_BOGOTA`, y 4 por captura manual confirmada) |
| **Evaluación del histórico oficial de SICE-TAC ya descargado** — confirmar si trae series de tiempo reales (varios períodos) o es otra "foto única" repetida | Insumo crítico del Modelo 1 | Pendiente — prompt de evaluación ya entregado |
| **Número de contrato/tarifario aplicado por viaje** en `FactFWOFUFO` | Explicaría la dispersión de tarifas en la misma ruta | L-07 EDA-1. 3 caminos alternativos ya identificados: (1) reconstruir por coincidencia de valor contra `tblSAPTarifas`/`tblSAPTarifasLog`; (2) preguntar a IT/SAP si el sistema transaccional lo guarda; (3) proxy de cercanía al rango legítimo |
| **Marca de "fuente"/mecanismo de fijación de precio** en `FactFWOFUFO` (equivalente a `cotizacion_lineas.Fuente`: manual vs. regla) | Sin esto no se puede distinguir un flete manual/negociado de uno fijado por la regla MAX(SICE-TAC, SAP) — hoy no hay forma de aislarlos de antemano | Nuevo, de la validación de §7 (pregunta abierta #6 en §4.1) |

---

## 3. Pendiente de mapeo/traducción — no son tablas nuevas, es trabajo con lo que ya se tiene

| Qué falta mapear | Detalle |
|---|---|
| Código de vehículo de EDA-1 (`ID Medio Transporte FO`, ej. `TC-3S2`) → `tblSAPTiposVehiculoTarifario` | El esquema de nombres es distinto al de Nexo (`2S3` sin el prefijo `TC-`/`FP-`) — no es un cruce automático, hay que resolverlo antes de poder correr la validación de §7 a escala de EDA-1 |
| Código de equipo de EDA-1 (`Clase Equipo FOP`) → `tblSAPTiposCarroseria`/`tblSAPClaseContenedor` | Mismo problema — nombres propios de EDA-1, no necesariamente iguales a los de Nexo |

---

## 4. Preguntas pendientes para negocio / dueños de la app

Todo lo que sigue son **conversaciones, no extracciones de datos** — se resuelven preguntando, no consultando una tabla. Agrupadas por tema.

### 4.1 Sobre la regla de precio manual (bloquean la HU de validación, §7)

| # | Pregunta | Por qué importa |
|---|---|---|
| 1 | ¿La regla "MAX(SICE-TAC, SAP)" que aplica la persona manualmente cubre el 100% de los viajes históricos, en todas las líneas de negocio y regiones, o solo un subconjunto? | Determina si la validación se puede generalizar a todo `FactFWOFUFO` o solo a una parte |
| 2 | ¿A qué grano decide el precio — por `FO` completo o por `FWO` (cliente dentro de una carga consolidada)? | Define el grano correcto de la reconstrucción |
| 3 | ¿Qué hace la persona cuando ni SICE-TAC ni SAP tienen tarifa vigente para esa ruta/vehículo? | Esos casos no son reconstruibles por regla — hay que saber cómo se resuelven hoy para no tratarlos como error |
| 4 | ¿Es la misma regla en todo el país, o varía por región/línea de negocio? | Relevante ahora que se confirma ampliar `FactFWOFUFO` más allá de Buenaventura |
| 5 | ¿Hay un rezago conocido entre cuándo se actualiza/fija una tarifa SAP (`VIGENCIA`) y cuándo esa persona empieza a aplicarla en la operación real? | Indicio de que sí lo hay: el salto de precio de marzo 2026 (EDA-1) no se replicó limpio en `historico-sap` por mes (H-011/L-09 EDA-2) |
| 6 | Fletes manuales sin ninguna marca que los identifique en `FactFWOFUFO` — ¿hay alguna pista indirecta (línea de negocio, tipo de cliente) que ayude a anticiparlos? | No hay forma de aislarlos de antemano, solo detectarlos después por no coincidir con ningún candidato reconstruido |

### 4.2 Sobre definiciones de datos (heredadas de EDA-1, algunas desde la extracción de fuentes corporativas y el set final)

| # | Pregunta | Por qué importa |
|---|---|---|
| 6 | ¿Los montos monetarios (`Flete Producción FO`, `Tarifa Venta al Cliente`, etc.) incluyen IVA o no? | Única pieza de la definición de estas columnas que sigue sin confirmar — moneda (COP) y unidad de peso (KG) ya están confirmadas |
| 7 | ¿Por qué contenerización (`OHUC`), pedidos sin costeo (`OPSC`) y carga extradimensionada (`OPXL`) no aparecen en el corredor de Buenaventura, aunque ya se confirmó que existen y se usan a nivel de toda la compañía? | L-09 EDA-1 — hipótesis: especialización por puerto (ej. contenerización concentrada en el Caribe), sin confirmar |
| 8 | Confirmación formal del criterio de extracción y los estados excluidos de `Viajes-Btura` | Ya hay evidencia fuerte de que es solo `Concluido`/`Ejecutado` (H-004 EDA-1) — falta que negocio lo confirme como definición oficial, no solo como hallazgo inferido |

### 4.3 Sobre comportamiento e integridad del sistema (EDA-2)

| # | Pregunta | Por qué importa |
|---|---|---|
| 9 | ¿Cuál es el criterio de desempate cuando hay varias tarifas "vigentes" simultáneas para la misma ruta (SICE-TAC y SAP)? | Caso extremo ya documentado: 567 candidatos para una sola ruta en SICE-TAC (L-04 EDA-2) — sin esto, cualquier reconstrucción de tarifa queda ambigua |
| 10 | ¿`tblSAPTarifasLog` (`historico-sap`) es un registro de tarifa realmente aplicada, o solo una auditoría de ediciones del catálogo? | Evidencia indirecta apunta a "auditoría" (65% de tarifas activas sin ninguna versión en el log, L-05 EDA-2) — sin confirmación directa no se puede usar como fuente de verdad |
| 11 | ¿Por qué quedaron huérfanas 5 cotizaciones (apuntan a un `SolicitudId` inexistente) y 1 cotización referenciada por 3 `tarifas_historicas`? | El usuario ya indicó que no sabría decir la causa exacta ("la app no está funcionando al 100%") — ameritaría revisión del equipo de desarrollo si se repite en producción real (L-08 EDA-2) |

---

## 5. Nuevas fuentes a curar — no existen en ningún sistema actual

| Fuente nueva | Para qué |
|---|---|
| Eventos de paro camionero (fechas, duración, intensidad) | Feature exógena para el Modelo 1 (pronóstico SICE-TAC) — no viene en ningún extracto, requiere curar manualmente una fuente de noticias/eventos |

---

## 6. Ampliación de alcance de extracciones ya existentes

| Extracción | Ampliación propuesta |
|---|---|
| `FactFWOFUFO` (base de la EDA-1) | Ampliar más allá del corredor de Buenaventura — confirmado como correcto por el usuario. Resuelve además la representatividad nacional (L-05 EDA-1) |
| `v1.0.0-piloto` (Nexo propio) | Repetir la extracción cuando exista producción real (no antes) — hoy son 13 solicitudes de prueba manual sin intención de representatividad (L-06 EDA-2) |

---

## 7. Recomendación: HU de validación de la regla de precio (no es una propuesta de modelo)

Esto **no es un modelo predictivo** — es una verificación, y se documenta acá (no en `propuesta_modelos.md`) por esa razón.

**El planteamiento:** el motor de Nexo (`ReglaDeCosteo`) ya automatiza una regla de negocio confirmada por el usuario — el precio se fija como **MAX(piso SICE-TAC vigente, tarifa SAP vigente)** para esa ruta+vehículo+peso. Esa regla se validó contra 32-12 cotizaciones de prueba de Nexo (87,5% y 100% de coincidencia exacta, H-005 EDA-2) — pero Nexo **nunca se ha probado contra la operación real de la empresa**, porque no tiene producción todavía. `FactFWOFUFO` sí es esa operación real (41.237 filas históricas).

**Por qué vale la pena antes de que Nexo entre en producción:** esa misma regla está a punto de gobernar **todas** las cotizaciones futuras del negocio. Si al aplicarla a escala completa de `FactFWOFUFO` resulta que coincide casi siempre → confirma que la regla ya automatizada refleja la realidad del negocio (y de paso, esa misma reconstrucción sirve como calculadora/auditoría para viajes que no pasan por Nexo). Si coincide poco → es una alerta importante que hay que resolver **antes** de confiar en la regla para todo el negocio, no después.

### Qué incluiría esta validación

1. **Paso 0 — clasificación por reconstrucción:** para cada viaje histórico de `FactFWOFUFO`, reconstruir el candidato SAP (tarifa vigente para esa ruta+vehículo+peso+fecha) y el candidato SICE-TAC (piso vigente para la misma combinación), y comparar contra el `Flete Producción FO` real:
   - Coincide con SAP → se etiqueta **Tarifario**.
   - Coincide con SICE-TAC → se etiqueta **SICE-TAC**.
   - No coincide con ninguno → probablemente manual, o no reconstruible por falta de vigencia histórica — se documenta como tal, no se fuerza una clasificación.
2. **Prerrequisitos de dato** (ya cubiertos en §2-3 de este documento): vigencia histórica real de SAP y SICE-TAC, mapeo de los códigos de vehículo/equipo propios de EDA-1 a los catálogos SAP reales.
3. **Reportar el % de coincidencia a escala completa**, comparado contra el 87,5%/100% de la muestra chica de Nexo — con la advertencia de que probablemente sea menor, no porque esos viajes no siguieran la regla, sino porque no hay vigencia histórica para confirmarlo tan atrás en el tiempo.
4. **Solo si**, después de resolver los huecos de dato, sigue habiendo una diferencia sistemática sin explicar (no solo ruido por datos faltantes) — ahí, y solo ahí, valdría la pena considerar un modelo residual pequeño (Gradient Boosting o regresión regularizada) sobre esa diferencia. No se propone de entrada.

### Preguntas de negocio que esta validación necesitaría resolver

Ya están listadas en §4.1 arriba (son las mismas 6 preguntas) — se repite el enlace acá porque son, en la práctica, los requisitos de entrada de esta validación, no solo curiosidades sueltas.

---

## Prioridad sugerida

1. **Evaluar el histórico de SICE-TAC ya descargado** (§2) — es lo único que ya está en la puerta, solo falta confirmarlo.
2. **Vigencia histórica real de SAP** (§2) — bloquea directamente la validación de §7 a escala completa.
3. **Confirmar las preguntas de negocio** (§4) — son conversaciones, no extracciones, y desbloquean el resto del diseño de la validación de §7.
4. El resto (mapeos, nuevas fuentes, ampliación de alcance) puede avanzar en paralelo, sin bloquear lo anterior.
