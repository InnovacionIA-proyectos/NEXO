# S2-HU-04.7 — Dataset de pliego realista y validación muestral del costeo

Rama: `18-s2-hu-047-pliego-de-prueba-realista-y-validación-muestral-del-costeo`.

Objetivo de la HU: construir un dataset de pliego de 300+ líneas con casos borde y
validar por muestreo que las propuestas calculadas cumplen las reglas de negocio del
costeo (regla del mayor, piso inviolable, fuente correcta), documentadas en
[`ReglaDeCosteo.cs`](../../../src/CabinaFletes.Domain/Costeo/Reglas/ReglaDeCosteo.cs).

## Dónde está el dataset

El dataset vive junto a las pruebas que lo consumen, no en esta carpeta, para que se
compile y ejecute como parte normal de la suite (no como un README que hay que creer
por las buenas):

- **Dataset (JSON, estructurado)**: [`tests/CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.json`](../../CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.json)
  — 320 líneas, versionado en el repo.
- **Dataset (Excel, mismo contenido)**: [`tests/CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.xlsx`](../../CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.xlsx)
  — insumo del flujo real de carga (ver "¿Por qué JSON y también Excel?" abajo).
- **Prueba**: [`tests/CabinaFletes.Domain.Tests/PliegoDatasetValidacionTests.cs`](../../CabinaFletes.Domain.Tests/PliegoDatasetValidacionTests.cs)
  — corre `ReglaDeCosteo` real sobre el dataset JSON y compara contra la regla esperada.

Reproducir:
```
dotnet test tests/CabinaFletes.Domain.Tests --filter FullyQualifiedName~PliegoDatasetValidacionTests
```

**Resultado real de esta sesión (no simulado):** `95/95` en verde — las 61 pruebas
que ya existían en `CabinaFletes.Domain.Tests` más las 34 nuevas de esta HU. Cero
regresiones.

## Cómo se generó

El dataset es determinista (sin `Math.random`, todo derivado del índice de línea)
para que sea revisable a mano y reproducible byte a byte. El script generador
(Node.js, ~200 líneas) no se versionó porque no aporta valor de negocio una vez el
JSON existe; si hace falta regenerarlo o ampliar el dataset, pedirlo y se rehace en
minutos con la misma lógica descrita abajo.

## ¿Por qué JSON y también Excel?

El pliego real no llega a `ReglaDeCosteo` en formato Excel. Hay dos pasos separados:

1. **Excel → análisis tolerante.** `ExcelSolicitudesAdapter.AnalizarAsync` (usa
   ClosedXML, la misma librería que ya usa el proyecto) recibe el `.xlsx`, detecta la
   fila de encabezado y devuelve la tabla como texto plano por celda — no sabe todavía
   qué columna es "origen" o "peso". Cada cliente manda su propia plantilla.
2. **El frontend mapea columnas** y arma el JSON estructurado que viaja a `POST
   /api/solicitudes/{id}/lineas:bulk` (`CargarLineasBulkCommand`, con campos ya
   tipados). Recién ahí, y más adelante en el auto-costeo, entra en juego
   `ReglaDeCosteo`.

Por eso el dataset principal (el que valida la regla de negocio) es el JSON: modela
el punto donde vive la lógica que pide la HU. El **Excel se agregó como insumo
adicional** para cubrir también el primer paso — la subida real que hace un
comercial — con las mismas 320 líneas.

`pliego-300-lineas.xlsx` tiene 3 hojas:

- **`Pliego`**: solo las columnas que un comercial diligenciaría (Origen, Destino,
  Tipo Vehículo, Peso, etc.). **No** trae tarifas SICE-TAC/plaza/histórico — esas no
  llegan en el Excel real, las busca el sistema después.
- **`QA - Trazabilidad`**: por cada `Id`, qué "encontraron" las fuentes simuladas y
  la propuesta esperada — para cruzar cada fila del Excel con el resultado
  documentado en este README. No es una hoja que traería un pliego real, es evidencia
  de QA.
- **`Leame`**: la misma explicación de arriba, dentro del archivo.

**Verificado con el código real, no solo generado:** se corrió
`ExcelSolicitudesAdapter.AnalizarAsync` (el adaptador real de infraestructura, sin
mocks) contra este `.xlsx`. Resultado real de esa corrida:

```
Hoja 'Pliego': fila encabezado=1, columnas=14, filas de datos totales=320, filas devueltas=320
  Encabezados: Id | Origen | Destino | Tipo Vehiculo | Carroceria | Tipologia de Carga | Peso (kg) | ...
```

Detecta el encabezado y las 320 filas correctamente. Esta verificación no quedó como
prueba automática dentro de `CabinaFletes.Domain.Tests` porque ese proyecto, por
arquitectura, no depende de `CabinaFletes.Infrastructure` (Domain es la capa más
interna); tampoco se agregó a `CabinaFletes.Api.Tests` porque esa suite necesita la
base de datos SQL Server corporativa para arrancar su fixture, no disponible en este
entorno (ver "Fuera de alcance"). Quedó como verificación manual documentada aquí,
reproducible con un arnés de una sola clase si hace falta repetirla.

## Interpretación de "piso vencido"

El criterio de aceptación menciona "piso vencido" como caso borde, pero el dominio no
tiene un concepto de vencimiento del piso SICE-TAC (siempre se toma el período
vigente más reciente disponible en DWSice). Lo que sí existe, y es lo que se modeló
aquí, es la **vigencia de la tarifa de plaza**: `ReglaDeCosteo.DecidirConTarifario(
..., tarifarioVencido: true)` y la entidad `AjusteVigenciaPlaza`. Cuando el tarifario
está vencido, la plaza se descarta aunque sea la más alta y gobierna el piso
SICE-TAC — esto es lo que cubren los casos `tarifario_vencido_con_piso` y
`tarifario_vencido_sin_piso`. Si negocio quiso decir otra cosa con "piso vencido",
avisar para ajustar el dataset.

## Catálogo de casos borde (320 líneas)

| Categoría | Líneas | ids | Qué prueba |
|---|---|---|---|
| Normal (regla del mayor, incl. empates) | 260 | 1–260 | Plaza gana, SICE-TAC gana, empate gana plaza — en volumen |
| Solo SICE-TAC, con piso disponible | 10 | 261–270 | Ignora la plaza aunque sea muy superior |
| Solo SICE-TAC, sin piso disponible | 5 | 271–275 | Requiere atención manual, no cae a la plaza |
| Sin tarifa de plaza | 15 | 276–290 | Gobierna el piso en solitario |
| Tarifario/piso vencido, con piso disponible | 7 | 291–297 | Plaza descartada, gobierna el piso |
| Tarifario/piso vencido, sin piso disponible | 3 | 298–300 | Requiere atención manual |
| Retorno de contenedor vacío | 10 | 301–310 | El flag no interfiere con la regla del mayor/piso de la línea principal |
| Sin ruta | 2 | 311–312 | Datos incompletos — no llega a tener candidatos |
| Peso malformado (negativo / no numérico) | 2 | 313–314 | Datos malformados — rechazo en captura |
| Sin configuración de vehículo | 1 | 315 | La ausencia no rompe la regla del mayor |
| Sin tipología | 1 | 316 | Ídem |
| Sin ninguna fuente (todo vacío) | 1 | 317 | Requiere atención manual, sin histórico de referencia |
| Fuente externa cae | 3 | 318–320 | Insumo exacto del escenario Gherkin "fuente caída" |

**320 líneas > 300 exigidas por el criterio de cierre**, con holgura para no rozar el
mínimo.

## Dos capas de validación, no solo la muestra

1. **Invariante de piso, en las 320 líneas** (no solo en la muestra):
   `Ninguna_propuesta_de_las_320_lineas_queda_por_debajo_del_piso_sicetac_vigente` —
   para toda línea con piso SICE-TAC comparable, la propuesta calculada nunca es
   menor a ese piso. Es más fuerte que un muestreo: cubre el 100% de la población.
2. **Muestra estratificada, campo a campo** (lo que pide literalmente el criterio de
   cierre: "propuesta vs regla esperada por caso") — 26 líneas representativas de
   cada categoría, comparando valor, fuente y contenido de la justificación devueltos
   por `ReglaDeCosteo` real contra la regla esperada documentada en el dataset.

## Tabla de la muestra (propuesta calculada vs regla esperada)

Todas las filas de esta tabla corresponden a un caso `[Theory]` que corrió en verde
en esta sesión.

### normal — regla del mayor (cubre plaza gana, SICE-TAC gana y empate)
| id | sicetac | plaza | histórico | valor esperado | fuente esperada |
|---|---|---|---|---|---|
| 3 | 874.000 | 942.000 | — | 942.000 | plaza |
| 9 | 1.096.000 | 992.000 | 1.036.000 | 1.096.000 | sicetac |
| 10 | 1.133.000 | 1.133.000 | — | 1.133.000 | plaza (empate) |
| 105 | 948.000 | 998.000 | 888.000 | 998.000 | plaza |
| 188 | 2.169.000 | 2.129.000 | — | 2.169.000 | sicetac |

### sin_tarifa_plaza — gobierna el piso en solitario
| id | sicetac | plaza | valor esperado | fuente esperada |
|---|---|---|---|---|
| 276 | 700.000 | — | 700.000 | sicetac |
| 283 | 847.000 | — | 847.000 | sicetac |
| 290 | 994.000 | — | 994.000 | sicetac |

### tarifario_vencido_con_piso — plaza descartada aunque sea mayor
| id | sicetac | plaza (vencida, sería mayor) | valor esperado | fuente esperada |
|---|---|---|---|---|
| 291 | 1.000.000 | 1.500.000 | 1.000.000 | sicetac |
| 294 | 1.090.000 | 1.590.000 | 1.090.000 | sicetac |
| 297 | 1.180.000 | 1.680.000 | 1.180.000 | sicetac |

### tarifario_vencido_sin_piso — requiere manual
| id | sicetac | plaza (vencida) | valor esperado | requiere manual |
|---|---|---|---|---|
| 298 | — | 2.000.000 | — | true |
| 299 | — | 2.100.000 | — | true |
| 300 | — | 2.200.000 | — | true |

### retorno_contenedor_vacio — el flag no altera la regla del mayor/piso
| id | sicetac | plaza | retornaContenedor | valor esperado | fuente esperada |
|---|---|---|---|---|---|
| 301 | 900.000 | 980.000 | true | 980.000 | plaza |
| 305 | 960.000 | 1.040.000 | true | 1.040.000 | plaza |
| 310 | 1.035.000 | 1.005.000 | true | 1.035.000 | sicetac |

> Nota de alcance: el flete de **devolución** del contenedor (línea 6 exclusiva) es
> un renglón aparte que arma `PropuestaTarifaAdapter` (`oficial?.Retorno`), fuera de
> `ReglaDeCosteo`. Esta muestra prueba que el flag `RetornaContenedor` **no
> interfiere** con la propuesta de la línea principal — no prueba el cálculo del
> renglón de devolución en sí, que requiere el adaptador completo con EF (ver "Fuera
> de alcance").

### solo_sicetac — ignora la plaza por completo (verificado por inspección de `PropuestaTarifaAdapter.ProponerLineaAsync`, no vía `ReglaDeCosteo`: esa rama no vive en el dominio puro)
| id | sicetac | plaza (mucho mayor, se ignora) | valor esperado | fuente esperada |
|---|---|---|---|---|
| 261 | 900.000 | 5.000.000 | 900.000 | sicetac |
| 265 | 1.000.000 | 5.000.000 | 1.000.000 | sicetac |
| 270 | 1.125.000 | 5.000.000 | 1.125.000 | sicetac |
| 271 | — | 3.000.000 | — | requiere manual (no cae a la plaza) |
| 273 | — | 3.000.000 | — | requiere manual |
| 275 | — | 3.000.000 | — | requiere manual |

### otros casos borde
| id | caso | sicetac | plaza | valor esperado | fuente esperada |
|---|---|---|---|---|---|
| 315 | sin_configuracion_vehiculo | 1.100.000 | 1.300.000 | 1.300.000 | plaza |
| 316 | sin_tipologia | 950.000 | 900.000 | 950.000 | sicetac |
| 317 | todo_vacio | — | — | — | requiere manual, sin referencia histórica |

### datos incompletos/malformados — documentados, no ejecutables contra `ReglaDeCosteo`
| id | caso | por qué no pasa por la regla de dominio |
|---|---|---|
| 311, 312 | sin_ruta | `PropuestaTarifaAdapter` corta antes ("La línea no tiene ruta definida"); requiere `Include(l => l.Ruta)` de EF |
| 313, 314 | peso_malformado | `[Range(0, ...)]` en `LineaSolicitud.Peso` lo rechaza en la validación de modelo del API (400), antes del auto-costeo |

## Desviaciones encontradas

**Ninguna.** Las 320 líneas del dataset (invariante de piso) y las 26+6 líneas de la
muestra estratificada (comparación campo a campo) coinciden exactamente con la regla
de negocio documentada en `ReglaDeCosteo.cs` y en `PropuestaTarifaAdapter.cs`. No hay
issues que abrir por esta verificación.

Si una corrida futura de este dataset (tras un cambio de código) encuentra una
desviación real, este README es el lugar para documentarla con: id de la línea,
propuesta obtenida, regla esperada, y el título/cuerpo del issue a abrir contra el
milestone de esta HU. No se creó ningún issue en esta sesión porque no hubo hallazgo
que reportar, y además este entorno no tiene `gh` CLI configurado (no hay forma de
abrirlo automáticamente aunque hubiera hecho falta).

## Insumo Gherkin

De [`docs/contrato-integracion/escenarios.feature`](../../../docs/contrato-integracion/escenarios.feature):

- **`Escenario: Cargar un pliego responde de inmediato`** (`Dado un pliego de 300
  líneas`) → insumo, según en qué punto del flujo se quiera entrar la prueba:
  - Desde la subida real: `pliego-300-lineas.xlsx` (hoja `Pliego`), para el endpoint
    de análisis de Excel.
  - Desde el bulk ya mapeado: las 320 líneas de `pliego-300-lineas.json`, para
    `POST /api/solicitudes/{id}/lineas:bulk`.
- **`Escenario: Un pliego con líneas sin resolver termina en estado parcial`**
  (`Dado un pliego donde una fuente externa falla para 3 líneas`) → insumo: las 3
  líneas con `fuenteCae: true`, ids **318, 319 y 320** (campo `fuenteCaeIds` en la
  raíz del JSON). Al costear el pliego completo, esas 3 deben terminar como
  `TipoPendienteDeCosteo.Tecnica` y las 317 restantes costeadas — el comportamiento
  de ese contrato ya está probado en `CostearLineaCommandTests` y
  `AutoCosteoSagaCargaTests`; este dataset aporta el insumo de 300+ líneas con la
  proporción real que pide el escenario, no la mecánica de reintento (que no depende
  de cuáles 3 líneas sean).

Se agregó un comentario en `escenarios.feature`, encima de cada uno de los 2
escenarios, apuntando a este dataset — sin tocar ningún `Dado/Cuando/Entonces`.

## Fuera de alcance (declarado)

- No se montó un test de integración real contra `POST
  /api/solicitudes/{id}/lineas:bulk` con SQL Server real: los QA previos de esta
  misma carpeta (`hu-04.5-signalr-avisos-respaldo`, issue #3) ya documentaron que esa
  BD corporativa de pruebas no está disponible en este entorno. El dataset queda
  listo para usarse ahí el día que haya acceso — mismo JSON, sin cambios.
- El cortocircuito de rutas "solo SICE-TAC" y el renglón de retorno de contenedor
  vacío viven en `PropuestaTarifaAdapter` (capa de infraestructura, con EF), no en
  `ReglaDeCosteo` (dominio puro). Se verificaron por inspección de código y con
  aserciones directas sobre el dataset, no ejecutando el adaptador real — eso
  requeriría la BD corporativa mencionada arriba.

---

## Verificación QA independiente (Kevin Cano, situado en esta rama antes de aprobar el PR #69)

Reproducido, no solo leído: build `--no-cache` (0 errores, 1 advertencia conocida),
y las 3 suites sin filtro contra SQL Server real — `Domain.Tests` **95/95** (coincide
exacto: 61 preexistentes + 34 nuevas), `Application.Tests` 75/75, `Api.Tests` 54/54.
Cero regresiones.

### Cerrado uno de los dos "fuera de alcance" declarados arriba

El propio PR deja explícito que no hay integración real contra `POST
/api/captura-solicitudes` ni contra el endpoint de análisis de Excel, por falta de
BD corporativa. Usando la BD local de Docker como sustituto (mismo patrón que el
resto de esta carpeta QA), sí se pudo verificar lo siguiente **contra la app
realmente levantada**, no en aislamiento:

**1) El endpoint real de análisis de Excel** (`POST
/api/captura-solicitudes/analizar-excel`), con el `.xlsx` real subido por HTTP
multipart — no la llamada aislada al adaptador que hizo el autor a mano:

```
hoja: Pliego | filaEncabezado: 1 | encabezados: 14 | totalFilas: 320 | filas devueltas: 320
encabezados: Id | Origen | Destino | Tipo Vehiculo | Carroceria | Tipologia de Carga |
             Peso (kg) | Linea de Negocio | Clase de Viaje | Clase de Contenedor |
             Exclusivo | Consolidado | Retorna Contenedor Vacio |
             Ciudad Devolucion Contenedor
```
Coincide exacto con lo que el autor documentó arriba de su propia corrida manual.

**2) Las 320 líneas del dataset, sometidas contra `POST /api/captura-solicitudes`
real** (script: [`verify-captura-carga-real.mjs`](verify-captura-carga-real.mjs)) —
la escala más grande sometida contra la app viva en toda esta sesión de QA:

- Las 4 líneas `sin_ruta`/`peso_malformado`, enviadas **una por una**: las 4
  rechazadas con `400`, tal como documenta la tabla de arriba. Confirmado en el log
  de la API que el rechazo de la línea con `peso: null` es la excepción de
  validación esperada (`PESO_REQUERIDO`), capturada limpiamente por el middleware —
  no un crash.
- Las **316 líneas válidas restantes, en una sola petición**: `201` en 1704 ms,
  `rutasCreadas=20`, `vehiculosCreados=12` (resueltos desde el texto libre real del
  dataset, sin que el cliente resuelva nada de antemano), `costeoEnProceso=true`
  (correcto: >10 líneas difiere el auto-costeo, `UmbralCosteoDiferido=10`).
- El costeo diferido en segundo plano **no dejó nada colgado**: verificado por
  consulta directa a `lineas_solicitud` — las 316 quedaron en `PorCostear`, el
  degradado correcto y documentado (`AutoCostearAsync` es best-effort) para un
  entorno sin `ConnectionStrings:DwSice/DwCubos` configuradas. No es un defecto: es
  la misma limitación de acceso corporativo que documenta la issue #3.
- Estado del sistema después: `/health` sano, 0 líneas `fail:`/`crit:` reales en el
  log (la única coincidencia de "Exception" es la validación esperada del punto
  anterior), las 7 colas de RabbitMQ de este flujo en 0 mensajes — este camino de
  captura no usa el broker (es `Task.Run` en proceso, a diferencia del flujo
  `/lineas:bulk` de la HU-04.2, que sí es asíncrono vía saga).

**Sigue fuera de alcance** (correctamente, según lo documentado arriba): el
cortocircuito "solo SICE-TAC" y el renglón de retorno de contenedor vacío contra el
adaptador real con datos que sí produzcan una tarifa — eso exige la matriz SICE-TAC
real de `DWSice`, que sigue sin estar disponible en este entorno (issue #3, sin
cambios).

### Conclusión

Las 95/95 pruebas y el hallazgo de "ninguna desviación" del autor se reprodujeron
exactos. La verificación manual del Excel y el flujo de captura real, que el autor
dejó fuera de alcance, se automatizaron y confirmaron contra la app viva a escala
real (316 líneas en una sola petición, la mayor de esta sesión). Sin objeciones para
mergear.
