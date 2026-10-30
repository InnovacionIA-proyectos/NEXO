# Acta: decisión de alcance de la IA en el Cubicador (go / no-go)

- **Issue:** S6-HU-35.5 (#370)
- **Fecha de apertura:** 2026-10-30
- **Estado:** decisión **preliminar**. Se confirma cuando se completen los datos del modo sombra
  (sección 3) y se firme la aprobación (sección 8).

## 1. Qué se decide

Si la IA (Claude) amplía su papel en el Cubicador más allá del modo sombra, y con qué alcance
queda la HU siguiente.

La decisión se apoya en dos mediciones que responden preguntas distintas:

| Medición | Pregunta que responde | Estado |
|---|---|---|
| IA contra el dataset dorado | ¿Cuánto **acierta** la IA frente a respuestas validadas? | Hecha (sección 2) |
| Modo sombra en QA | ¿Cuánto **se parece** la IA al motor con tráfico real, cuánto falla y cuánto cuesta? | En recolección (sección 3) |

## 2. Resultado: la IA contra el dataset dorado

La IA se midió con el mismo prompt, el mismo esquema de salida y la misma validación que usa el
modo sombra en producción (`cubicador/application/ia_sombra/`), sobre los 22 casos validados de
la semilla y con las mismas métricas que la línea base del motor. Los dos informes tienen la
misma huella de dataset (`78884e7c8037`): se midieron sobre exactamente los mismos casos.
Herramienta: `evaluacion-ia/cubicador/evaluar_ia.py`; informes completos:
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

## 3. Resultado: modo sombra en QA

### 3.1 Plan de recolección

| | |
|---|---|
| Estado al 2026-10-30 | **Apagado.** La tabla `dbo_cubicador.iaSombra` no existe en la base de QA y el Cubicador de QA no tiene llave de Anthropic. |
| Prueba local (2026-10-30) | **Funciona de punta a punta** con el modelo real: la app respondió igual que siempre y dejó una fila en `iaSombra` con `Estado = OK`, la misma flota que el motor, 4,8 s y ~2.400 tokens. |
| Tráfico esperado | QA recibió 134 recomendaciones entre el 2026-10-14 y el 2026-10-29: unas 15 por día hábil. |
| Ventana | **10 días hábiles** desde el encendido, o **150 comparaciones con `Estado = OK`**, lo que ocurra después. |
| Costo esperado | ~US$0,17 por día y ~US$2 en toda la ventana. El tope diario por defecto (1 millón de tokens, ~US$3,4) no debería alcanzarse. |
| Qué no cambia | La respuesta al usuario: la comparación corre en segundo plano y el modelo no ve lo que respondió el motor. |

Pasos para encenderlo (detalle en `docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md` §7):

1. Aplicar `cubicador/database/esquema/367_ia_sombra.sql` en la base que usa hoy el Cubicador de
   QA (`dbTarifario_Nexo_Pruebas`, esquema `dbo_cubicador`). Es una tabla nueva: no toca las
   existentes.
2. En Dokploy, app `cubicador-api`: `IA_SOMBRA_HABILITADA=true`, `ANTHROPIC_API_KEY` y, si la
   llave es de organización, `ANTHROPIC_WORKSPACE_ID`. Redesplegar.
3. Pedir una recomendación desde NEXO y comprobar que aparece una fila en `iaSombra` con
   `Estado = OK`.

Si el Cubicador se traslada a su base propia (`dbCubicador_Pruebas`) antes de cerrar la ventana,
las filas ya recolectadas se copian a la tabla `dbo.iaSombra` de esa base, que ya existe.

### 3.2 Resultados (se completa al cerrar la ventana)

Con las consultas de `docs/ia/ESTUDIO-CUBICADOR-IA.md` §7, de solo lectura.

| Métrica | Resultado |
|---|---|
| Ventana (desde / hasta) | |
| Intentos / comparaciones con `Estado = OK` | |
| Intentos fallidos, por `Estado` | |
| Acuerdo con el motor en asignar / no asignar | |
| Flota exacta igual a la del motor | |
| Misma configuración (sin mirar carrocería) | |
| Misma cantidad / la IA pide más / la IA pide menos | |
| Propuestas que exceden peso / volumen / pierden unidades | |
| Latencia del modelo p50 / p95 | |
| Costo total / por cubicación | |

**Revisión manual obligatoria:** cada comparación en la que la IA pide **menos** vehículos que el
motor se revisa una por una, para saber si es otro caso como S-19 o un error del motor.

## 4. Lo que la IA ya hace bien junto al motor

- **Interpretar descripciones libres de mercancía** en posiciones estructuradas (#368): 23/23
  casos exactos y 7/7 rechazos correctos; el motor valida todo lo que sale.
- **Explicar la recomendación** en lenguaje natural (#369).

En los dos casos la IA no decide: prepara datos o explica, y el motor sigue mandando.

## 5. Criterios

Los criterios propuestos en la issue eran «acierto igual o superior al del motor» y «p95 de
latencia no peor».

| Criterio | Resultado con el dataset dorado | Comentario |
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
5. En modo sombra, **menos del 5 % de intentos fallidos** (tiempo agotado, límite del proveedor o
   respuesta inválida).

## 6. Decisión

**Preliminar (con el dataset dorado):**

- **No-go** a que la IA decida la flota o reemplace al motor. No supera al motor y falla
  sistemáticamente en lo geométrico, que es justo lo que el motor resuelve.
- **Go** a encender el modo sombra en QA para recolectar datos reales (sección 3). Es barato y no
  afecta la respuesta.
- **Go** a usar la IA donde complementa al motor sin decidir: interpretación de texto libre y
  explicación de la recomendación.

**Cómo se confirma con el modo sombra.** Al cerrar la ventana:

- Si la IA pide menos vehículos que el motor en algún caso que la revisión manual confirma como
  error de la IA, o el acuerdo en flota exacta es menor al 95 %, el no-go queda confirmado.
- Si el acuerdo es alto y no hay casos de seguridad, el no-go se mantiene igual (parecerse al
  motor no es superarlo), pero la siguiente reevaluación se hace con el dataset de cargas reales
  validadas contra los criterios de la sección 5.

**Final (se completa al cerrar la ventana):**

## 7. Alcance de la HU siguiente

**Interpretación de descripciones libres de mercancía en la captura.** Conectar el servicio de
#368 al flujo real: el texto libre de una solicitud se convierte en posiciones, el motor las
valida y recomienda, y lo que se rechaza se muestra con su motivo (nunca se inventa un peso o una
medida).

- Medir con texto de capturas reales, que se escribe peor que el sintético (abreviaturas,
  pulgadas, dos mercancías en una frase).
- Mostrar la explicación de #369 junto a la recomendación.

### Pendientes

| Pendiente | Responsable | Para qué |
|---|---|---|
| Aplicar `367_ia_sombra.sql` en `dbTarifario_Nexo_Pruebas` | Bryan | Que el modo sombra pueda registrar |
| Definir la llave del Cubicador (workspace propia, o la de organización con `ANTHROPIC_WORKSPACE_ID` mientras tanto), cargarla en Dokploy con `IA_SOMBRA_HABILITADA=true` y redesplegar | Bryan, con quien administre la cuenta de Anthropic y el despliegue | Encender el modo sombra |
| Correr las consultas de §7 al cerrar la ventana, revisar los casos en que la IA pide menos y completar las secciones 3.2 y 6 | Bryan | Cerrar el acta |
| Importar y validar cargas reales (`evaluacion-ia/cubicador/importar_cargas_reales.py`) y repetir la evaluación de la IA | Bryan | Medir el acierto con datos reales |
| Cerrar #367 (tras verificar en QA) y #368 | Sara | Están terminadas pero abiertas |

## 8. Aprobación

| Rol | Nombre | Decisión | Fecha |
|---|---|---|---|
| Responsable de la issue | | | |
| Líder técnico | | | |

## Anexo: cómo reproducir la medición contra el dataset dorado

```bash
# gratis: arma los mensajes sin llamar al modelo
python evaluacion-ia/cubicador/evaluar_ia.py --simular

# de verdad (necesita ANTHROPIC_API_KEY y, si la llave es de organización, ANTHROPIC_WORKSPACE_ID)
python evaluacion-ia/cubicador/evaluar_ia.py --salida-md evaluacion-ia/cubicador/informe_ia_semilla.md

# un caso, varias veces
python evaluacion-ia/cubicador/evaluar_ia.py --solo S-19 --repeticiones 3
```

La corrida completa (22 llamadas) costó unos US$0,24.
