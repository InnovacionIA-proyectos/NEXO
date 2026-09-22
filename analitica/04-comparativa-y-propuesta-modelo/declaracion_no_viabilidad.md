# Declaración: objetivos donde el dato no soporta predicción útil — S2-HU-11 parte 3

Entregable propio que exige el criterio de cierre de la HU y el escenario Gherkin "El dato no soporta una predicción útil". Formaliza, con evidencia citada, cuáles de los objetivos de predicción evaluados en [`informe_comparativo.md`](informe_comparativo.md) §4 **no son viables con el dato disponible hoy** — y por qué no es un problema de mecanismo o de metodología, sino de que el dato en sí no alcanza.

---

## Objetivos donde el dato no soporta predicción útil

### 1. Predecir o asistir la cotización de CabinaFletes (ej. sugerir `ValorFlete` al vendedor)

**Declarado: no viable — el dato no soporta predicción útil.**

- Solo 13 solicitudes de prueba manual en `v1.0.0-piloto`, creadas por el propio usuario a mano, **sin intención de cobertura o representatividad de casos de negocio** (confirmado directamente por el usuario — memoria del proyecto, EDA-2).
- `ValorMercancia` y `Peso` (en `lineas_solicitud`) tienen valores puestos al azar durante esas pruebas, sin relación con magnitudes reales de negocio — y a diferencia de `ValorFlete` (que sí tiene la columna `Fuente` para separar lo manual), estas 2 columnas no tienen ninguna forma programática de distinguir señal real de ruido (H-002 EDA-2).
- No es un problema de algoritmo ni de features — es que el volumen y la calidad del dato de origen no alcanzan para que cualquier modelo aprenda un patrón de negocio real.

**Qué lo haría viable:** repetir esta extracción cuando CabinaFletes tenga producción real (L-06 EDA-2).

---

### 2. Predecir cancelación o probabilidad de que un viaje no se concrete

**Declarado: no viable con ninguna fuente actual — el dato no soporta predicción útil.**

- `Viajes-Btura`/`FactFWOFUFO` (EDA-1) contiene **únicamente** FOs en estado `Concluido`/`Ejecutado` — sesgo de selección y supervivencia confirmado con evidencia directa: en la misma ventana, a nivel compañía, existen 8.034 FO `Anulado` y 115.216 `En tratamiento`, ausentes del extracto (H-004 EDA-1, verificado contra `FactFWOFUFO`).
- `v1.0.0-piloto` (EDA-2) tampoco aporta estados de abandono de mercado real — es el mismo set de 13 solicitudes de prueba, sin cancelaciones reales de cliente.
- No hay forma de entrenar un modelo de cancelación cuando el set de entrenamiento, por diseño de la extracción, **excluye exactamente los casos que se querría predecir**.

**Qué lo haría viable:** una extracción nueva de `FactFWOFUFO` que incluya todos los estados, no solo `Concluido`/`Ejecutado` (ver `recomendacion_captura_datos.md` §2).

---

### 3. Predecir margen o rentabilidad por ruta/cliente

**Declarado: no viable con el dato actual — el dato no soporta predicción útil.**

- `Tarifa Venta al Cliente` (EDA-1) — la columna que haría falta para calcular margen — es **idéntica** a `Flete Producción FO` en el 72,2% de las filas válidas, en una columna con miles de valores únicos posibles (estadísticamente imposible por azar) — confirmado directamente por el negocio como mal capturada en este extracto específico (H-002 EDA-1).
- No es un problema de modelo: la variable que se necesitaría como base del cálculo de margen simplemente no refleja la realidad en esta fuente.

**Qué lo haría viable:** identificar la tabla fuente correcta donde `Tarifa Venta al Cliente` sí se capture bien (pendiente, sin candidato identificado todavía).

---

## Objetivo con matiz — no encaja como "sí/no" simple

### Enriquecer la predicción de `Flete Producción FO` con contexto de mercado vigente por fecha (piso SICE-TAC / plaza SAP)

**No se declara como "el dato no soporta predicción útil"** — el mecanismo de cruce con ambas fuentes (SAP y SICE-TAC) ya se reprodujo exacto (H-005 EDA-2), así que el bloqueo actual es de **cobertura de procesamiento**, no de que el dato sea insuficiente por naturaleza: de los 27 períodos históricos reales de SICE-TAC ya disponibles (`Historicos_SICE/`), falta repetir el procesamiento en los períodos que cubren la ventana completa de EDA-1 (abril 2025-abril 2026). Se documenta como **"no viable hoy, pendiente de completar"**, no como declaración de no-viabilidad de datos — ver `informe_comparativo.md` §4 (objetivo 2) y `recomendacion_captura_datos.md` §2.

---

## Objetivos donde el dato sí soporta predicción útil (referencia, no parte de esta declaración)

Ver `informe_comparativo.md` §4 objetivo 1 (predecir `Flete Producción FO` con variables operativas del histórico — viable hoy) y `propuesta_modelos.md` (las 3 fichas de modelo propuestas sobre esa base).
