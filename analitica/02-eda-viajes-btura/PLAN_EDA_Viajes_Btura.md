# Plan de EDA — Set de Viajes Buenaventura 2025–2026

**Historia:** `EDA sobre el set ya entregado (históricos)` **Objetivo:** analizar exploratoriamente el set que el negocio ya entregó al equipo de datos, evaluando calidad, distribuciones, patrones, vacíos y sesgos; y documentar explícitamente qué preguntas ese set **no** puede responder.

| Campo | Valor |
| :---- | :---- |
| Insumo principal | `Viajes-Btura-2025-2026.xlsx` (hoja `Hoja1`) |
| Insumo secundario | `inconsistencias_viajes 1.xlsx` (5 hojas de excepciones) |
| Diccionario de columnas | **Encontrado dentro del propio archivo de inconsistencias**: hoja `Distancia_0`, fila 2 (definición por columna) \+ filas 21–31 (catálogo CLASE FWO). Transcrito en el Anexo D. El Google Sheets externo sigue sin acceso pero ya no es bloqueante |
| Volumen | 41.237 filas × 17 columnas |
| Ventana temporal | 2025-04-01 → 2026-04-07 (371 días con registro) |
| Granularidad observada | 1 fila ≈ 1 posición de orden de flete (FO), **no** 1 viaje — a confirmar |
| Fecha del plan | 2026-09-01 |

### Cambios en la v2 (tras hallar el diccionario embebido)

| \# | Qué cambió | Efecto |
| :---- | :---- | :---- |
| 1 | Se encontró un **diccionario interno** en la hoja `Distancia_0` (fila 2 \+ catálogo CLASE FWO en filas 21–31) | R-01 cerrado. De 12 preguntas al negocio quedan 4 |
| 2 | **Corrección de un hallazgo falso:** las "13 filas de `Distancia_0` ausentes del dataset" no existían — eran el diccionario leído como si fuera dato | R-02 retirado. Nueva regla de carga en §2.4 |
| 3 | El **18,2 % de nulos de equipo es estructural**, no un defecto (= PKG \+ PRD, tal como manda el diccionario) | Hallazgo baja de severidad Alta a Baja. Defecto real: **3 registros** |
| 4 | El **21,7 % de `Origen = Destino` es legítimo** en un 88,7 % (descontenerización OHUB \+ OH3P) | Defecto real acotado a **978 registros** OPIM \+ OPTN |
| 5 | `Costo Producción` **es** la tarifa de venta (872/874 verificado por valor \+ definición oficial) | R-03 pasa de sospecha a hecho reportable |
| 6 | `Tipo` está definido como "puede omitirse" | Se resuelve la redundancia: manda `ID Medio Transporte FO` |
| 7 | El catálogo declara **10 líneas de negocio y el set trae 7** | Nueva limitación §12.1-bis y nuevo riesgo R-06 |

> **Nota de alcance.** Este es un EDA sobre un set entregado, no un proyecto de ingeniería de datos. El objetivo no es *arreglar* el dato sino **caracterizarlo y dejar por escrito su capacidad y sus límites**. Toda corrección que se identifique se documenta como recomendación, no se aplica sobre el crudo.

---

## 0\. Criterio de cierre (Definition of Done)

El EDA se considera terminado únicamente cuando los tres entregables existen y están versionados:

- [ ] **Notebook reproducible y versionado en el repo** — corre de principio a fin en limpio (`Restart & Run All`) sobre el crudo inmutable, sin rutas locales ni celdas manuales.  
- [ ] **Hallazgos documentados** — calidad, distribuciones, patrones y vacíos, cada uno con evidencia cuantificada (conteo \+ % \+ ejemplo).  
- [ ] **Lista explícita de limitaciones** — qué NO se puede responder con este set, y por qué (columna faltante / granularidad / cobertura / calidad).

Criterios de aceptación transversales:

- [ ] Todo número afirmado en el informe es reproducible desde una celda del notebook.  
- [ ] Ninguna conclusión se apoya en una columna cuyo significado no esté confirmado con el negocio.  
- [ ] Las decisiones tomadas ante ambigüedad quedan registradas en el log de supuestos (§13).

---

## 1\. Fase 0 — Encuadre antes de tocar el dato

**Objetivo:** no explorar a ciegas. Sin esto, el EDA produce gráficos bonitos que nadie usa.

### 1.1 Preguntas de negocio a las que este set debería servir

Levantar con el dueño del dato y dejarlas escritas *antes* de analizar. Formato sugerido:

| \# | Pregunta de negocio | Columnas que la responderían | ¿Set suficiente? |
| :---- | :---- | :---- | :---- |
| P1 | ¿Cuánto se movió desde/hacia Buenaventura por mes? | `Fecha Creación FO`, `Origen`, `Destino` | (llenar) |
| P2 | ¿Cuál es el margen por ruta / cliente / tipo de vehículo? | `Tarifa venta al cliente`, `Flete producción FO` | (llenar) |
| P3 | ¿Qué participación tiene flota propia vs tercero? | `Propiedad` | (llenar) |
| … | … | … | … |

Esta tabla es el insumo directo de la §12 (limitaciones): toda pregunta que quede en "No" o "Parcial" se traslada allí con su razón.

### 1.2 Definiciones — estado tras hallar el diccionario interno

El diccionario embebido en `Distancia_0` (Anexo D) resuelve buena parte de estas preguntas sin depender del negocio. Estado actualizado:

| \# | Pregunta | Estado | Respuesta / qué falta |
| :---- | :---- | :---- | :---- |
| 1 | ¿Qué representa un registro? | ⚠️ Parcial | El diccionario define `FO` como "equivale al número del viaje", pero hay 128 valores de `FO` repetidos (§4.2). El grano sigue sin cerrar |
| 2 | ¿`FO` es identificador único? | ⚠️ Corregido (2026-09-08) | Ya **no** se sostiene que los repetidos "sean un defecto" sin más — la verificación sobre los 128 grupos completos (§4.2) muestra que el 100 % comparte vehículo y fecha pero difiere en cliente y/o peso, consistente con que `FO` identifique el **viaje del vehículo** (puede consolidar varios clientes), no la orden de un cliente. Pendiente de confirmar con negocio |
| 3 | ¿`Fecha Creación FO` es creación o ejecución? | ✅ Resuelto | "Fecha en la cual fue creada la FO **en el sistema**" → es **administrativa**. Confirma que el set NO sirve para análisis de tiempos operativos (refuerza L-01) |
| 4 | Semántica de las dos monetarias | ✅ Resuelto | `Tarifa Venta Cliente` \= "tarifa de venta del viaje realizado"; `Flete Producción FO` \= "costo de producción o flete que se paga al Transportista". Falta solo moneda e IVA |
| 5 | Unidad de `Peso Neto` | ❌ Abierto | Define el concepto ("carga sin tara del vehículo") pero **no la unidad**. kg vs t sigue sin confirmar |
| 6 | Unidad de `Distancia Total` | ✅ Resuelto | "Distancia total, **en KM**, de la operación de transporte". Falta si es planificada o real |
| 7 | Jerarquía de las columnas de equipo | ✅ Resuelto | `Grupo de Equipo FWOP` aplica **solo** a posición TUR (CAM \= camión, CN \= contenedor); `Clase Equipo FOP` aplica **solo** a grupos CN y CAM; `Tipo` está definido literalmente como **"Puede omitirse"** → `ID Medio Transporte FO` es la fuente de verdad |
| 8 | `Posición Principal FWOP` | ✅ Resuelto | PKG \= Paquete, PRD \= Producto, TUR \= Transportation Unit Resource |
| 9 | `ID Clase Documento FWO` | ✅ Resuelto | Catálogo completo de 10 códigos (Anexo D). Explica el 21,7 % de Origen \= Destino — ver §5.5 |
| 10 | Maestro de clientes para `BP Cliente` | ❌ Abierto | Solo se define como código de la empresa contratante |
| 11 | Criterio de extracción y estados excluidos | ❌ Abierto | Sigue siendo la pregunta más importante para el negocio |
| 12 | ¿Corte único o refresco periódico? | ❌ Abierto | — |

**Quedan solo 4 preguntas para escalar al negocio (5, 10, 11, 12), más los matices de moneda/IVA y planificado/real.** Escalar solo esas, no las doce.

### 1.3 Reconciliación del diccionario contra el dataset

**Fuentes de diccionario disponibles, en orden de prioridad:**

1. **Diccionario interno** — hoja `Distancia_0` del archivo de inconsistencias, fila 2 (definiciones) y filas 21–31 (catálogo CLASE FWO). Es la fuente primaria porque viaja **con el propio dato entregado**. Transcrito en el Anexo D. *Ojo: está pegado dentro de una hoja de datos, no en una hoja aparte, por lo que cualquier lectura con `pd.read_excel` sin `nrows` lo ingiere como si fueran registros. Es la causa del error corregido en R-02.*  
2. Google Sheets externo — sin acceso; útil solo para contrastar y para las columnas que el interno no cubre.  
3. Negocio — reservado para las 4 preguntas que ninguna de las dos fuentes responde (§1.2).

Ejercicio obligatorio, columna por columna:

- Columnas en el diccionario que **no** están en el dataset → posible pérdida en la extracción.  
    
- Columnas en el dataset que **no** están en el diccionario → campos sin gobierno.  
    
- Nombres que difieren entre artefactos. **Confirmado con evidencia dura:** el mismo campo aparece con tres rótulos distintos según el artefacto.


| Posición | Dataset | Hoja `Distancia_0` | Otras 4 hojas | Definición oficial (fila 2\) |
| :---- | :---- | :---- | :---- | :---- |
| P | `Tarifa venta al cliente` | `Tarifa Venta Cliente` | **`Costo Producción`** ⚠️ | "Tarifa de venta del viaje realizado" |
| Q | `Flete producción FO` | `Flete Producción FO` | `Importe FO` | "Costo de producción o flete que se paga al Transportista" |


  Validación por valor (no solo por conteo), cruzando `Costo_0` contra el dataset por `FO`: `Costo Producción` \= `Tarifa venta al cliente` en **872 de 874** filas; `Importe FO` \= `Flete producción FO` en **868 de 874**. Conclusión: **la columna rotulada `Costo Producción` contiene en realidad la tarifa de venta.** Es un rótulo semánticamente invertido que puede producir un análisis de rentabilidad exactamente al revés. Las \~2 y \~6 filas que no cuadran deben revisarse una a una.


- Tipos de dato declarados vs tipos observados.  
    
- Dominios de valores declarados vs valores observados (§5.4).

**Entregable de la fase:** tabla de reconciliación \+ lista de preguntas abiertas con responsable y fecha de respuesta.

---

## 2\. Fase 1 — Montaje reproducible

**Objetivo:** que el notebook corra igual hoy, en el equipo de otro, y en seis meses.

### 2.1 Estructura de repo sugerida

> **Nota (2026-09-10):** esta carpeta se renombró de `analitica/02-eda/` a `analitica/02-eda-viajes-btura/` al arrancar el segundo EDA (sobre el set consolidado), para distinguir claramente los dos análisis paralelos. Las rutas de abajo quedan como referencia histórica de cuando se escribió este plan; la ruta real actual es `analitica/02-eda-viajes-btura/`.

**No es un sub-repo aparte.** Vive dentro de `analitica/02-eda/`, que ya existe en el repo con `datos/` ya poblada (los 7 archivos crudos) y `notebooks/` + `reportes/` ya prometidas en su `README.md`. La estructura de abajo es una combinación: conserva esos nombres (español, ya fijados) y les suma las piezas del plan original que el esqueleto todavía no cubría (`src/`, `datos/interim/`, `datos/processed/`).

```
analitica/02-eda/
├── README.md                  # YA EXISTE — se completa al arrancar: cómo correr, a quién preguntar
├── PLAN_EDA_Viajes_Btura.md   # este plan
├── requirements.txt           # versiones fijas (pandas, jupyter, etc.) — mismo patrón que cubicador/
├── datos/                     # YA EXISTE, YA POBLADA — equivale al "data/raw/" del plan. INMUTABLE.
│   ├── Viajes-Btura-2025-2026.xlsx
│   ├── viajes-btura-2025-2026.csv
│   ├── inconsistencias_viajes (1).xlsx
│   ├── inconsistencias_viajes-*.csv (5 hojas)
│   ├── interim/                # NUEVO — parquet tipado, generado por código
│   └── processed/              # NUEVO — tablas de salida del EDA
├── notebooks/                  # YA PROMETIDA por el README
│   ├── 00_setup_y_carga.ipynb
│   ├── 01_perfilado_estructural.ipynb
│   ├── 02_calidad_del_dato.ipynb
│   ├── 03_univariado.ipynb
│   ├── 04_bivariado_multivariado.ipynb
│   ├── 05_temporal_y_geografico.ipynb
│   ├── 06_outliers_y_sesgos.ipynb
│   └── 07_sintesis_y_limitaciones.ipynb
├── src/                         # NUEVO — código reutilizable entre notebooks
│   ├── carga.py                 # lectura + tipado + normalización de nombres
│   ├── reglas_calidad.py        # catálogo de reglas (§5.7 / Anexo C)
│   └── graficos.py              # estilo unificado
└── reportes/                    # YA PROMETIDA por el README (llamada "reports/" en el borrador original)
    ├── figures/
    ├── hallazgos.md
    ├── limitaciones.md
    └── diccionario_reconciliado.md   # sin subcarpeta docs/ aparte — un solo .md,
                                       # igual que el resto de analitica/ (un diccionario
                                       # por etapa en la raíz, no anidado)
```

No hace falta un `.gitignore` propio de `02-eda/`: el crudo se commitea igual que en `00-fuentes/` (ya está así en `datos/`); si `interim/`/`processed/` llegan a pesar mucho, se excluyen puntualmente en el `.gitignore` raíz del repo, que ya cubre `__pycache__/` y `.venv/` (agregarle `.ipynb_checkpoints/` cuando arranquen los notebooks).

> Alternativa válida: un solo notebook maestro numerado por secciones. Lo no negociable es que sea **uno solo el punto de entrada** y que el orden de ejecución sea lineal.

### 2.2 Reglas de reproducibilidad

- Versiones fijas de librerías (`pandas==x.y.z`, etc.).  
- Rutas relativas al repo, nunca `C:\Users\...`.  
- Semilla fija donde haya muestreo (`random_state=42`).  
- Sin celdas que dependan de haber ejecutado otra cosa antes fuera del notebook.  
- Salida limpia de outputs pesados antes de commitear (`nbstripout` o `jupytext` a `.py` para diff legible en Git).  
- Hash del archivo crudo registrado en la primera celda (`hashlib.md5`) para detectar si alguien cambió el insumo.  
- Fecha y hora de ejecución impresas en la primera celda.

### 2.3 Versionado

- Rama por fase o rama única — se trabaja en la rama ya existente para este EDA, en vez de crear `feature/eda-viajes-btura`.  
- Commits atómicos con mensaje descriptivo (`feat(eda): perfilado de columnas monetarias`).  
- PR con el informe de hallazgos en la descripción.  
- `datos/` se commitea igual que en `00-fuentes/` (no se excluye por defecto); solo `datos/interim/` o `datos/processed/` entran a `.gitignore` si llegan a pesar demasiado.

### 2.4 Carga defensiva

- **Primera celda obligatoria: inspección del rango real del Excel antes de leerlo como tabla.** Con `openpyxl`, recorrer `ws.dimensions`, `ws.max_row`, `ws.max_column` y listar las celdas no vacías por hoja. Los Excel de negocio traen metadatos, notas y catálogos pegados debajo o al lado del dato. *(En este caso concreto, saltarse este paso hizo que el diccionario y el catálogo CLASE FWO se leyeran como si fueran 13 registros de viaje — ver §5.6.)*  
- Regla derivada: **si `max_row` no coincide con el número de filas de dato esperado, no seguir hasta explicar la diferencia.**  
- Leer con `dtype=str` en una primera pasada para ver el crudo **tal como viene**, antes de que pandas infiera tipos y silencie problemas.  
- Segunda pasada con tipado explícito y conversión controlada (`errors='coerce'`), contando cuántos valores fallaron cada conversión.  
- Persistir a Parquet en `datos/interim/` con los tipos ya fijados; el resto del EDA lee de allí (más rápido y determinista que releer Excel).  
- Normalizar nombres de columna a `camelCase` en el código, manteniendo el mapeo original documentado. *(Decisión de equipo, 2026-09-08: se prefirió camelCase sobre snake_case — no afecta el contenido del dato, solo la convención de nombres en código. Ver `notebooks/00_setup_y_carga.ipynb`, Paso 5.)*

---

## 3\. Fase 2 — Inventario y perfilado estructural

**Objetivo:** saber qué hay, sin juzgar todavía.

### 3.1 Checklist

- [ ] Dimensiones (filas × columnas) y peso en memoria.  
- [ ] Listado de columnas con: tipo declarado, tipo inferido, nº de nulos, % nulos, nº de únicos, cardinalidad relativa, ejemplo de valor.  
- [ ] `head()`, `tail()` **y** `sample(20, random_state=42)` — las primeras filas suelen no ser representativas.  
- [ ] Clasificación de cada columna en: identificador / fecha / categórica nominal / categórica jerárquica / numérica continua / numérica monetaria / texto libre.  
- [ ] Detección de columnas constantes o cuasi-constantes (sin poder informativo).  
- [ ] Detección de columnas redundantes entre sí (§6.4).  
- [ ] Revisión de las hojas adicionales del Excel y de filas de encabezado/totales pegadas al final.  
- [ ] Perfilado automático como red de seguridad (`ydata-profiling` o `sweetviz`) — **como complemento, nunca como sustituto** del análisis dirigido.

### 3.2 Fotografía estructural ya levantada (baseline)

| Columna | Tipo | % nulos | Únicos | Nota |
| :---- | :---- | :---- | :---- | :---- |
| `ID Clase Documento FWO` | texto | 0 % | 7 | OPIM (67 %), OHUB, OPEX, OH3P, OPTN, OPDS, OPTI |
| `FO` | entero | 0 % | 41.107 | 130 valores repetidos → **no es PK** |
| `Fecha Creación FO` | fecha | 0 % | 371 | 2025-04-01 → 2026-04-07 |
| `BP Cliente` | entero | 0 % | 537 | código, sin nombre |
| `Origen` | texto | 0 % | 113 | Buenaventura \= 74,7 % |
| `Destino` | texto | 0 % | 122 | Buenaventura \= 47,0 % |
| `Distancia Total` | float | 0 % | 287 | 0 → 1.496; mediana 297 |
| `Peso Neto` | float | 0 % | 8.117 | 0 → 39.320; mediana 7.150 |
| `Posición Principal FWOP` | texto | 0 % | 3 | TUR / PRD / PKG |
| `Grupo de Equipo FWOP` | texto | 18,18 % | 2 | CN \= contenedor / CAM \= camión. Nulos **estructurales**: solo aplica a posición TUR |
| `Vehículo` | texto | 0 % | 3.418 | placa o código |
| `ID Medio Transporte FO` | texto | 0 % | 15 | incluye un valor basura `1` |
| `Clase Equipo FOP` | texto | 18,19 % | 27 | CNT40.HC domina. Nulos estructurales salvo **3 casos TUR** que sí son defecto |
| `Propiedad` | texto | 0 % | 2 | Tercero 91,2 % / Propio 8,8 % |
| `Tipo` | texto | 0 % | 18 | el diccionario dice "**puede omitirse**" → descartable |
| `Tarifa venta al cliente` | float | 0 % | 4.291 | 0 → 86.700.000 |
| `Flete producción FO` | entero | 0 % | 4.027 | −49.978 → 46.800.000 |

---

## 4\. Fase 3 — Unicidad y granularidad

**Objetivo:** entender qué representa una fila. Es el paso que más análisis arruina si se salta.

### 4.1 Pruebas

- [x] Duplicados de fila completa. → **0 encontrados.**  
- [x] Duplicados de la supuesta clave `FO`. → **128 valores de `FO` repetidos** (130 filas de más sobre 41.107 únicos — ver precisión del conteo en §4.2).  
- [x] Para cada `FO` repetido: ¿qué columnas difieren? → Ver §4.2: no son posiciones de una misma orden ni un error simple — el patrón (100% de los 128 grupos) es carga consolidada de varios clientes en un mismo viaje de vehículo.  
- [x] Buscar clave compuesta que sí sea única: `FO` \+ `Posición Principal FWOP`, `FO` \+ `Vehículo`, etc. → **Encontrada y verificada (notebook `02`, Paso 4):** `FO` \+ `BP Cliente` \+ `Peso Neto` \+ `Destino` identifica las 41.237 filas sin colisión (41.237 de 41.237). `FO` solo: 41.107/41.237. Agregar `Vehículo` no aporta nada (nunca varía dentro de un grupo repetido de `FO`, coherente con §4.2).  
- [ ] Duplicados lógicos: mismo cliente, misma fecha, mismo origen-destino, mismo peso, distinto `FO`.  
- [ ] Verificar si un mismo `Vehículo` aparece en dos viajes solapados en el tiempo (imposibilidad física).

### 4.2 Hallazgo sobre los `FO` repetidos

> **CORRECCIÓN (2026-09-08, notebook `02_calidad_del_dato.ipynb`, Paso 3).** La versión anterior de esta sección afirmaba, con base en solo 2 ejemplos, que los pares repetidos "comparten fecha, distancia, peso, tipo y valores, y difieren únicamente en Origen". La verificación sistemática sobre los **128 grupos completos** (no una muestra) descarta esa afirmación — el detalle queda abajo.

**Precisión sobre el conteo:** hay **128 valores de `FO` distintos** con repetición, no 130. El "130" del plan y del Anexo A sigue siendo correcto, pero mide otra cosa: son las filas "de más" sobre el total de únicos (126 pares × 1 fila de más + 2 tríos × 2 filas de más = 130). Ambos números son correctos, no son contradictorios.

**Qué cambia realmente dentro de cada grupo repetido**, verificado sobre los 128 grupos completos:

| Columna | Grupos con variación (de 128) | % |
| :---- | :---- | :---- |
| `Peso Neto` | 105 | 82 % |
| `Flete Producción FO` | 102 | 80 % |
| `Tarifa venta al cliente` | 99 | 77 % |
| `BP Cliente` | 96 | 75 % |
| `Clase Equipo FOP` | 48 | 38 % |
| `Destino` | 34 | 27 % |
| `Grupo de Equipo FWOP` | 33 | 26 % |
| `Posición Principal FWOP` | 32 | 25 % |
| `Origen` | 25 | 20 % |
| `Distancia Total` | 17 | 13 % |
| `Fecha Creación FO`, `ID Clase Documento FWO`, `ID Medio Transporte FO`, `Vehículo`, `Tipo`, `Propiedad` | 0 | 0 % |

`Origen` —la columna que la versión anterior señalaba como la única que cambia— es en realidad de las que **menos** cambia. `Vehículo` y `Fecha Creación FO` nunca cambian dentro de un grupo repetido: eso sí es 100 % consistente.

**Patrón universal confirmado — 128 de 128 grupos (100 %):** comparten el mismo `Vehículo` y la misma `Fecha Creación FO`, y difieren en `BP Cliente` y/o `Peso Neto` (y casi siempre en las columnas de plata).

**Hipótesis más consistente con la evidencia:** el diccionario interno (Anexo D) define `FO` como *"documento de subcontratación con el dueño del vehículo... equivale al número del viaje"* — el número identifica el **viaje del vehículo**, no la orden de un cliente individual. La lectura más plausible es que **un mismo viaje puede consolidar carga de varios clientes distintos**, cada uno con su propio peso y su propia tarifa, bajo un único número de `FO`. Esto reemplaza la dicotomía original ("multi-recogida legítima" vs. "error de duplicación") por una tercera lectura mejor respaldada por el dato.

**Sin resolver:** al menos un caso (`FO` 301205267) tiene cliente distinto con **tarifa idéntica** entre ambas filas — no se puede descartar que sea una corrección de cliente mal aplicada en vez de consolidación genuina. No hay forma de distinguirlo solo con el dato disponible.

**Pregunta a escalar al negocio (reemplaza la pregunta genérica anterior):** ¿un mismo `FO` puede representar el viaje de un vehículo que transporta carga de varios clientes distintos (carga consolidada)? ¿O cada `FO` debería corresponder a un único cliente, y estos 128 casos son un defecto de la extracción o del sistema origen?

**Impacto en el tratamiento de las cifras (invierte la recomendación anterior):** si la hipótesis de carga consolidada es correcta, **sumar `Tarifa venta al cliente`/`Flete producción FO` por fila** (no deduplicar por `FO`) sería lo correcto para métricas de ingreso — al contrario de lo que advertía la versión anterior de esta sección. **No usar estas cifras en reportes hasta confirmar con el negocio.**

**Confirmación externa (2026-09-08, consulta directa a `FactFWOFUFO` en DWCubos).** El esquema de esa tabla confirma la hipótesis por diseño: `Distancia Total FO` está definida a nivel de `FO` (el viaje), mientras que `ID Ubicación Origen/Destino FWOP` está a nivel de `FWOP` (la posición de cada cliente dentro del viaje) — son dos grados distintos de la jerarquía, no se espera que varíen juntos. Verificado a escala de **toda la compañía**: de 90.285 viajes con más de un `FWO` (consolidados), la distancia varía en solo **660 (0,73%)** —la verdadera anomalía, pendiente de investigar si se retoma— mientras que el origen varía en **4.833 (5,35%)**, la minoría esperable. Esto corrobora, con el sistema fuente, que el patrón encontrado en el extracto de Buenaventura no es un defecto de la extracción sino el comportamiento normal del modelo de datos de origen.

---

## 5\. Fase 4 — Calidad del dato

**Objetivo:** cuantificar el estado del dato por dimensión, no dar una opinión general.

Se evalúan **siete dimensiones**. Cada una produce un número, no un adjetivo.

### 5.1 Completitud

- % de nulos por columna, ordenado.  
    
- Mapa de calor de nulos (`missingno.matrix`) para ver si los faltantes van juntos.  
    
- ¿Los nulos son aleatorios o sistemáticos? Cruzar el flag de nulo contra fecha, cliente, tipo de documento, origen.  
    
  > **RESUELTO — el 18,2 % de nulos NO es un defecto de calidad.** El diccionario establece que `Grupo de Equipo FWOP` "aplica solo para la posición principal TUR" y que `Clase Equipo FOP` "aplica solo para grupo de equipo CN y CAM". Verificado contra el dato:  
  > 

| Posición Principal | Grupo Equipo nulo | Clase Equipo nula |
| :---- | :---- | :---- |
| PKG (1.299) | 1.299 (100 %) | 1.299 (100 %) |
| PRD (6.197) | 6.197 (100 %) | 6.197 (100 %) |
| TUR (33.741) | 0 | **3** |

  > 

  > Los 7.496 nulos son exactamente PKG \+ PRD, es decir **nulos estructurales esperados por el modelo de datos**. Se deben modelar como "no aplica", no como faltante.

  > 

  > **El defecto real son 3 registros TUR con `Clase Equipo FOP` nula** (0,007 %), que sí violan la regla. Ésos son los que hay que listar y escalar.

  > 

  > Implicación para el plan: la severidad de este hallazgo baja de **Alta** a **Baja**, y el "18,2 % de la operación sin clasificar" desaparece como limitación. Lo que queda es que **cualquier análisis por tipo de equipo aplica solo al universo TUR (81,8 %)**, por diseño, no por mala captura.


- Nulos disfrazados: cadenas vacías, `"NA"`, `"N/A"`, `"-"`, `"SIN DATO"`, `0` usado como faltante, `1900-01-01`.  
    
- Completitud por periodo: ¿empeora o mejora la captura a lo largo del tiempo?

### 5.2 Unicidad

Cubierto en §4.

### 5.3 Validez (rangos y formatos)

- Numéricas: valores negativos donde no deberían existir, ceros sospechosos, valores fuera de rango físico.  
- Fechas: fuera de la ventana declarada, futuras, festivos/domingos con volumen anómalo.  
- Texto: espacios al inicio/fin, mayúsculas/minúsculas mezcladas, tildes inconsistentes, caracteres raros.  
- Identificadores: longitud y patrón consistentes (placas de vehículo, códigos BP).

### 5.4 Consistencia (dominios y reglas cruzadas)

- Cardinalidad de cada categórica y valores de baja frecuencia (candidatos a error de digitación).  
- Normalización de nombres de ciudad: detectar `BOGOTA, DISTRITO CAPITAL` vs `BOGOTA` vs `BOGOTÁ`; `GUADALAJARA DE BUGA` vs `BUGA`. Usar distancia de edición (`rapidfuzz`) para agrupar candidatos.  
  > **RESUELTO (2026-09-09, notebook `02_calidad_del_dato.ipynb`, Paso 13).** Comparación sistemática con `rapidfuzz` (`token_set_ratio`, umbral 80) sobre los 113+122 nombres de origen/destino. Los ejemplos citados arriba (variantes de Bogotá, Buga) **no existen en este dataset** — eran ilustrativos, no verificados contra el dato real. Los únicos 4 candidatos que salieron (`GIRARDOT`/`GIRARDOTA`, `SOACHA`/`SOCHA`, `SANTA ROSA DE CABAL`/`SANTA ROSA DE OSOS`, `TURBACO`/`TURBO`) son **falsos positivos**: municipios colombianos reales y distintos que solo suenan parecido. **Conclusión: no hay evidencia de nombres de ciudad duplicados/inconsistentes en este set.**  
- Coherencia entre columnas que describen lo mismo. **Ya detectado:** `Tipo` y `ID Medio Transporte FO` son casi redundantes pero **no coinciden siempre** — p. ej. 175 registros con `Tipo = TC.3S3` traen `ID Medio Transporte = FP-3S3`, y 9 registros `TC.2S2` traen `TC-3S3`. **Resuelto por el diccionario:** `Tipo` está definido textualmente como **"Puede omitirse"**, mientras `ID Medio Transporte FO` tiene definición sustantiva ("categoría o tipo de vehículo... TC-3S2 \= tractocamión de tres ejes con trailer de dos"). → **`ID Medio Transporte FO` es la fuente de verdad; `Tipo` se descarta.** Las discrepancias dejan de ser un problema a resolver y pasan a ser evidencia de que `Tipo` no es confiable.  
  > **CORRECCIÓN DE MAGNITUD (2026-09-09, notebook `02_calidad_del_dato.ipynb`, Paso 12).** Los 175 y 9 casos citados eran solo 2 ejemplos, no el conteo completo. Verificado sobre las 32 combinaciones distintas de discrepancia: **5.420 registros (13,1 % del dataset)** tienen `Tipo` ≠ `ID Medio Transporte FO` (ya normalizando el separador `.` vs `-`). Muchos pares (ej. `C2.4.5T`→`TC_TURBO`, 1.203 casos; `C2.8T`→`TC_SENCI`, 860 casos) no son errores de captura sino **dos catálogos de nomenclatura paralelos** que nunca estuvieron pensados para coincidir letra por letra. Esto **refuerza** la decisión ya tomada (descartar `Tipo`), no la cambia — la magnitud real (13,1 %) hace aún más clara la falta de confiabilidad de `Tipo`.  
- Valor basura confirmado: `ID Medio Transporte FO = "1"` en 2 registros.  
- Coherencia jerárquica: ¿`Clase Equipo FOP` es siempre consistente con `Grupo de Equipo FWOP`?

### 5.5 Exactitud (plausibilidad)

Reglas de negocio verificables sobre el propio dato:

| Regla | Resultado observado |
| :---- | :---- |
| `Distancia Total > 0` | **17 registros en 0** (0,04 %) — de estos, **14 tienen además `Origen ≠ Destino`** con ciudades reales y peso sustancial (ver refinamiento abajo) |
| `Peso Neto > 0` | **82 registros en 0** (0,20 %) — más **2 registros adicionales en exactamente 1 kg** (notebook `03`, Paso 2), prácticamente equivalente a cero. Total funcional: 84 |
| `Tarifa venta al cliente > 0` | **874 registros en 0** (2,12 %) — más **3 registros adicionales en exactamente $1** (notebook `03`, Paso 2), un valor de relleno equivalente a cero en la práctica. Total funcional: 877 |
| `Flete producción FO > 0` | **183 en 0** (0,44 %) y **1 negativo** (−49.978) |
| `Tarifa ≥ Flete` (margen no negativo) | **3.839 registros con margen negativo** (9,31 %) |
| `Origen ≠ Destino` | **8.966 registros con Origen \= Destino** (21,74 %) |
| Peso ≤ capacidad legal del tipo de vehículo | por evaluar contra tabla de capacidades |
| Distancia coherente con el par Origen–Destino | por evaluar: misma ruta debería tener distancia estable |

> **RESUELTO — el 21,7 % de `Origen = Destino` es mayoritariamente legítimo.** Con el catálogo CLASE FWO (Anexo D) el cruce se puede hacer ya:  
> 

| Clase | Descripción | O=D | % de esa clase | % del total O=D |
| :---- | :---- | :---- | :---- | :---- |
| OHUB | Pedido Descontenerización | 6.082 | **99,8 %** | 67,8 % |
| OH3P | Pedido Descontenerización Terceros | 1.871 | **100 %** | 20,9 % |
| OPIM | Pedido de Puerto a interior | 832 | 3,0 % | 9,3 % |
| OPTN | Pedido Nacional | 146 | 9,8 % | 1,6 % |
| OPEX | Pedido interior a Puerto | 34 | 0,9 % | 0,4 % |
| OPDS | Pedido Distribución Nacional | 1 | 1,5 % | 0,0 % |

>   
> Las dos clases de **descontenerización** (OHUB \+ OH3P) explican el **88,7 %** de los casos, y en ambas el fenómeno es prácticamente universal dentro de la clase — coherente con una operación que empieza y termina en el mismo sitio. **No son errores: son un tipo de operación que debe segmentarse, no depurarse.**  
>   
> **El defecto acotado son los 978 registros OPIM \+ OPTN con `Origen = Destino`** (2,4 % del set), que sí contradicen la definición de su clase (`OPIM` \= de puerto a interior, `OPTN` \= pedido nacional). Ésos son los que van a revisión.  
>   
> Nota: la intuición de que "OPEX explica el fenómeno" es errónea — OPEX es *interior a puerto*, precisamente un movimiento con origen distinto de destino, y aporta solo 34 casos.

> **REFINAMIENTO (2026-09-09, notebook `02_calidad_del_dato.ipynb`, Paso 10).** Los 978 defectos `OPIM`+`OPTN` no son homogéneos: **974 de 978 (99,6 %) tienen `Distancia Total = 10`**, la misma "firma" numérica de las operaciones de descontenerización (`OHUB`/`OH3P`). Esto sugiere que la mayoría de estos 978 **no son un error de origen/destino, sino un código de clase de documento (`ID Clase Documento FWO`) mal asignado** — probablemente son descontenerización real etiquetada como `OPIM`/`OPTN` en vez de `OHUB`/`OH3P`. Solo **4 registros** (distancias 521, 521, 233,5 y 505 km) no siguen ese patrón y sí son genuinamente atípicos — esos ameritan revisión individual, no los 978 como bolsa homogénea.
>
> **Hipótesis alternativa probada y descartada:** ¿podrían ser viajes intraurbanos legítimos (ej. recogida y entrega dentro de la misma ciudad, en puntos distintos), dado que `Origen`/`Destino` se registran a nivel de población, no de dirección exacta? Verificado: **el 100 % de los 978 registros tienen `Origen = BUENAVENTURA`** — ninguna otra ciudad. Si fuera un fenómeno de granularidad intraurbana general, se esperaría variedad de ciudades. La concentración total en Buenaventura (la misma ciudad de la descontenerización legítima) refuerza la hipótesis del código mal asignado. Además, `OPIM` se define como *"Pedido de Puerto a interior"* — un registro que nunca sale del puerto contradice su propia definición por diseño.
>
> **Además, cruzando contra R01:** `Distancia Total = 0` y `Origen = Destino` casi no se superponen (solo 3 filas en común de 17 y 8.966 respectivamente). Los otros **14 registros con `Distancia Total = 0`** tienen `Origen ≠ Destino` con ciudades reales y distantes (Yumbo, Funza, Mosquera, Soledad, Bucaramanga, Chinchiná, Tuluá) y pesos sustanciales (4.000-34.000 kg) — son envíos de largo recorrido genuinos a los que simplemente **falta la distancia real** (debería ser de cientos de km, no 0). Es un defecto limpio, distinto del fenómeno de descontenerización.

### 5.6 Integridad referencial

- ¿Todo `BP Cliente` existe en el maestro de clientes? (requiere el maestro).  
    
- ¿Toda `Vehículo` existe en el maestro de flota?  
    
- Validar que todos los códigos de `ID Clase Documento FWO` existan en el catálogo CLASE FWO. **Verificado: los 7 códigos presentes están todos en el catálogo, sin huérfanos.** Lo relevante es lo contrario — ver §12.1, códigos del catálogo ausentes en el set.  
    
- Contraste con el archivo de inconsistencias. **Verificado: los 5 subconjuntos de inconsistencias son consistentes con el dataset**, todos sus `FO` existen en él y los conteos calzan (Anexo B). No hay evidencia de extracciones con alcances distintos.  
    
  > **Corrección respecto a la versión anterior de este plan.** Se había reportado que 13 de los 30 registros de `Distancia_0` no existían en el dataset. **Era falso**, y el error es instructivo: esa hoja tiene 1 fila de definiciones \+ 17 filas de dato \+ 1 en blanco \+ 11 filas del catálogo CLASE FWO \= las 30 "filas" que devuelve una lectura ingenua. Los 17 registros reales coinciden al 100 %.  
  >   
  > **Lección metodológica que debe quedar en el notebook:** nunca leer un Excel de negocio con `pd.read_excel()` sin antes inspeccionar el rango real con `openpyxl` (`ws.max_row`, `ws.dimensions`) y recorrer las celdas no vacías. Los archivos de negocio suelen traer metadatos, notas y catálogos pegados debajo o al lado de los datos. **Añadir esta inspección como primera celda obligatoria de la fase §2.4.**

### 5.7 Oportunidad y cobertura temporal

- Registros por día / semana / mes; buscar huecos.  
- **Ya detectado:** cobertura mensual estable entre 2.959 y 3.821 registros/mes, salvo **abril 2026 con solo 523 registros** → mes parcial (corte al día 7). No debe compararse contra meses completos ni incluirse en tendencias sin marcarlo.  
- Verificar si hay días sin ningún registro dentro de la ventana.

### 5.8 Salida de la fase

Una **tabla resumen de calidad** con: dimensión, regla, registros afectados, % , severidad (bloqueante / alta / media / baja), decisión (excluir / imputar / marcar / consultar), responsable.

---

## 6\. Fase 5 — Análisis univariado

**Objetivo:** conocer la forma de cada variable por separado.

### 6.1 Numéricas (`Distancia Total`, `Peso Neto`, `Tarifa venta al cliente`, `Flete producción FO`)

- Estadísticos: n, media, mediana, desviación, mín, máx, P1, P5, P25, P75, P95, P99.  
- Asimetría (skewness) y curtosis.  
- Histograma \+ KDE, y boxplot. Para las monetarias, además histograma en **escala logarítmica** (la tarifa va de 0 a 86,7 M — en escala lineal no se ve nada).  
- ECDF para leer percentiles directamente.  
- Conteo explícito de ceros y negativos **separado** del resto de la distribución (mezclarlos distorsiona todos los estadísticos).  
- Test de normalidad solo si se va a usar para algo; en general basta describir la forma.

### 6.2 Categóricas

- Tabla de frecuencias absoluta y relativa, incluyendo nulos.  
- Gráfico de barras ordenado, con las categorías de cola agrupadas en "Otros".  
- Índice de concentración (¿qué % del volumen está en el top 5?). **Ya visible:** `Propiedad` \= 91,2 % Tercero; `ID Clase Documento FWO` \= 67,4 % OPIM; `Clase Equipo FOP` \= 47,1 % CNT40.HC. Son distribuciones muy concentradas → cualquier conclusión sobre las categorías minoritarias tendrá poca base muestral.

### 6.3 Fecha

- Distribución por año, mes, semana, día del mes, día de la semana.  
- Detección de estacionalidad y de días atípicos.

### 6.4 Redundancia entre columnas

Matriz de asociación entre las cinco columnas que describen el vehículo/equipo (`Tipo`, `ID Medio Transporte FO`, `Clase Equipo FOP`, `Grupo de Equipo FWOP`, `Vehículo`), usando V de Cramér o información mutua. Objetivo: determinar cuántas dimensiones reales hay detrás de cinco columnas y cuál conservar.

---

## 7\. Fase 6 — Análisis bivariado y multivariado

**Objetivo:** encontrar relaciones, no solo describir columnas.

### 7.1 Numérica ↔ numérica

- Matriz de correlación de **Pearson y de Spearman** (Spearman es la que importa aquí: las distribuciones son sesgadas y hay outliers extremos).  
- Scatter plots clave:  
  - `Distancia Total` vs `Tarifa venta al cliente` → ¿la tarifa sigue la distancia? ¿hay bandas discretas (tarifario)?  
  - `Peso Neto` vs `Tarifa venta al cliente` → ¿se cobra por peso o por viaje?  
  - `Flete producción FO` vs `Tarifa venta al cliente` → visualizar la línea de margen cero y los 3.839 puntos por debajo.  
  - `Distancia Total` vs `Peso Neto`.  
- Con 41 mil puntos, usar `hexbin` o `datashader` en vez de scatter plano.

### 7.2 Categórica ↔ numérica

- Boxplots / violines de tarifa, flete, peso y distancia segmentados por `Propiedad`, `Tipo`, `ID Clase Documento FWO`, `Posición Principal FWOP`.  
- Tabla de medianas y rangos intercuartílicos por categoría (mediana, no media: las colas son muy largas).  
- Evaluar significancia solo si se va a sustentar una decisión (Kruskal-Wallis / Mann-Whitney, no ANOVA, dada la no normalidad).

### 7.3 Categórica ↔ categórica

- Tablas de contingencia y mapas de calor de proporciones:  
  - ~~`ID Clase Documento FWO` × `Origen = Destino`~~ → **ya ejecutado, ver §5.5.** Queda solo caracterizar los 978 casos residuales OPIM \+ OPTN.  
  - ~~`Posición Principal FWOP` × nulos de equipo~~ → **ya ejecutado, ver §5.1.** Queda solo revisar los 3 casos TUR.  
  - `ID Clase Documento FWO` × `Propiedad` → ¿qué líneas de negocio se subcontratan más?  
  - `ID Clase Documento FWO` × mes → ¿cambia la mezcla de negocio en el tiempo?  
  - `Propiedad` × `ID Medio Transporte FO` → ¿la flota propia se concentra en ciertos vehículos?  
  - `Posición Principal FWOP` × `Clase Equipo FOP` (restringido al universo TUR).

### 7.4 Variable derivada de margen

Construir `margen = Tarifa venta al cliente − Flete producción FO` y `margen_% = margen / Tarifa`, con manejo explícito de la división por cero (874 tarifas en 0). Analizar su distribución por cliente, ruta, tipo de vehículo y mes.

> **Advertencia a dejar escrita:** todo el análisis de margen depende de que se confirme la semántica de las dos columnas monetarias (§1.2, punto 4\) y de que se resuelva la ambigüedad de nombres con el archivo de inconsistencias (§1.3). Hasta entonces, es exploratorio y no debe reportarse al negocio como cifra de rentabilidad.

### 7.5 Multivariado

- Segmentación descriptiva: agrupar rutas por volumen, distancia y margen y ver si emergen clústeres naturales.  
- Si se usa clustering (K-Means, DBSCAN) o PCA, es **solo como herramienta exploratoria**, con escalado previo y documentando que no es un modelo entregable.

---

## 8\. Fase 7 — Análisis temporal

- Serie de conteo de viajes por día y por semana, con media móvil.  
- Serie de tarifa mediana y flete mediano por mes → ¿hay ajustes tarifarios visibles?  
- Descomposición estacional (`statsmodels.seasonal_decompose`) si la serie diaria lo permite.  
- Patrón intrasemanal: viajes por día de la semana; ¿hay actividad en domingos y festivos colombianos?  
- Detección de quiebres estructurales (cambio de nivel abrupto en volumen o en tarifa) — suelen indicar cambio de sistema fuente, no cambio de negocio.  
- Comparación año contra año limitada: el set cubre 13 meses, así que solo abril 2025 vs abril 2026 (y este último parcial). **Documentar que no hay dos años completos.**

---

## 9\. Fase 8 — Análisis geográfico y de red

- Top orígenes y destinos, con % acumulado.  
- Matriz origen–destino de los pares más frecuentes (heatmap del top 20×20).  
- Flujo direccional: entradas a Buenaventura vs salidas desde Buenaventura, por mes → mide el desbalance importación/exportación.  
- **Desbalance ya visible:** Buenaventura aparece como origen en 30.820 registros (74,7 %) y como destino en 19.383 (47,0 %). Explicar la diferencia es un hallazgo en sí mismo.  
- Consistencia de distancia por par O–D: para cada par, calcular desviación estándar de `Distancia Total`. Un par con distancias muy dispersas indica error de captura o ruta variable.  
- Los 8.966 registros con Origen \= Destino se analizan como **segmento aparte**, no se mezclan con los viajes punto a punto.  
- Normalización de nombres de municipio contra el listado DANE, si está disponible.  
- Grafo de red (nodos \= ciudades, aristas \= flujos) solo si aporta más que la matriz.

---

## 10\. Fase 9 — Outliers y anomalías

**Objetivo:** distinguir error de dato, caso legítimo extremo, y evento de negocio.

- Métodos: IQR (1,5× y 3×), z-score robusto (MAD), percentiles extremos (P99,5), e `IsolationForest` para outliers multivariados.  
- Aplicar **por segmento**, no global: un peso de 39 t es normal en tractocamión y absurdo en camioneta. Segmentar por `Tipo` antes de marcar.  
- Casos a revisar uno por uno (revisión manual, no automática):  
  - Tarifa de 86.700.000 — ¿viaje especial, carga extradimensionada, o error de digitación?  
  - `Flete producción FO` negativo (−49.978) — ¿nota crédito o error de signo?  
  - Distancias en 0 con peso y tarifa positivos.  
  - Registros con margen negativo mayor a cierto umbral.  
- Para cada outlier: clasificarlo en `error_dato` / `caso_legitimo` / `por_confirmar`, y dejar el `FO` listado en un anexo.  
- **No eliminar outliers en el EDA.** Marcarlos con una columna flag y documentar el criterio.

---

## 11\. Fase 10 — Sesgos y representatividad

**Objetivo:** entender de quién y de qué habla este dato, y de quién no.

Preguntas a responder con evidencia:

1. **Sesgo de cobertura geográfica.** El set es de Buenaventura. No representa la operación nacional. ¿Qué % de la operación total de la compañía es? *(Requiere una cifra externa; si no se consigue, se declara como limitación.)*  
2. **Sesgo de selección.** ¿Qué filtró la extracción? ¿Se excluyeron viajes anulados, cancelados o en borrador? Un set que solo contiene viajes exitosos **no puede usarse para analizar fallas ni cancelaciones**.  
3. **Sesgo de supervivencia.** Si solo hay FOs facturadas, los viajes no facturados son invisibles.  
4. **Concentración de clientes.** Distribución de volumen por `BP Cliente`: calcular índice de concentración (top 10 / total, HHI). Si unos pocos clientes concentran el grueso, las medias globales describen a esos clientes, no al negocio.  
5. **Sesgo por flota.** 91,2 % tercero — cualquier conclusión sobre flota propia se apoya en 3.642 registros y en un subconjunto probablemente no aleatorio de rutas.  
6. **Sesgo del segmento sin clasificar.** Las 7.496 filas sin datos de equipo: ¿son un tipo de operación entero que quedará excluido de todo análisis por equipo?  
7. **Sesgo temporal.** Abril 2026 incompleto; ¿hubo cambios de sistema o de proceso durante la ventana que hagan no comparables los primeros meses con los últimos?  
8. **Sesgo de granularidad.** Si una fila no es un viaje (§4), toda métrica de "número de viajes" está mal contada.

---

## 12\. Fase 11 — Vacíos y limitaciones (el entregable diferenciador)

**Objetivo:** dejar por escrito, sin ambigüedad, qué preguntas este set no responde. Esta es la sección que evita que alguien tome una decisión sobre datos que no soportan la pregunta.

### 12.1 Vacíos estructurales identificados — columnas que el set NO tiene

| Falta | Qué impide analizar |
| :---- | :---- |
| Fecha/hora de salida, llegada y entrega | Tiempos de tránsito, cumplimiento de promesa, OTIF, ciclos operativos |
| Estado del viaje (ejecutado / anulado / en curso) | Tasa de cumplimiento, cancelaciones, viajes fallidos |
| Costo real de operación | Rentabilidad real (solo hay flete pagado, no costo total con peajes, combustible, etc.) |
| Nombre / segmento / sector del cliente | Análisis comercial por segmento (solo hay código `BP Cliente`) |
| Conductor / operador | Análisis de desempeño humano, seguridad vial |
| Coordenadas o georreferencia | Análisis espacial real, distancias verificables, mapas |
| Ruta ejecutada / telemetría | Desviaciones, tiempos en vía, paradas |
| Unidad de medida documentada | Certeza sobre kg vs t, km vs millas, moneda |
| Contenedores / unidades por viaje | Densidad de carga, utilización real |
| Volumen (m³) o dimensiones | Análisis de ocupación volumétrica |
| Número de contrato o tarifario aplicado | Explicar la dispersión de tarifas en la misma ruta |
| Marca temporal de última modificación del registro | Trazabilidad del dato |
| Indicador de viaje vacío / posicionamiento | Separar movimientos productivos de reposicionamiento |

### 12.1-bis Líneas de negocio ausentes en la extracción

El catálogo CLASE FWO define **10** líneas de negocio; el set entregado contiene solo **7**. Tres códigos del catálogo no aparecen en ningún registro:

| Código | Descripción | Qué NO se puede analizar |
| :---- | :---- | :---- |
| `OHUC` | Pedido Contenerización | Toda la operación de contenerización (la inversa de OHUB, que sí está y pesa 6.092 registros). Un desbalance así es improbable en un puerto: **o la extracción la filtró, o esa operación se registra en otro sistema** |
| `OPSC` | Pedido sin Cálculo de costos | Registros que por definición no llevan costeo. **Su ausencia es sospechosa justo cuando se investigan 874 tarifas en 0 y 183 fletes en 0** — vale la pena preguntar si esos ceros deberían haber sido OPSC |
| `OPXL` | Pedido Extradimensionado y rodados | Carga extradimensionada y vehículos rodados. Explicaría outliers de tarifa como el de $86,7 M, que hoy quedan sin categoría |

**Acción:** preguntar al negocio si estas tres clases (a) no tuvieron operación en el periodo, (b) fueron filtradas en la extracción, o (c) viven en otro sistema. La respuesta cambia el alcance declarado del set.

### 12.2 Limitaciones por calidad

Trasladar aquí, con su cifra, cada hallazgo bloqueante de la §5. Por ejemplo:

- No se puede calcular ingreso total confiable mientras existan 874 tarifas en 0 sin explicación.  
- No se puede contar viajes con precisión mientras `FO` no sea clave única.  
- El análisis por tipo de equipo aplica solo al universo TUR (81,8 %); PKG y PRD quedan fuera **por diseño del modelo**, no por mala captura. Debe declararse como alcance, no como defecto.  
- No se puede calcular rentabilidad mientras no se confirme moneda e IVA de las dos columnas monetarias (su semántica ya está resuelta, §1.3).

### 12.3 Limitaciones por cobertura

- Ventana de 13 meses (uno parcial) → no permite análisis de estacionalidad multianual ni comparaciones año contra año completas.  
- Alcance geográfico Buenaventura → no extrapolable a la operación nacional.

### 12.4 Formato del entregable

Tabla en `reportes/limitaciones.md`:

| ID | Pregunta que no se puede responder | Causa | Tipo (columna faltante / calidad / cobertura / granularidad) | Qué se necesitaría para responderla |
| :---- | :---- | :---- | :---- | :---- |
| L-01 | ¿Cuál es el tiempo de tránsito promedio Bogotá–Buenaventura? | No hay fechas de salida ni de llegada | Columna faltante | Campos de fecha/hora de ejecución en la extracción |
| L-02 | ¿Cuántos viajes se hicieron realmente en el periodo? | `FO` no es única y no se sabe si una fila \= un viaje | Granularidad | Definición formal del grano \+ clave única |
| L-03 | ¿Cuál es la rentabilidad por ruta? | No hay costo real; semántica de columnas monetarias sin confirmar | Columna faltante \+ calidad | Confirmación del negocio \+ campo de costo |
| … | … | … | … | … |

---

## 13\. Fase 12 — Síntesis, documentación y cierre

### 13.1 Informe de hallazgos

Cada hallazgo se documenta con una ficha estándar:

```
ID:            H-007
Título:        21,7 % de los registros tienen Origen igual a Destino
Dimensión:     Exactitud / plausibilidad
Evidencia:     8.966 de 41.237 registros (21,74 %). Celda §5.5 del notebook.
Ejemplo:       FO 3012xxxxx — BUENAVENTURA → BUENAVENTURA, 0 km
Hipótesis:     Corresponde a operaciones intra-puerto o posicionamiento de vacíos
Verificación:  Cruce contra ID Clase Documento FWO (pendiente confirmación negocio)
Severidad:     Alta
Impacto:       Distorsiona toda métrica de distancia promedio y de rutas
Recomendación: Segmentar y excluir de análisis punto a punto hasta confirmar
Responsable:   (nombre) — Fecha compromiso: (fecha)
```

### 13.2 Log de supuestos

Toda decisión tomada ante ambigüedad, con su justificación y su reversibilidad.

### 13.3 Recomendaciones al negocio / al dueño del dato

Separadas en: (a) campos a agregar en la próxima extracción, (b) reglas de validación a implementar en origen, (c) definiciones a formalizar en el diccionario.

### 13.4 Verificación final antes del PR

- [ ] `Restart & Run All` en limpio, sin errores, desde el crudo.  
- [ ] Todos los números del informe salen de una celda del notebook (spot-check de al menos 5).  
- [ ] Ninguna ruta absoluta, ninguna credencial, ningún dato sensible commiteado.  
- [ ] Gráficos con título, ejes rotulados y unidades.  
- [ ] `README` explica cómo correrlo y qué versión del insumo usa (con hash).  
- [ ] Revisión por un par del equipo de datos.  
- [ ] Las tres condiciones del criterio de cierre (§0) marcadas.

---

## 14\. Riesgos del EDA

| ID | Riesgo | Mitigación |
| :---- | :---- | :---- |
| R-01 | ~~El diccionario de columnas no es accesible~~ → **CERRADO.** Existe un diccionario interno en la hoja `Distancia_0` (Anexo D) que cubre las 17 columnas y el catálogo CLASE FWO | Usar el diccionario interno como fuente primaria. El Google Sheets externo queda como contraste opcional, no como bloqueo |
| R-02 | ~~Los dos archivos provienen de extracciones distintas~~ → **CERRADO Y CORREGIDO.** La afirmación era un artefacto de lectura: las "13 filas faltantes" eran el diccionario y el catálogo pegados bajo el dato. Los 5 subconjuntos son consistentes con el dataset | Riesgo retirado. Se conserva como lección: inspeccionar el rango real del Excel antes de leerlo (§2.4) |
| R-02b | **Riesgo nuevo:** lecturas ingenuas de Excel producen hallazgos falsos y erosionan la confianza en el informe | Inspección con `openpyxl` como primera celda; todo conteo que sustente un hallazgo se valida con un segundo método antes de publicarse |
| R-03 | ~~Posible confusión costo/venta~~ → **ESCALADO A HECHO.** La columna rotulada `Costo Producción` contiene la tarifa de venta (872/874 verificado por valor \+ definición oficial) | No es ya un riesgo a vigilar sino un defecto a reportar. Exigir corrección del rótulo en origen y prohibir el uso de esas 4 hojas para cálculos de costo |
| R-06 | **Riesgo nuevo:** el catálogo declara 10 líneas de negocio y el set trae 7; el alcance real del set puede ser menor al que se cree | Resolver §12.1-bis con el negocio antes de declarar el set como representativo de la operación de Buenaventura |
| R-04 | Analizar sin definir el grano lleva a métricas infladas | §4 es prerequisito de §7 en adelante |
| R-05 | El negocio no responde las preguntas de §1.2 a tiempo | Entregar el EDA con las limitaciones marcadas como "no resuelto por falta de definición", no adivinar |

---

## 15\. Secuencia de ejecución sugerida

| Orden | Fase | Bloquea a |
| :---- | :---- | :---- |
| 1 | §1 Encuadre y preguntas al negocio | Todo (se puede avanzar en paralelo mientras responden) |
| 2 | §2 Montaje reproducible | Todo |
| 3 | §3 Perfilado estructural | §4 en adelante |
| 4 | §4 Unicidad y granularidad | §6, §7, §8 (toda métrica agregada) |
| 5 | §5 Calidad | §7 (define qué registros se marcan) |
| 6 | §6 Univariado | §7 |
| 7 | §7 Bivariado / multivariado | §11 |
| 8 | §8 Temporal y §9 Geográfico | §11 |
| 9 | §10 Outliers | §11 |
| 10 | §11 Sesgos | §12 |
| 11 | §12 Limitaciones | §13 |
| 12 | §13 Cierre | — |

---

## Anexo A — Baseline cuantitativo ya verificado

Cifras calculadas directamente sobre `Viajes-Btura-2025-2026.xlsx` (41.237 filas). Sirven como punto de partida y como control: si el notebook no las reproduce, algo cambió en la carga.

| Indicador | Valor |
| :---- | :---- |
| Filas / columnas | 41.237 / 17 |
| Duplicados de fila completa | 0 |
| `FO` repetidos | 130 |
| Rango de fechas | 2025-04-01 → 2026-04-07 |
| Mes parcial | 2026-04 (523 registros) |
| Nulos `Grupo de Equipo FWOP` | 7.496 (18,18 %) — **estructurales** (= PKG \+ PRD) |
| Nulos `Clase Equipo FOP` | 7.499 (18,19 %) — 7.496 estructurales \+ **3 defectos reales** en TUR |
| Nulos en ambas simultáneamente | 7.496 |
| Registros `Origen = Destino` legítimos (OHUB \+ OH3P) | 7.953 (88,7 % de los O=D) |
| Registros `Origen = Destino` sospechosos (OPIM \+ OPTN) | **978** (2,4 % del set) |
| Líneas de negocio del catálogo ausentes en el set | 3 de 10 (OHUC, OPSC, OPXL) |
| `Distancia Total` \= 0 | 17 (0,04 %) |
| `Peso Neto` \= 0 | 82 (0,20 %) |
| `Tarifa venta al cliente` \= 0 | 874 (2,12 %) |
| `Flete producción FO` \= 0 | 183 (0,44 %) |
| `Flete producción FO` \< 0 | 1 |
| Margen negativo (tarifa \< flete) | 3.839 (9,31 %) |
| `Origen` \= `Destino` | 8.966 (21,74 %) |
| Clientes distintos | 537 |
| Vehículos distintos | 3.418 |
| Orígenes / destinos distintos | 113 / 122 |
| Buenaventura como origen | 30.820 (74,7 %) |
| Buenaventura como destino | 19.383 (47,0 %) |
| Propiedad Tercero / Propio | 37.595 / 3.642 |

## Anexo B — Correspondencia entre el dataset y el archivo de inconsistencias

| Hoja | Filas de dato real | FOs en el dataset | Contenido extra en la hoja | Equivalencia |
| :---- | :---- | :---- | :---- | :---- |
| `Distancia_0` | **17** | 17 (100 %) | Fila 2 \= diccionario de columnas; filas 21–31 \= catálogo CLASE FWO | `Distancia Total` \= 0 |
| `Costo_0` | 874 | 874 (100 %) | — | `Costo Producción` ≡ `Tarifa venta al cliente` \= 0 ⚠️ |
| `ImporteFO_0` | 183 | 183 (100 %) | — | `Importe FO` ≡ `Flete producción FO` \= 0 |
| `Peso_Neto_0` | 82 | 82 (100 %) | — | `Peso Neto` \= 0 |
| `ImporteFO_Negativo` | 1 | 1 (100 %) | — | `Flete producción FO` \< 0 |

**Los 5 subconjuntos son íntegramente consistentes con el dataset.** La versión anterior de este anexo reportaba 30 filas en `Distancia_0` con solo 17 coincidencias; era un error de lectura (ver §5.6).

**Nota (2026-09-09, notebook `02_calidad_del_dato.ipynb`, Paso 11):** `Costo_0` tiene 874 filas pero solo **869 `FO` únicos** — 5 `FO` se repiten dentro de esta misma hoja de excepciones, consistente con el patrón de carga consolidada ya documentado en §4.2 (no es un error, es el mismo fenómeno visto también aquí). Las otras 4 hojas no presentan esta diferencia.

⚠️ `Costo Producción` no es un costo: contiene la tarifa de venta. Verificado por valor en 872 de 874 filas, y respaldado por la definición oficial del diccionario interno.

## Anexo C — Catálogo de reglas de validación (para `src/reglas_calidad.py`)

Implementar cada regla como una función que devuelve un DataFrame con los registros que la incumplen, para que el conteo del informe y la lista de casos salgan del mismo lugar.

```
R01  distancia_no_nula          Distancia Total > 0
R02  peso_no_nulo               Peso Neto > 0
R03  tarifa_no_nula             Tarifa venta al cliente > 0
R04  flete_no_nulo              Flete producción FO > 0
R05  flete_no_negativo          Flete producción FO >= 0
R06  margen_no_negativo         Tarifa venta al cliente >= Flete producción FO
R07  origen_distinto_destino    Origen != Destino
R08  fo_unico                   FO sin duplicados
R09  equipo_clasificado         Grupo de Equipo FWOP y Clase Equipo FOP no nulos
R10  medio_transporte_valido    ID Medio Transporte FO en dominio conocido
R11  tipo_coherente_medio       Tipo consistente con ID Medio Transporte FO
R12  fecha_en_ventana           Fecha Creación FO dentro del rango declarado
R13  peso_bajo_capacidad        Peso Neto <= capacidad del Tipo
R14  distancia_estable_od       Desv. de Distancia por par O-D bajo umbral
R15  ciudad_normalizada         Origen y Destino en catálogo de municipios
```

## Anexo D — Diccionario interno (transcripción literal)

Fuente: archivo `inconsistencias_viajes 1.xlsx`, hoja `Distancia_0`. **No está en una hoja aparte: está embebido dentro de una hoja de datos** (fila 2 y filas 21–31), por lo que cualquier lectura tabular ingenua lo confunde con registros.

### D.1 Definición por columna (fila 2\)

| Columna | Definición literal |
| :---- | :---- |
| `ID Clase Documento FWO` | "Corresponde al código que representa la línea de negocio definida por la compañía (Ver tabla CLASE FWO en la parte inferior)." |
| `FO` | "Documento producto de la planificación, es el documento de subcontratación con el dueño del vehículo que transporta la carga (Equivale al número del viaje)." |
| `Fecha Creación FO` | "Fecha en la cual fue creada la FO en el sistema." |
| `BP Cliente` | "Corresponde al código de la empresa con la cual hacemos el contrato de transporte, cliente al que le movilizamos la carga." |
| `Origen` | "Población donde se origina el transporte de la carga." |
| `Destino` | "Población donde termina el transporte de la carga." |
| `Distancia Total` | "Distancia total, en KM, de la operación de transporte." |
| `Peso Neto` | "Peso de la carga transportada sin tener en cuenta el peso o tara del vehículo." |
| `Posición Principal FWOP` | "Posición de carga registrada en la FWO, la cual puede ser PKG (Paquete), PRD (Producto) o TUR (Transportation Unit Resource)." |
| `Grupo de Equipo FWOP` | "Este aplica solo para la posición principal TUR, y puede ser CAM para indicar si es camión o CN para indicar que es un contenedor." |
| `Vehículo` | "Placa del vehículo que realiza la operación de transporte." |
| `ID Medio Transporte FO` | "Categoría o tipo de vehículo para transportar la carga, de acuerdo al modo de transporte terrestre. Ejemplo TC-3S2 indica que es un tracto camión de tres ejes con un trailer enganchado de dos ejes." |
| `Clase Equipo FOP` | "Clase de equipo utilizado, aplica solo para grupo de equipo CN y CAM." |
| `Propiedad` | "Indica si el recurso utilizado corresponde a un vehículo de nuestra flota propia o es de un tercero externo a la compañía." |
| `Tipo` | **"Puede omitirse."** |
| `Tarifa Venta Cliente` | "Tarifa de venta del viaje realizado." |
| `Flete Producción FO` | "Costo de producción o flete que se paga al Transportista." |

### D.2 Catálogo CLASE FWO (filas 21–31)

| Código | Descripción | Registros en el set |
| :---- | :---- | :---- |
| `OH3P` | Pedido Descontenerización Terceros | 1.871 |
| `OHUB` | Pedido Descontenerización | 6.092 |
| `OHUC` | Pedido Contenerización | **0 — ausente** |
| `OPDS` | Pedido Distribución Nacional | 65 |
| `OPEX` | Pedido interior a Puerto | 3.877 |
| `OPIM` | Pedido de Puerto a interior | 27.813 |
| `OPSC` | Pedido sin Cálculo de costos | **0 — ausente** |
| `OPTI` | Pedido Internacional | 22 |
| `OPTN` | Pedido Nacional | 1.497 |
| `OPXL` | Pedido Extradimensionado y rodados | **0 — ausente** |

*(Transcripción fiel, incluidos los errores de digitación del original: "Dsitribución", "origna", "Correponde".)*

### D.3 Lo que el diccionario NO cubre

- Unidad de `Peso Neto` (kg vs t).  
- Moneda de las columnas monetarias, y si incluyen IVA.  
- Si `Distancia Total` es planificada o recorrida real.  
- Maestro de `BP Cliente` (código → nombre).  
- Criterio de extracción y estados excluidos.  
- Significado de los sufijos de `Clase Equipo FOP` (CNT40.HC, CNT20.ST, etc.) más allá de lo inferible.

---

**Supuestos de este plan:** stack Python (pandas, matplotlib/seaborn, Jupyter) y repo Git. Si el equipo trabaja con R, Databricks u otra herramienta, la estructura de fases se mantiene y solo cambia el andamiaje de la §2.  
