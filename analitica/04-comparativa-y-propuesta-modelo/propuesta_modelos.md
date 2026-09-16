# Propuesta de modelo(s) — S2-HU-11 parte 3

Dos propuestas de modelo, cada una con objetivo, grano, features y supuestos — **ningún modelo se construye este sprint**. Cada ficha declara explícitamente sus limitaciones conocidas y qué falta resolver antes de poder entrenar en serio. Ver [`informe_comparativo.md`](informe_comparativo.md) para la comparativa que sustenta estas propuestas, y `hallazgos.md`/`limitaciones.md` de ambos EDA para la evidencia citada (H-0xx/L-0x).

**Nota:** originalmente se había planteado aquí un "Modelo 2" para `Flete Producción FO` vía Tarifario SAP. Al analizarlo más a fondo, no es un modelo predictivo — es una **verificación** de si la regla de precio que CabinaFletes ya automatiza (`ReglaDeCosteo`) también explica el histórico real de la empresa. Se movió a [`recomendacion_captura_datos.md`](recomendacion_captura_datos.md) §7, como recomendación de una HU de validación futura, no como propuesta de modelo.

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

## Modelo 2 — Recomendador de tarifa con mayor probabilidad de ser "ganada" (`tarifas_historicas`)

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
