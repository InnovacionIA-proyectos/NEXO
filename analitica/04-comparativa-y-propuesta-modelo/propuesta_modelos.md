# Propuesta de modelo(s) — S2-HU-11 parte 3

Tres propuestas de modelo, cada una con objetivo, grano, features y supuestos — **ningún modelo se construye este sprint**. Cada ficha declara explícitamente sus limitaciones conocidas y qué falta resolver antes de poder entrenar en serio. Ver [`informe_comparativo.md`](informe_comparativo.md) para la comparativa que sustenta estas propuestas, y `hallazgos.md`/`limitaciones.md` de ambos EDA para la evidencia citada (H-0xx/L-0x).

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

### Modelo de ML propuesto (2 opciones)

**Opción A — Gradient Boosting (LightGBM o XGBoost) con features de rezago.** Se trata el pronóstico como regresión supervisada (valor del mes siguiente, dado valores de meses anteriores + salario mínimo + combustible + temporada + paros), entrenado con **todas las combinaciones ruta×vehículo×carga a la vez** (modelo global). Ventaja: aprende patrones compartidos entre combinaciones parecidas, no depende de que cada una tenga suficiente historia propia — relevante porque hay muchas combinaciones y poca historia individual. Desventaja: menos transparente en cómo separa tendencia de estacionalidad; necesita buen trabajo de features de rezago.

**Opción B — SARIMAX o Prophet, por combinación (o por segmento agrupado).** Modelo de series de tiempo clásico, diseñado específicamente para tendencia + estacionalidad + variables exógenas (salario mínimo, combustible, paros). Ventaja: más interpretable, más fácil de explicar a negocio ("la tendencia sube por esto, el pico estacional es por esto otro"). Desventaja: necesita suficiente historia **por cada combinación individual** — riesgo real si, aun con el histórico nuevo, algunas combinaciones quedan con pocos períodos.

**Recomendación inicial:** empezar con la Opción A (Gradient Boosting) por el problema de poca historia por combinación, y usar la Opción B como comparación/validación cuando haya combinaciones con suficiente historia propia.

### Supuestos

- Que el histórico oficial descargado (excels de la página de SICE-TAC) trae series de tiempo reales, no otra "foto única" con otro nombre — **pendiente de verificar** (ver prompt de evaluación ya entregado).
- Que los factores exógenos (salario mínimo, combustible) mantienen una relación razonablemente estable con SICE-TAC en el tiempo.
- Que la señal de "paros camioneros" se puede construir a partir de una fuente externa curada a mano — no viene lista en ningún dataset.

### Limitaciones conocidas

- Profundidad histórica real de los excels descargados: sin confirmar (L-07 EDA-2 — la única muestra que se tenía antes era una sola foto de `202608`).
- Ambigüedad de "tarifa vigente única": puede haber varias filas vigentes simultáneas para la misma combinación (hasta 567 candidatos documentados, L-04 EDA-2) — hace falta un criterio de desempate antes de poder tratar cada combinación como una serie de tiempo limpia.
- La variable de paros camioneros es trabajo de captura nuevo, no dato ya disponible.

### Reentrenamiento (diseñado, no ejecutado este sprint)

- **Disparador:** cuando se publique un nuevo `PERIODO` oficial de SICE-TAC.
- **Proceso:** agregar el período nuevo al histórico, reentrenar, comparar el error de pronóstico del modelo nuevo contra el vigente sobre una ventana de validación reciente.
- **Versionado:** cada modelo entrenado se guarda con fecha + métricas + rango de datos usado, como manifiesto en el repo (nunca el artefacto pesado — ya establecido en `05-modelo/README.md`).
- **Regla de reemplazo:** el modelo nuevo solo reemplaza al vigente en producción si iguala o mejora el error de pronóstico sobre la ventana de validación — nunca se reemplaza automáticamente sin ese chequeo.

---

## Modelo 2 — `Flete Producción FO` (reconstrucción por reglas + corrección residual)

### Objetivo

Predecir/reconstruir `Flete Producción FO` (el histórico ejecutado, `FactFWOFUFO`) para cualquier viaje, replicando la regla de negocio **confirmada directamente por el usuario**: el precio real se fija manualmente como **MAX(piso SICE-TAC vigente, tarifa SAP vigente)** para esa ruta+vehículo+peso — la misma regla que el motor de CabinaFletes (`ReglaDeCosteo`) ya automatiza para cotizaciones nuevas, aplicada históricamente a mano por una persona. Se usa un modelo estadístico solo para corregir el residual en los casos donde la regla sola no baste.

### Grano

`FO` + `BP Cliente` + `Peso Neto` + `Destino` — el grano viaje-cliente confirmado (H-001 EDA-1), no `FO` crudo.

### Arquitectura propuesta (2 capas)

1. **Capa de reglas:** para cada viaje histórico, calcular el candidato SICE-TAC (piso vigente a la fecha del viaje) y el candidato SAP (tarifa vigente a la fecha, filtrando por vehículo+peso+ruta exactos), y tomar el máximo — reproduciendo la misma regla que ya se validó parcialmente en H-005 de EDA-2 (62,5% de coincidencia exacta en las 32 líneas de CabinaFletes).
2. **Capa residual:** un modelo (regresión / gradient boosting) entrenado sobre la **diferencia** entre el valor real y el valor reconstruido por reglas, usando las variables ya identificadas en EDA-1 (peso, distancia/zona, tipo de vehículo, flota, línea de negocio, componente temporal) — para absorber los casos de negociación manual pura o vigencia histórica desconocida que la regla no explica.

### Features

- **Capa de reglas:** ruta→zona SAP (`tblSAPDANE`/`tblZonasTarificacionRelacion`), tipo de vehículo→SAP (`tblSAPTiposVehiculoTarifario`, requiere mapear el esquema propio de EDA-1 al de SAP), peso, fecha del viaje (para resolver vigencia), línea de negocio (`LineaNegocio`/`ClaseViaje`/`ClaseContenedor`, traducciones ya confirmadas en EDA-2).
- **Capa residual:** las mismas variables candidatas ya propuestas en `informe_comparativo.md` §6 (peso, distancia/zona, tipo de vehículo, flota, línea de negocio, componente temporal).

### Modelo de ML propuesto (2 opciones para la capa residual)

La capa de reglas **no es machine learning** — es la fórmula de negocio (MAX) replicada tal cual, sin entrenar nada. Las 2 opciones de abajo son para la capa residual (corregir lo que la regla no explica):

**Opción A — Gradient Boosting (XGBoost, LightGBM o CatBoost).** La elección estándar para datos tabulares con mezcla de categóricas (ruta, tipo de vehículo, línea de negocio) y numéricas (peso, distancia), a esta escala (~41.000 filas). Ventaja: maneja categóricas sin mucho preprocesamiento, captura interacciones entre variables automáticamente (ej. cómo el tipo de vehículo cambia el efecto de la distancia), robusto a outliers. Desventaja: menos transparente por defecto — necesita SHAP o similar para explicar una predicción puntual a negocio.

**Opción B — Regresión lineal regularizada (Ridge/Lasso) con categóricas codificadas.** Ventaja: cada coeficiente tiene un significado directo y auditable ("por cada 1.000 kg más, el residual sube en $X") — más fácil de explicar y de que negocio confíe en el resultado. Desventaja: no captura interacciones entre variables a menos que se agreguen a mano, probablemente con menor precisión si las relaciones reales no son lineales.

**Recomendación inicial:** Opción A como modelo principal (mejor precisión esperada), Opción B como comparación obligatoria — si Ridge da un resultado casi igual de bueno, es señal de que no vale la pena la complejidad extra de Gradient Boosting.

### Supuestos

- Que la regla MAX(SICE-TAC, SAP) confirmada para el proceso manual aplica de forma consistente a la mayoría de los viajes históricos de `FactFWOFUFO` — **validado solo en 32 casos de CabinaFletes, pendiente de validar a la escala completa de EDA-1 (41.237 filas)**.
- Que se puede conseguir la vigencia histórica real (no solo el valor actual) tanto de SAP como de SICE-TAC.
- Que una fracción de los datos son fletes manuales sin ninguna marca que los identifique (a diferencia de `cotizacion_lineas.Fuente`, `FactFWOFUFO` no tiene un campo equivalente) — se acepta como ruido irreducible en la capa residual, no se intenta clasificar de antemano.

### Limitaciones conocidas / preguntas abiertas (pendientes de negocio o de datos)

| # | Pregunta | Tipo |
|---|---|---|
| 1 | ¿La regla se aplica a todos los viajes históricos, en todas las líneas de negocio y regiones, o solo a un subconjunto? | Negocio |
| 2 | ¿A qué grano decide el precio la persona — por `FO` completo o por `FWO` (cada cliente dentro de una carga consolidada)? | Negocio |
| 3 | ¿Qué pasa cuando ni SICE-TAC ni SAP tienen tarifa vigente para esa ruta/vehículo? (Esos casos no son reconstruibles por regla) | Negocio |
| 4 | Vigencia histórica real ausente tanto en SAP (`AjusteVigenciaPlaza`, L-02 EDA-2) como en SICE-TAC (solo una foto de 151 filas, L-07 EDA-2) | Dato |
| 5 | Traducción de los códigos propios de EDA-1 (`ID Medio Transporte FO`, `Clase Equipo FOP`, nombres de ciudad) a los catálogos SAP reales — esquema de nombres distinto al de EDA-2, no es un cruce automático | Dato |
| 6 | Fletes manuales sin ninguna marca que los identifique en `FactFWOFUFO` — no hay forma de aislarlos de antemano, solo detectarlos después por desviación fuerte de ambas referencias | Dato/Metodología |
| 7 | Si se amplía más allá de Buenaventura (ya confirmado que es correcto hacerlo): ¿la regla del "MAX" es uniforme en todo el país, o varía por región/línea de negocio? | Negocio |
| 8 | Posible desfase de tiempo entre cuándo se actualiza la tarifa oficial y cuándo se empieza a aplicar en la operación real (indicio: H-011 EDA-2, el salto de marzo 2026 no se replicó limpio en `historico-sap`) | Dato/Negocio |

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
