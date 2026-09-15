# Recomendación de captura de datos futura — S2-HU-11 parte 3

Consolida en un solo documento todo lo que se identificó como necesario a lo largo de ambos EDA y de la propuesta de modelos — qué ya se consiguió, qué falta pedir, qué falta mapear/traducir, y qué falta confirmar con negocio. Ver `hallazgos.md`/`limitaciones.md` de ambos EDA y `propuesta_modelos.md` para el detalle y la evidencia de cada punto (H-0xx/L-0x).

---

## 1. Ya conseguido en este sprint — no volver a pedirlo

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
| **Vigencia histórica real de `tarifa-plaza`** (no solo el valor actual de `VIGENCIA`, que es mutable) | Sin esto, no se puede saber qué tarifa SAP estaba vigente en la fecha exacta de un viaje pasado — bloquea la reconstrucción del Modelo 2 | L-02 EDA-2, confirmado con evidencia directa en H-005 (12/32 líneas no calzaron por esto) |
| **Evaluación del histórico oficial de SICE-TAC ya descargado** — confirmar si trae series de tiempo reales (varios períodos) o es otra "foto única" repetida | Insumo crítico del Modelo 1 | Pendiente — prompt de evaluación ya entregado |
| **Número de contrato/tarifario aplicado por viaje** en `FactFWOFUFO` | Explicaría la dispersión de tarifas en la misma ruta | L-07 EDA-1. 3 caminos alternativos ya identificados: (1) reconstruir por coincidencia de valor contra `tblSAPTarifas`/`tblSAPTarifasLog`; (2) preguntar a IT/SAP si el sistema transaccional lo guarda; (3) proxy de cercanía al rango legítimo |
| **Marca de "fuente"/mecanismo de fijación de precio** en `FactFWOFUFO` (equivalente a `cotizacion_lineas.Fuente`: manual vs. regla) | Sin esto no se puede distinguir un flete manual/negociado de uno fijado por la regla MAX(SICE-TAC, SAP) — hoy no hay forma de aislarlos de antemano | Nuevo, de la propuesta del Modelo 2 (pregunta abierta #6) |

---

## 3. Pendiente de mapeo/traducción — no son tablas nuevas, es trabajo con lo que ya se tiene

| Qué falta mapear | Detalle |
|---|---|
| Código de vehículo de EDA-1 (`ID Medio Transporte FO`, ej. `TC-3S2`) → `tblSAPTiposVehiculoTarifario` | El esquema de nombres es distinto al de CabinaFletes (`2S3` sin el prefijo `TC-`/`FP-`) — no es un cruce automático, hay que resolverlo antes de poder usar la capa de reglas del Modelo 2 a escala de EDA-1 |
| Código de equipo de EDA-1 (`Clase Equipo FOP`) → `tblSAPTiposCarroseria`/`tblSAPClaseContenedor` | Mismo problema — nombres propios de EDA-1, no necesariamente iguales a los de CabinaFletes |

---

## 4. Pendiente de confirmar con negocio — definiciones, no datos crudos

| Pregunta | Por qué importa |
|---|---|
| ¿La regla "MAX(SICE-TAC, SAP)" que aplica la persona manualmente cubre el 100% de los viajes históricos, en todas las líneas de negocio y regiones, o solo un subconjunto? | Determina si el Modelo 2 se puede generalizar a todo `FactFWOFUFO` o solo a una parte |
| ¿A qué grano decide el precio — por `FO` completo o por `FWO` (cliente dentro de una carga consolidada)? | Define el grano correcto de entrenamiento del Modelo 2 |
| ¿Qué hace la persona cuando ni SICE-TAC ni SAP tienen tarifa vigente para esa ruta/vehículo? | Esos casos no son reconstruibles por regla — hay que saber cómo se resuelven hoy para no tratarlos como error |
| ¿Es la misma regla en todo el país, o varía por región/línea de negocio? | Relevante ahora que se confirma ampliar `FactFWOFUFO` más allá de Buenaventura |
| ¿Por qué contenerización (`OHUC`), pedidos sin costeo (`OPSC`) y carga extradimensionada (`OPXL`) no aparecen en el corredor de Buenaventura? | L-09/L-08 EDA-1 — ya confirmado que existen a nivel compañía, falta confirmar si es especialización por puerto |

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
| `v1.0.0-piloto` (CabinaFletes propio) | Repetir la extracción cuando exista producción real (no antes) — hoy son 13 solicitudes de prueba manual sin intención de representatividad (L-06 EDA-2) |

---

## Prioridad sugerida

1. **Evaluar el histórico de SICE-TAC ya descargado** (§2) — es lo único que ya está en la puerta, solo falta confirmarlo.
2. **Vigencia histórica real de SAP** (§2) — bloquea directamente la validación del Modelo 2 a escala completa.
3. **Confirmar las preguntas de negocio** (§4) — son conversaciones, no extracciones, y desbloquean el resto del diseño del Modelo 2.
4. El resto (mapeos, nuevas fuentes, ampliación de alcance) puede avanzar en paralelo, sin bloquear lo anterior.
