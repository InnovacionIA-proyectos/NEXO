# Propuesta de modelo(s) — S2-HU-11 parte 3

Dos propuestas de modelo, cada una con objetivo, grano, features y supuestos — **ningún modelo se construye este sprint**. Cada ficha declara explícitamente sus limitaciones conocidas y qué falta resolver antes de poder entrenar en serio. Ver [`informe_comparativo.md`](informe_comparativo.md) para la comparativa que sustenta estas propuestas, y `hallazgos.md`/`limitaciones.md` de ambos EDA para la evidencia citada (H-0xx/L-0x).

**Nota:** originalmente se había planteado aquí un modelo para `Flete Producción FO` vía Tarifario SAP, entendido como comparación contra la regla de precio. Al analizarlo más a fondo, esa comparación específica no es un modelo predictivo — es una **verificación** de si la regla que CabinaFletes ya automatiza (`ReglaDeCosteo`, MAX(SICE-TAC, SAP)) también explica el histórico real de la empresa. Esa verificación se movió a [`recomendacion_captura_datos.md`](recomendacion_captura_datos.md) §7, como recomendación de una HU de validación futura, no como propuesta de modelo. Es un objetivo **distinto** del Modelo 2 de abajo: el Modelo 2 no compara contra la regla de precio, predice `Flete Producción FO` directamente a partir de variables operativas del viaje — el objetivo 1 de `informe_comparativo.md` §4, ya declarado viable con el dato de hoy.

---

## Modelo 1 — Pronóstico del piso SICE-TAC (`VALOR`/`VALORHORA`) en el tiempo

### Objetivo

**No** replicar el valor SICE-TAC de hoy para una ruta que ya existe en la tabla oficial — eso es una consulta, no una predicción (SICE-TAC es un valor regulado, calculado oficialmente por el gobierno para cada combinación ruta+vehículo+carga). El objetivo real: **pronosticar cómo van a cambiar `VALOR` y `VALORHORA`** (y por extensión `VALORSICECONHORA = VALOR + VALORHORA × HORASPACTADAS`) en los próximos meses, para una combinación ruta+vehículo+carga, dado el comportamiento de sus factores conocidos (salario mínimo, combustible, temporada, paros camioneros).

Requerimiento de negocio explícito — se construye independientemente de la evaluación de pertinencia de los datos (ver Limitaciones).

### Grano

Una fila = una combinación **ruta (`NOMBRERUTA`/`ORIGEN`-`DESTINO`) × `CONFIGURACION` × `NOMBRETIPOCARGA` × `CONDICIONCARGA`**, en un `PERIODO` (mes) específico. Granularidad temporal: mensual, según la cadencia real de actualización oficial de SICE-TAC.

### Features candidatas

- Histórico propio de la serie (`VALOR`/`VALORHORA` en meses anteriores — rezagos, tendencia)
- Salario mínimo vigente (con rezago de 1-3 meses — patrón ya confirmado en EDA-1, H-003)
- Precio del combustible (ACPM) — componente conocido de la fórmula oficial de SICE-TAC
- Peajes, si hay fuente disponible
- Estacionalidad (mes del año, fechas de alta demanda)
- Eventos de paro camionero (variable binaria/intensidad) — **no existe en ningún extracto actual, requiere curar una fuente de noticias/eventos aparte**
- Tipo de vehículo/carga, para no mezclar dinámicas de segmentos distintos

### Datos adicionales necesarios y cómo se van a tratar

**¿Qué falta conseguir, además del histórico de SICE-TAC?**

| Dato | ¿Ya lo tenemos? | Cómo se extrae |
|---|---|---|
| Histórico SICE-TAC (`VALOR`/`VALORHORA` por ruta/vehículo/carga/período) | ✅ Sí — 27 excels en `Historicos_SICE/` (enero 2025 a septiembre 2026), formato ya confirmado (cubo `WF_CUBO1_26S` completo, ~2 millones de filas por archivo). El mecanismo de cálculo se validó exacto (100%) para el período de agosto 2026 | Ya descargados de la página oficial de SICE-TAC. Falta procesar los otros 26 con el mismo método (ver abajo) |
| Salario mínimo histórico (mensual) | ❌ No | Decretos oficiales de MinTrabajo/Presidencia — serie corta y pública, se puede tabular a mano (1 valor por año, más los decretos de ajuste intermedio) |
| Precio de combustible (ACPM) histórico | ❌ No | Boletines del Ministerio de Minas y Energía. Antes de salir a buscarlo aparte, revisar si la metodología oficial de SICE-TAC ya publica el valor de ACPM que usa como insumo de su propia fórmula |
| Peajes | ❌ No, y probablemente fuera de alcance inicial | Sin fuente clara identificada — la tarifa de peaje depende del corredor específico, no del par origen-destino a este nivel de agregación. Se deja como deseable, no bloqueante |
| Eventos de paro camionero (fecha, duración, intensidad) | ❌ No | No hay fuente oficial automatizada — se cura a mano desde prensa o gremios de transporte (reportes de paros) |
| Estacionalidad | ✅ Se deriva de `PERIODO`, sin fuente externa | Feature engineering directo (mes del año, variables estacionales) |

**Cómo se van a tratar los 27 excels de SICE-TAC:**

1. Para cada uno de los 27 archivos, extraer las mismas columnas ya identificadas en el período de agosto 2026 (`ID`, `PERIODO`, `CONFIGURACION`, `VALOR`, `NOMBRERUTA`, `NOMBRETIPOCARGA`, `NOMBREUNIDADTRANSPORTE`, `ORIGEN`, `DESTINO`, `VALORHORA`, `HORASRECORRIDO`, `HORASPACTADAS`, `CONDICIONCARGA`, `VIAESTANDAR`).
2. Calcular `VALORSICECONHORA` con la fórmula ya confirmada (`VALOR + VALORHORA × HORASPACTADAS`), si no viene directo.
3. Aplicar la misma regla de selección ya validada (prioriza vía estándar + menos horas + menor valor) para resolver la ambigüedad de varias tarifas "vigentes" simultáneas para la misma combinación dentro de un mismo período (el mismo problema ya documentado de los 567 candidatos).
4. Consolidar los 27 períodos en una sola tabla larga — una fila = combinación ruta+vehículo+carga × período. Esa es la serie de tiempo base para entrenar el Modelo 1.
5. **Nota de escala:** cada archivo pesa ~100-170 MB (~2 millones de filas) — 27 archivos son del orden de 50+ millones de filas en total. No es un `pd.read_excel` casual — necesita procesamiento por lotes o una herramienta pensada para ese volumen, no cargar todo en memoria de una vez.

### Modelo de ML propuesto (2 opciones)

**Opción A — Gradient Boosting (LightGBM o XGBoost) con features de rezago.** Se trata el pronóstico como regresión supervisada (valor del mes siguiente, dado valores de meses anteriores + salario mínimo + combustible + temporada + paros), entrenado con **todas las combinaciones ruta×vehículo×carga a la vez** (modelo global). Ventaja: aprende patrones compartidos entre combinaciones parecidas, no depende de que cada una tenga suficiente historia propia — relevante porque hay muchas combinaciones y poca historia individual. Desventaja: menos transparente en cómo separa tendencia de estacionalidad; necesita buen trabajo de features de rezago.

**Opción B — SARIMAX o Prophet, por combinación (o por segmento agrupado).** Modelo de series de tiempo clásico, diseñado específicamente para tendencia + estacionalidad + variables exógenas (salario mínimo, combustible, paros). Ventaja: más interpretable, más fácil de explicar a negocio ("la tendencia sube por esto, el pico estacional es por esto otro"). Desventaja: necesita suficiente historia **por cada combinación individual** — riesgo real si, aun con el histórico nuevo, algunas combinaciones quedan con pocos períodos.

**Recomendación inicial:** empezar con la Opción A (Gradient Boosting) por el problema de poca historia por combinación, y usar la Opción B como comparación/validación cuando haya combinaciones con suficiente historia propia.

### Supuestos

- Que los factores exógenos (salario mínimo, combustible) mantienen una relación razonablemente estable con SICE-TAC en el tiempo.
- Que la señal de "paros camioneros" se puede construir a partir de una fuente externa curada a mano — no viene lista en ningún dataset.
- Que la regla de selección ya validada (vía estándar + menos horas + menor valor) resuelve la ambigüedad de tarifas simultáneas de forma consistente en los 26 períodos todavía sin procesar, igual que en agosto 2026.

### Limitaciones conocidas

- Profundidad histórica real: **ya no es una limitación** — confirmado que existen 27 períodos reales (`Historicos_SICE/`, enero 2025 a septiembre 2026); solo falta procesar 26 de los 27 con el método ya validado (ver "Datos adicionales" arriba).
- Ambigüedad de "tarifa vigente única": puede haber varias filas vigentes simultáneas para la misma combinación dentro de un mismo período (hasta 567 candidatos documentados, L-04 EDA-2) — ya hay una regla de selección validada (vía estándar + menos horas + menor valor, confirmada 100% exacta en agosto 2026), pero falta confirmar que se sostiene igual de bien en los demás períodos.
- La variable de paros camioneros es trabajo de captura nuevo, no dato ya disponible.
- El volumen de datos (27 archivos, ~50+ millones de filas en total) requiere procesamiento por lotes, no un análisis casual en memoria.

### Reentrenamiento (diseñado, no ejecutado este sprint)

- **Disparador:** cuando se publique un nuevo `PERIODO` oficial de SICE-TAC.
- **Proceso:** agregar el período nuevo al histórico, reentrenar, comparar el error de pronóstico del modelo nuevo contra el vigente sobre una ventana de validación reciente.
- **Versionado:** cada modelo entrenado se guarda con fecha + métricas + rango de datos usado, como manifiesto en el repo (nunca el artefacto pesado — ya establecido en `05-modelo/README.md`).
- **Regla de reemplazo:** el modelo nuevo solo reemplaza al vigente en producción si iguala o mejora el error de pronóstico sobre la ventana de validación — nunca se reemplaza automáticamente sin ese chequeo.

---

## Modelo 2 — Predicción de `Flete Producción FO` (tarifa de techo) con variables operativas

### Objetivo

Predecir `Flete Producción FO` —el valor real pagado al transportista, el techo de tarifa que efectivamente rige la operación— a partir de las variables operativas propias del viaje (peso, zona/distancia, tipo de vehículo, flota, línea de negocio, componente temporal), usando `FactFWOFUFO` como fuente. Corresponde al objetivo 1 de `informe_comparativo.md` §4, ya declarado **viable hoy** con el dato disponible.

**Alcance confirmado por negocio:** sin filtrar por corredor — se entrena con **todas** las rutas de `FactFWOFUFO` a nivel nacional, no solo el corredor Buenaventura (confirmado por el usuario, ver `recomendacion_captura_datos.md` §6). El único filtro que se conserva es el que la extracción ya trae por diseño: **solo viajes en estado `Concluido`/`Ejecutado`**. El modelo predice la tarifa esperada para viajes que efectivamente se completan; no intenta cubrir `Anulado`/`En tratamiento` — eso es un objetivo distinto (probabilidad de cancelación), declarado no viable aparte (`declaracion_no_viabilidad.md`, objetivo 2).

### Grano

**`FO` + `BP Cliente` + `Peso Neto` + `Destino`** — mismo grano recomendado en `informe_comparativo.md` §5, correspondiente al nivel `FWO` (el contrato real con el cliente dentro de un `FO` que puede agrupar carga consolidada de varios clientes, confirmado contra `FactFWOFUFO`, H-001 EDA-1). No se entrena a nivel `FO` crudo porque esa clave no es única (128 valores repetidos).

### Features candidatas

Mismas variables ya identificadas en `informe_comparativo.md` §6:

- `Peso Neto` (confirmado en KG contra `FactFWOFUFO`)
- `Distancia Total` — válida como variable de tarifa/zona, no como medición real del viaje (H-011 EDA-1)
- Zona de tarificación de origen/destino, agrupando ciudades satélite en vez de la ciudad cruda (~113/122 ciudades → ~15-20 zonas, H-008 EDA-1) — pendiente completar el mapeo para ciudades fuera del corredor actual
- Tipo de vehículo: `ID Medio Transporte FO` + `Clase Equipo FOP` (las 2 dimensiones reales, tras eliminar la redundancia de las 5 columnas de vehículo/equipo, H-007 EDA-1)
- Flota (Propio/Tercero)
- Línea de negocio / Clase Expedición FWO
- Componente temporal explícito (mes, o régimen antes/después de marzo 2026 — necesario por el salto de +16,7% confirmado en esa fecha, H-003 EDA-1)

**Deseable a futuro, no bloqueante para la primera versión:** contexto de mercado vigente por fecha (piso SICE-TAC / plaza SAP) — el mecanismo de cruce ya está confirmado exacto en ambos lados (H-005 EDA-2), pero incorporarlo como feature depende de terminar de procesar los 27 períodos históricos de SICE-TAC a la escala de los 13 meses de EDA-1 (objetivo 2 de `informe_comparativo.md` §4, L-07 EDA-2) — se puede sumar en una iteración posterior.

**Mismas variables exógenas candidatas que el Modelo 1**, porque presionan el costo real de operar el viaje igual que presionan el piso SICE-TAC:

- Salario mínimo vigente (con rezago de 1-3 meses, mismo patrón confirmado en EDA-1, H-003)
- Precio del combustible (ACPM) — componente directo del costo de operación del transportista
- Peajes, si hay fuente disponible
- Eventos de paro camionero (variable binaria/intensidad) — mismo insumo que el Modelo 1, no existe en ningún extracto actual

Aplican con la misma salvedad que en el Modelo 1: se agregan como features candidatas, no como variables ya disponibles — ver la tabla de abajo para qué falta conseguir de cada una.

### Datos adicionales necesarios

| Dato | ¿Ya lo tenemos? | Cómo se extrae |
|---|---|---|
| `FactFWOFUFO` ampliado a nivel nacional | ❌ No — hoy solo se tiene el corredor Buenaventura (41.237 filas, 13 meses) | Nueva extracción, ya confirmada como correcta por negocio (`recomendacion_captura_datos.md` §6); mismo formato y columnas del extracto actual |
| Mapeo ciudad→zona de tarificación completo | Parcial — resuelto para 21/24 ciudades de la muestra actual | Completar contra `tblSAPDANE`/`tblZonasTarificacionRelacion` para las ciudades nuevas del extracto nacional |
| Salario mínimo histórico (mensual) | ❌ No | Mismo insumo que el Modelo 1 — decretos oficiales de MinTrabajo/Presidencia, serie corta y pública |
| Precio de combustible (ACPM) histórico | ❌ No | Mismo insumo que el Modelo 1 — boletines del Ministerio de Minas y Energía |
| Eventos de paro camionero (fecha, duración, intensidad) | ❌ No | Mismo insumo que el Modelo 1 — se cura a mano desde prensa o gremios de transporte (ver `recomendacion_captura_datos.md` §5) |

### Modelo de ML propuesto (2 opciones)

**Opción A — Gradient Boosting (LightGBM o XGBoost), modelo único a nivel nacional.** Regresión supervisada de `Flete Producción FO` sobre las features operativas, entrenado con todas las combinaciones ruta×vehículo×línea de negocio a la vez. Ventaja: aprovecha el volumen nacional completo y comparte patrones entre zonas parecidas — relevante para zonas nuevas (fuera de Buenaventura) con poca historia propia. Desventaja: puede diluir efectos muy locales de una zona específica si la zona no entra como feature categórica de buena calidad.

**Opción B — Modelo jerárquico/segmentado por zona o línea de negocio.** Un modelo (o una capa de ajuste) por zona de tarificación o línea de negocio, en vez de uno global. Ventaja: más fácil de auditar por segmento ("¿por qué el techo de Buenaventura es distinto al del Caribe?"), y aísla mejor el salto de régimen de marzo 2026 si no afecta igual a todas las zonas. Desventaja: necesita volumen suficiente por segmento — riesgo real en zonas nuevas del extracto nacional que hoy no se conocen.

**Recomendación inicial:** empezar con la Opción A sobre el extracto nacional completo, con zona y línea de negocio como features categóricas fuertes, y usar la Opción B como diagnóstico para detectar si algún segmento se comporta sistemáticamente distinto (candidato a submodelo propio).

### Supuestos

- Que restringir el entrenamiento a viajes `Concluido`/`Ejecutado` es el alcance aceptado por negocio para este modelo — confirmado ahora, a diferencia del objetivo de cancelación (declarado no viable aparte).
- Que las relaciones entre variables operativas y `Flete Producción FO` observadas en el corredor Buenaventura (bivariado ya explorado en EDA-1) se mantienen razonablemente al extender a nivel nacional — pendiente confirmar cuando llegue el extracto ampliado.
- Que el salto de régimen de marzo 2026 (+16,7%) se puede modelar como variable temporal explícita, sin necesitar un modelo separado por período.

### Limitaciones conocidas

- Sesgo de selección: el modelo solo aprende de viajes que llegan a concluirse — no es válido para estimar la probabilidad de que un viaje se concrete (H-004 EDA-1; ese objetivo queda declarado no viable aparte, `declaracion_no_viabilidad.md` objetivo 2).
- Hasta que llegue el extracto nacional ampliado, el único dato de entrenamiento disponible sigue siendo el corredor Buenaventura (41.237 filas, 13 meses) — el alcance nacional es el objetivo confirmado, no el estado actual del dato.
- Contexto de mercado (piso SICE-TAC / plaza SAP vigente por fecha) no está aún incorporado como feature — sigue bloqueado por escala de procesamiento, no por el mecanismo (objetivo 2, L-07 EDA-2).
- Mapeo completo de ciudad→zona de tarificación pendiente para las ciudades nuevas que traiga el extracto nacional (H-008 EDA-1).
- `Distancia Total` es valor de tabla/zona, no medición real del viaje — se usa como variable de tarifa, no de logística real (H-011 EDA-1).
- Las variables exógenas (salario mínimo, combustible, paros) son, igual que en el Modelo 1, trabajo de captura nuevo — hoy no existen en ningún extracto disponible, así que la primera versión del modelo se entrena solo con variables operativas y suma las exógenas cuando estén curadas.

### Reentrenamiento (diseñado, no ejecutado este sprint)

- **Disparador:** cadencia fija de **3 días**, a diferencia del Modelo 1 (que depende de la publicación oficial de un nuevo `PERIODO` de SICE-TAC, con cadencia mensual). Tiene sentido una cadencia corta y fija acá porque `FactFWOFUFO` se alimenta de viajes que se van completando de forma continua, no de una publicación periódica externa — cada ciclo de 3 días simplemente sí o sí incorpora los viajes `Concluido`/`Ejecutado` nuevos desde el ciclo anterior.
- **Proceso:** agregar los viajes nuevos del ciclo, reentrenar, comparar el error contra el modelo vigente sobre una ventana de validación reciente (igual que Modelo 1).
- **Métrica de error:** **MAPE** (error porcentual promedio) como métrica principal — más interpretable para negocio que un error en pesos crudos, dado que el valor de `Flete Producción FO` varía en órdenes de magnitud muy distintos entre rutas cortas y largas. **MAE** en pesos como métrica secundaria, para dimensionar el costo absoluto del error. No se fija un umbral numérico de aceptación todavía — no hay evidencia real para defender un número específico sin haber corrido el modelo al menos una vez; se define comparando siempre contra el error del modelo vigente, no contra un umbral absoluto inventado.
- **Ventana de validación:** propuesta inicial — últimos **15 días** de viajes concluidos (ventana rodante, ~5 ciclos de reentrenamiento), como holdout para decidir si el modelo nuevo reemplaza al vigente. Ajustable una vez se vea el volumen real de viajes completados por ciclo.
- **Actualización de variables exógenas dentro del ciclo:** las exógenas (salario mínimo, combustible, paros) no cambian a la misma cadencia que el reentrenamiento — cada ciclo de 3 días usa el valor vigente más reciente de cada una, no vuelve a consultar la fuente externa en cada ciclo (salario mínimo es ~anual, combustible más frecuente, paros es por evento).
- **Versionado y regla de reemplazo:** mismos criterios que Modelo 1 (manifiesto con fecha + métricas + rango de datos, nunca el artefacto pesado; el modelo nuevo solo reemplaza al vigente si iguala o mejora el MAPE de validación — nunca se reemplaza automáticamente sin ese chequeo).
- **Dueño del proceso:** pendiente de asignar — no se define responsable operativo (equipo de datos/MLOps vs. automatizado) hasta que el modelo entre en producción.
- **Pendiente de confirmar con negocio:** si 3 días es una cadencia realista dado el volumen real de viajes nuevos que se completan en ese lapso (si el volumen por ciclo es muy bajo, reentrenar cada 3 días puede no cambiar el modelo de forma significativa frente a, por ejemplo, una cadencia semanal).

---

## Modelo 3 — Recomendador de tarifa con mayor probabilidad de ser "ganada" (`tarifas_historicas`)

### Objetivo

Dado una nueva línea de solicitud (ruta, vehículo, peso, cliente, tipo de carga), recomendar la tarifa con mayor probabilidad de ser aceptada ("ganada") por el cliente, usando `tarifas_historicas` (Biblioteca de Tarifas Ganadas) como referencia.

### Grano

Línea de solicitud/cotización — mismo grano que `cotizacion_lineas`/`tarifas_historicas`.

### Features candidatas (a futuro, cuando exista volumen)

Ruta, tipo de vehículo, peso, cliente (o segmento), tarifa cotizada vs. tarifa de referencia SAP/SICE-TAC, histórico de tarifas ganadas para esa ruta/cliente.

### Modelo de ML propuesto (2 opciones, tentativas — sin definir en firme)

**Opción A — Clasificación binaria con Gradient Boosting.** Predice la probabilidad de "ganada" para una tarifa propuesta, dado el contexto (ruta, vehículo, peso, cliente). Ventaja: simple de entrenar y evaluar con las métricas usuales de clasificación. Desventaja: evalúa una tarifa a la vez, no compara directamente entre varias opciones candidatas.

**Opción B — Modelo de ranking entre tarifas candidatas.** En vez de clasificar una tarifa aislada, compara varias tarifas posibles para la misma solicitud y ordena cuál tiene mayor probabilidad de ser aceptada. Ventaja: más alineado al caso de uso real ("recomendar la mejor entre varias opciones", no solo "sí/no" para una). Desventaja: necesita más datos y es más complejo de entrenar/evaluar que una clasificación simple.

No se define cuál usar en firme porque el modelo no se construye hasta que exista volumen real — la elección dependerá de qué tan desbalanceada resulte la clase "ganada"/"perdida" y de si el caso de uso real termina siendo "evaluar una tarifa" (Opción A) o "elegir entre varias" (Opción B).

### Supuestos

Requiere producción real de CabinaFletes con volumen suficiente de cotizaciones ganadas/perdidas para aprender un patrón.

### Estado

**Explícitamente no viable hoy — solo se deja planteado.** `tarifas_historicas` tiene hoy 16 filas, provenientes de 13 solicitudes de prueba manual sin intención de representatividad (L-06 EDA-2, confirmado por el usuario). Se retoma cuando exista volumen real de producción — no requiere trabajo adicional de preparación ahora, más allá de esta ficha.
