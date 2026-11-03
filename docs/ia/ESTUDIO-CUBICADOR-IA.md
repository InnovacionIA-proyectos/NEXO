# Estudio comparativo del Cubicador: motor actual contra IA

S6-HU-35.1 (#366). Define cómo se mide si la IA mejora al Cubicador, con qué datos y contra qué punto de partida. La decisión de hasta dónde ampliar su alcance se toma después, con estos números (#370).

## 1. Qué se compara

El Cubicador recomienda qué vehículos y cuántos hacen falta para una carga (`POST /recomendaciones`, motor `seleccion_flota`). Es determinista, se verifica con pruebas y no inventa: ningún camión que proponga excede su peso ni su volumen.

La IA no lo reemplaza. Se evalúa como un candidato que recibe **la misma carga y el mismo catálogo** y responde en **la misma forma** (qué vehículos y qué lleva cada uno). Lo que se mide es si, corriendo en modo sombra, acierta tanto como el motor y si aporta algo que el motor no hace (entender descripciones libres de mercancía y explicar el resultado).

| Etapa | Qué hace la IA | Qué se mide | Issue |
|---|---|---|---|
| A. Misma decisión | Recomienda flota con la carga ya estructurada | Las cinco métricas de este documento | #367 |
| B. Interpretar | Lee la descripción de la mercancía y la vuelve tipo, cantidad y medidas | Acierto del tipo y de las medidas, y que todo pase por las validaciones del motor | #368 |
| C. Explicar | Cuenta por qué salió ese vehículo | Revisión humana con rúbrica (no es un número) | #369 |

Este documento fija las métricas de la etapa A y la línea base del motor. Las etapas B y C reusan el mismo dataset y agregan sus propios casos.

## 2. Las métricas

Cada tasa se reporta con su denominador (`aciertos/total`): un porcentaje sin el total no dice nada. Las cuentas están en `evaluacion-ia/cubicador/metricas.py` y tienen pruebas.

| Métrica | Cómo se calcula | Denominador |
|---|---|---|
| **Acierto del vehículo** | La flota propuesta es una de las flotas correctas del caso. Un vehículo es el par (configuración, carrocería): un 3S3 furgón no es un 3S3 plataforma. Se reporta también sin mirar la carrocería, para separar «eligió otro camión» de «eligió el camión correcto con otra carrocería». | Casos que se deben asignar y traen flotas correctas |
| **Flota mínima** | Cantidad de vehículos propuesta contra la mínima correcta. Se reporta aparte el **exceso** (un camión de más cuesta plata) y el **defecto** (menos de los que caben es prometer algo que no se puede cargar). No se promedian. | Casos asignables que el candidato asignó |
| **Respeto de capacidad y volumen** | Se **recalcula** con el catálogo, sin creerle al candidato: el peso de cada vehículo contra la capacidad de su combinación vehículo-carrocería (el 3S3 furgón son 30.000 kg, no los 34.000 del chasis), el volumen contra el volumen físico del vehículo, y que cada unidad pedida vaya en exactamente un vehículo. | Casos que el candidato asignó |
| **Latencia p95** | Percentil 95 por rango más cercano de todas las muestras (varias repeticiones por caso); se reportan también p50 y máximo. | Muestras |
| **Costo por cubicación** | `(tokens de entrada × precio de entrada + tokens de salida × precio de salida) / 1.000.000`, promediado por cubicación. Los tokens son los que reporta el proveedor. El motor no gasta tokens: su costo por llamada es 0. | Muestras |

Además se mide si **asignó o no** como el experto (por ejemplo, una pieza de 13 m debe terminar en `SIN_SELECCION_AUTOMATICA`/`EXTRADIMENSIONADO` y no en un camión).

**Cómo se leen.** El respeto de capacidad es una condición, no una nota: un candidato con una sola violación no puede pasar a producción, sin importar su acierto. Los umbrales del go/no-go los fija #370; el criterio de partida es que la IA no quede por debajo del motor en acierto del vehículo ni en flota mínima, y que su p95 y su costo sean aceptables para quien espera la recomendación.

## 3. El dataset dorado

Cada caso trae la carga (tipo de mercancía, cantidad, medidas, peso), la decisión correcta (vehículos y cantidad mínima, o «no asignar» con su motivo) y de dónde sale la etiqueta. Formato en `evaluacion-ia/cubicador/casos/*.json`.

### Lo que hay hoy: la semilla sintética

22 casos escritos a mano en `casos/semilla_sintetica.json`, con la respuesta correcta calculada por razonamiento físico (el vehículo más pequeño que cumple peso, volumen y medidas, y la cantidad de unidades por capa del piso para los de empaque). Cubren peso que manda, volumen que manda, piezas largas, extradimensión por medida y por peso, refrigerado, mercancías incompatibles, carga condicionada, dos posiciones del mismo producto, límites justo debajo y encima de un vehículo, y cargas donde el volumen alcanza pero el piso no.

Sirve para probar que la herramienta mide bien y para ver el comportamiento básico del motor. **No sirve para decidir:** son casos fáciles y no son cargas reales.

### Lo que falta: cargas reales con decisión humana

El criterio de la issue pide cargas reales. Salen de las líneas de solicitud que ya tienen vehículo elegido por una persona:

1. Alguien con acceso corre `evaluacion-ia/cubicador/extraer_cargas_reales.sql` (solo lectura; una fila por posición de carga). Sale únicamente lo que el motor necesita: sin cliente, ruta, descripciones ni texto libre, y los identificadores como un resumen irreversible.
2. `python evaluacion-ia/cubicador/importar_cargas_reales.py cargas.csv casos/reales_por_validar.json` agrupa las filas por carga (las líneas con el mismo `GrupoRutaId` son una sola carga repartida en camiones) y arma los casos.
3. Un experto de Operaciones revisa cada caso **sin ver lo que respondió el motor** y confirma o corrige la flota correcta; al terminar pone `etiquetaValidada: true`. El evaluador ignora los casos sin validar.
4. El archivo con datos reales **no se versiona en el repositorio**: se guarda en la carpeta compartida del equipo.

**Por qué no se toma la decisión registrada como verdad.**

- Si la línea traía la recomendación del Cubicador, la persona pudo aceptarla sin pensarla, y medir al motor contra su propia sugerencia lo deja siempre bien. Esos casos salen con `usoRecomendacion` para separarlos; los más valiosos son los que la persona eligió sin que el motor opinara.
- La persona pudo pedir más vehículos de los necesarios, o uno que el Cubicador no tiene (la configuración comercial incluye tipos como el 2S3, que el catálogo de cubicación no tiene). Esos casos salen con `observaciones`.

**Tamaño y composición (propuesta, para confirmar con Operaciones).** Al menos 100 cargas validadas, con un mínimo de 5 por tipo: homogénea, heterogénea, una carga que necesita varios vehículos, mercancía condicionada, refrigerada, extradimensionada, y las que están cerca del límite de un vehículo. Una quinta parte se reserva como prueba ciega: no se usa para ajustar instrucciones ni modelo, solo para la medición final. Los casos dudosos los revisan dos personas.

## 4. Cómo se mide

- Mismo dato y mismo catálogo para todos los candidatos. Hoy el catálogo es el de QA calcado (`catalogo_qa.py`: 5 vehículos, 9 tipos de mercancía). Si el catálogo real cambia, la línea base deja de ser comparable y se vuelve a medir.
- Cada caso se corre varias veces (50 en la línea base); la exactitud se toma de la primera corrida y la latencia y el costo de todas.
- La **latencia del motor se mide en proceso**, sin HTTP ni base de datos. La latencia que vive el usuario (NEXO llama al Cubicador por HTTP con un tope de 15 s) se mide aparte, en modo sombra (#367), para el motor y para la IA con el mismo borde.
- Para los modelos (Sonnet 4.6 y 5.5, esfuerzos `low`/`medium`/`high`) se reporta cada combinación por separado.
- La herramienta no se ejecuta en el CI: se corre bajo demanda y no toca base de datos ni red.

```
python evaluacion-ia/cubicador/evaluar.py                     # línea base con el dataset validado
python evaluacion-ia/cubicador/evaluar.py --salida-md informe.md --salida-json informe.json
python -m pytest evaluacion-ia/cubicador --noconftest          # pruebas de las cuentas y del dataset
```

Para evaluar la IA se escribe un adaptador con la forma de `adaptador_motor` (recibe un caso y devuelve una respuesta con vehículos, latencia y tokens); las métricas y el informe no cambian.

## 5. Línea base del motor actual

Medida sobre la **semilla sintética** (22 casos, 50 repeticiones), en un equipo de desarrollo; repetida el 2026-10-30 sobre la versión versionada del dataset (huella `78884e7c8037`, la misma con la que se midió la IA en la sección 8), con los mismos aciertos. Informe completo en `evaluacion-ia/cubicador/informe_linea_base_semilla.md`.

| Métrica | Resultado |
|---|---|
| Decisión (asignar / no asignar) | 100 % (22/22) |
| Motivo cuando no asigna | 100 % (2/2) |
| Acierto del vehículo (flota exacta) | 100 % (18/18) |
| Flota mínima (cantidad exacta) | 100 % (20/20) |
| Exceso medio de vehículos | 0 |
| Respeta el peso / el volumen / conserva las unidades | 100 % (20/20) en las tres |
| Latencia en proceso p50 / p95 / máx | 1,6 / 25,7 / 390 ms (varía entre corridas; siempre en milisegundos) |
| Costo por cubicación | 0 USD (0 tokens) |

**Cómo leerlo.**

- El 100 % dice que el motor no falla en lo básico, no que sea perfecto: con casos fáciles no hay dónde fallar. La línea base que cuenta es la que se mida con las cargas reales validadas; se obtiene corriendo el mismo comando cuando ese dataset exista.
- La latencia de p95 la dominan las cargas grandes (cientos de unidades); la mediana es de menos de un milisegundo. Es la del motor solo: no incluye red ni base.
- En la medición apareció un error **mío** al etiquetar: había contado 24 estibas de 120×100×150 por Minimula, y el motor decía 32. El motor tenía razón: girando la estiba con el lado de 100 cm hacia arriba caben dos alturas. Por eso las etiquetas de empaque son hipótesis hasta que las confirma un experto o un cálculo independiente, y por eso el dataset real se valida con personas.

**Lo que esta medición no ve.**

- «Respeto de capacidad y volumen» compara sumas de peso y de volumen. No verifica que las piezas quepan físicamente en el piso (eso es lo que hace el empaque 3D del motor). Una propuesta puede pasar esta verificación y aun así no entrar. Para la IA eso se cubre pasando siempre su respuesta por las validaciones del motor (#368).
- Hay una sola mercancía por caso en la mayoría; las cargas heterogéneas grandes están poco representadas hasta que lleguen las reales.

## 6. Qué queda pendiente para cerrar la issue

| Pendiente | Quién | Cómo se cierra |
|---|---|---|
| Correr la extracción de cargas reales y guardar el archivo en la carpeta compartida | Quien tenga acceso a la base (Datos) | Un SELECT de solo lectura |
| Revisión de las etiquetas por un experto de Operaciones | Operaciones, con apoyo de @bquinterobtst | Marcar `etiquetaValidada: true` caso por caso |
| Volver a medir la línea base con las cargas reales y actualizar la sección 5 | Quien corra el estudio | `evaluar.py` y pegar el informe |
| Confirmar el tamaño y la composición del dataset (sección 3) | Operaciones | Acuerdo escrito |

## 7. Modo sombra: las mismas métricas con tráfico real

Con `IA_SOMBRA_HABILITADA=true` el Cubicador compara, en segundo plano, cada recomendación del motor con la de Claude y deja una fila en `dbo_cubicador.iaSombra` (ver [ROTACION-LLAVE-ANTHROPIC.md](../despliegue/ROTACION-LLAVE-ANTHROPIC.md) §7). La respuesta al usuario no cambia ni se retrasa. El modelo **no ve** lo que respondió el motor.

Cada fila ya trae las cuentas de este documento, calculadas con el mismo criterio que `metricas.py`: acuerdo en la flota exacta y en la configuración, cantidad de vehículos, y el respeto de peso, volumen y unidades **recalculado con el catálogo** (no lo que el modelo dijo). Lo que no está en la tabla es la decisión humana correcta: esta medición dice cuánto se parece el modelo al motor, no cuánto acierta. Para eso sigue haciendo falta el dataset validado de §3.

Consultas de solo lectura (cambiar `dbo_cubicador` si el esquema es otro):

```sql
-- Qué pasó con cada intento
SELECT Estado, COUNT(*) AS intentos
FROM [dbo_cubicador].[iaSombra]
GROUP BY Estado
ORDER BY intentos DESC;

-- Acuerdo con el motor (solo las comparaciones que se pudieron hacer)
SELECT
    COUNT(*) AS comparadas,
    AVG(CASE WHEN CoincideResultado = 1 THEN 1.0 ELSE 0.0 END) AS acuerdo_en_asignar,
    AVG(CASE WHEN CoincideFlota = 1 THEN 1.0 ELSE 0.0 END) AS flota_exacta,
    AVG(CASE WHEN CoincideConfiguracion = 1 THEN 1.0 ELSE 0.0 END) AS misma_configuracion,
    AVG(CASE WHEN CantidadIa = CantidadMotor THEN 1.0 ELSE 0.0 END) AS misma_cantidad,
    AVG(CASE WHEN CantidadIa > CantidadMotor THEN 1.0 ELSE 0.0 END) AS ia_pide_mas,
    AVG(CASE WHEN CantidadIa < CantidadMotor THEN 1.0 ELSE 0.0 END) AS ia_pide_menos
FROM [dbo_cubicador].[iaSombra]
WHERE Estado = N'OK';

-- Seguridad: cuántas veces lo que propuso el modelo no se puede cargar
SELECT
    SUM(CASE WHEN IaRespetaPeso = 0 THEN 1 ELSE 0 END) AS excede_peso,
    SUM(CASE WHEN IaRespetaVolumen = 0 THEN 1 ELSE 0 END) AS excede_volumen,
    SUM(CASE WHEN IaConservaUnidades = 0 THEN 1 ELSE 0 END) AS pierde_o_repite_unidades
FROM [dbo_cubicador].[iaSombra]
WHERE Estado = N'OK';

-- Latencia del modelo (ms)
SELECT DISTINCT
    PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY LatenciaIaMs) OVER () AS p50_ms,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY LatenciaIaMs) OVER () AS p95_ms
FROM [dbo_cubicador].[iaSombra]
WHERE Estado = N'OK';

-- Costo por cubicación. Los precios son USD por millón de tokens: poner los vigentes del modelo.
DECLARE @precio_entrada decimal(10,4) = 2.0;
DECLARE @precio_salida decimal(10,4) = 10.0;
SELECT
    COUNT(*) AS llamadas,
    SUM(TokensEntrada * @precio_entrada + TokensSalida * @precio_salida) / 1000000.0 AS usd_total,
    SUM(TokensEntrada * @precio_entrada + TokensSalida * @precio_salida) / 1000000.0 / NULLIF(COUNT(*), 0) AS usd_por_cubicacion
FROM [dbo_cubicador].[iaSombra]
WHERE TokensEntrada > 0;
```

La latencia es la del modelo (su propia llamada), no la de la API del Cubicador: esa no cambia. Una fila con `Estado` distinto de `OK` cuenta como intento fallido (límite del proveedor, tiempo agotado, respuesta que no cumple el contrato) y entra en la tasa de fallos, no en el acuerdo.

## 8. La IA contra el dataset dorado

Con `evaluacion-ia/cubicador/evaluar_ia.py` la IA del modo sombra (mismo prompt, mismo esquema y misma validación que en producción) se midió sobre los mismos 22 casos y con las mismas métricas de la sección 5. Iguala al motor en todo menos en flota mínima (95 %): en el caso S-19 propone una Minimula donde hacen falta dos, y lo repite en cada corrida. Es justo el acomodo en el piso que se describe en la sección 5. Informe: `evaluacion-ia/cubicador/informe_ia_semilla.md`. La decisión que sale de aquí está en [`ACTA-DECISION-IA-CUBICADOR.md`](ACTA-DECISION-IA-CUBICADOR.md).
