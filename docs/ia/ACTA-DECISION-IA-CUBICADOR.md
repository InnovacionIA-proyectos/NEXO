# Acta: decisión de alcance de la IA en el Cubicador (go / no-go)

- **Issue:** S6-HU-35.5 (#370)
- **Fecha:** 2026-10-30
- **Estado:** decisión propuesta, pendiente de aprobación (ver al final)

## Qué se decide

Si la IA (Claude) amplía su papel en el Cubicador más allá del modo sombra, y con qué alcance
queda la HU siguiente.

## Evidencia

### 1. Acierto contra el dataset dorado

La IA se midió con el mismo prompt, el mismo esquema de salida y la misma validación que usa el
modo sombra en producción (`cubicador/application/ia_sombra/`), sobre los 22 casos validados de
la semilla y con las mismas métricas que la línea base del motor. Los dos informes tienen la
misma huella de dataset (`78884e7c8037`): se midieron sobre exactamente los mismos casos. Herramienta:
`evaluacion-ia/cubicador/evaluar_ia.py`; informes completos:
`evaluacion-ia/cubicador/informe_ia_semilla.md` y `informe_linea_base_semilla.md`.

| Métrica | Motor | IA (Sonnet 5.5, esfuerzo high) |
|---|---|---|
| Decisión (asignar / no asignar) | 100 % (22/22) | 100 % (22/22) |
| Motivo cuando no asigna | 100 % (2/2) | 100 % (2/2) |
| Acierto del vehículo (flota exacta) | 100 % (18/18) | 100 % (18/18) |
| Flota mínima (cantidad exacta) | 100 % (20/20) | **95 % (19/20)** |
| Respeta peso / volumen / unidades | 100 % (20/20) | 100 % (20/20) |
| Latencia p50 / p95 | 1,6 / 25,7 ms | **5,1 / 13,0 s** |
| Costo por cubicación | 0 | US$0,011 (estimación por lo alto: cuenta la caché a precio completo) |

**El único error es grave.** En el caso S-19 (33 estibas de 120×100×150 cm) la IA propone **una
Minimula** cuando hacen falta **dos**: el volumen cabe (59 de 76 m³), pero en el piso, girando las
estibas y apilando dos alturas, solo caben 32. Se repitió tres veces más y falló las tres: es
sistemático. Recomendar menos vehículos de los necesarios produce una carga que no cabe; es el
error más caro posible.

Las métricas de «respeta peso / volumen» salen en 100 % porque comparan sumas: no verifican que
las piezas quepan físicamente. El acomodo en 3D lo resuelve el motor y la IA no.

**Límites de esta medición.** Son 22 casos sintéticos y fáciles, una corrida por caso (S-19 se
repitió tres veces más) y una sola configuración: la de producción del modo sombra (Sonnet 5.5,
esfuerzo high, prompt v1). Alcanza para decir que la IA hoy no supera al motor; no alcanza para
decir cuánto acertaría con cargas reales.

### 2. Modo sombra con tráfico real

**No hay datos.** El código está en QA, pero el modo sombra nunca se encendió: falta la workspace
y la llave de Anthropic del Cubicador, y la tabla `iaSombra` no existe en la base que usa hoy el
Cubicador de QA. En la base propia del Cubicador (`dbCubicador_Pruebas`) ya está creada.

Aunque tuviera datos, el modo sombra mide **cuánto se parece la IA al motor**, no cuánto acierta.
El acierto solo se mide contra respuestas validadas, como en el punto 1.

### 3. Lo que la IA ya hace bien junto al motor

- **Interpretar descripciones libres de mercancía** en posiciones estructuradas (#368): 23/23
  casos exactos y 7/7 rechazos correctos; el motor valida todo lo que sale.
- **Explicar la recomendación** en lenguaje natural (#369).

En los dos casos la IA no decide: prepara datos o explica, y el motor sigue mandando.

## Criterios

Los criterios propuestos en la issue eran «acierto igual o superior al del motor» y «p95 de
latencia no peor».

| Criterio | Resultado | Comentario |
|---|---|---|
| Acierto igual o superior al del motor | **No cumple** | 95 % contra 100 % en flota mínima, con un error de seguridad |
| p95 de latencia no peor | No aplica tal como está escrito | El motor responde en milisegundos y un modelo en segundos: nunca se cumpliría. En modo sombra la IA corre aparte y no retrasa la respuesta |

**Criterios corregidos** para cualquier reevaluación futura de la IA en la selección de flota:

1. Acierto en flota mínima y en vehículo **igual o superior al del motor**, medido sobre cargas
   **reales validadas**, no solo sintéticas.
2. **Cero propuestas con menos vehículos de los necesarios** (criterio de seguridad, se exige
   aparte del promedio).
3. **No retrasa la respuesta al usuario.** Si algún día la IA entrara en el camino de la
   respuesta, se fija un p95 máximo en segundos, no una comparación con el motor.
4. Costo por cubicación de **US$0,02 o menos**.

## Decisión propuesta

**No-go** a que la IA decida la flota o reemplace al motor. No supera al motor y falla
sistemáticamente en lo geométrico, que es justo lo que el motor resuelve.

**Go** a:

1. **Encender el modo sombra en QA**, para medir el parecido con tráfico real. Es barato
   (~US$0,011 por cubicación; con el tope por defecto de 1 millón de tokens al día, el gasto
   máximo es de unos US$3,4 diarios) y no afecta la respuesta.
2. **Usar la IA donde complementa al motor sin decidir:** interpretación de texto libre y
   explicación de la recomendación.

## Alcance de la HU siguiente

**Interpretación de descripciones libres de mercancía en la captura.** Conectar el servicio de
#368 al flujo real: el texto libre de una solicitud se convierte en posiciones, el motor las
valida y recomienda, y lo que se rechaza se muestra con su motivo (nunca se inventa un peso o una
medida).

- Medir con texto de capturas reales, que se escribe peor que el sintético (abreviaturas,
  pulgadas, dos mercancías en una frase).
- Mostrar la explicación de #369 junto a la recomendación.

## Pendientes

| Pendiente | Responsable | Para qué |
|---|---|---|
| Crear la workspace y la llave de Anthropic del Cubicador, y poner `ANTHROPIC_API_KEY`, `ANTHROPIC_WORKSPACE_ID` e `IA_SOMBRA_HABILITADA=true` en el Cubicador de QA | Bryan, con quien administre la cuenta de Anthropic y el despliegue | Encender el modo sombra |
| Crear la tabla `iaSombra` donde corra el Cubicador (ya existe en `dbCubicador_Pruebas`; conviene encenderlo junto con el traslado a esa base, #423) | Bryan | Que el modo sombra pueda registrar |
| Importar y validar cargas reales (`evaluacion-ia/cubicador/importar_cargas_reales.py`) y repetir la evaluación de la IA | Bryan | Medir el acierto con datos reales |
| Cerrar #367 (tras verificar en QA) y #368 | Sara | Están terminadas pero abiertas |

## Cómo reproducir la medición

```bash
# gratis: arma los mensajes sin llamar al modelo
python evaluacion-ia/cubicador/evaluar_ia.py --simular

# de verdad (necesita ANTHROPIC_API_KEY y, si la llave es de organización, ANTHROPIC_WORKSPACE_ID)
python evaluacion-ia/cubicador/evaluar_ia.py --salida-md evaluacion-ia/cubicador/informe_ia_semilla.md

# un caso, varias veces
python evaluacion-ia/cubicador/evaluar_ia.py --solo S-19 --repeticiones 3
```

La corrida completa (22 llamadas) costó unos US$0,24.

## Aprobación

| Rol | Nombre | Decisión | Fecha |
|---|---|---|---|
| Responsable de la issue | | | |
| Líder técnico | | | |
