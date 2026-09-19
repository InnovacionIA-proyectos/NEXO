# Plan de EDA — Extracción que cruza las 4 fuentes (S2-HU-11 parte 2)

**Historia:** `EDA sobre la extracción que cruza las fuentes` **Objetivo:** análisis exploratorio del dataset consolidado de S2-HU-10 (BD propia + piso SICE-TAC histórico + plaza/histórico SAP + clientes), con el mismo rigor del EDA-1, evaluando qué **señal** aparece al cruzar el contexto de mercado que el set de históricos no tenía (p. ej. relación cotización vs. piso/plaza del período).

| Campo | Valor |
| :---- | :---- |
| Insumo | `analitica/01-set-final/v1.0.0-piloto/` — snapshot congelado, 13 CSV lado propio + 4 CSV lado externo, **sin aplanar** (grano propio por tabla, cruce por llaves documentadas) |
| Diccionario | `01-set-final/v1.0.0-piloto/diccionario-y-linaje.md` — ya existe, autocontenido, con llaves de cruce documentadas. No hay que reconstruirlo desde cero como en el EDA-1 |
| Calidad ya reportada | `01-set-final/v1.0.0-piloto/reporte-inconsistencias.md` — 5 hallazgos ya documentados por HU-10.3, se heredan como baseline, no se re-descubren |
| Dependencia | S2-HU-10.3 (ya entregada, `v1.0.0-piloto`) |
| Volumen (lado propio) | 13 solicitudes, 18 cotizaciones, 48 líneas de cotización — **muestra chica y real, no producción** |
| Volumen (lado externo) | 151 filas SICE-TAC (muestra estratificada, no la tabla completa), 23.020 tarifa-plaza, 25.318 histórico SAP, 38.995 clientes SAP |
| Fecha del plan | 2026-09-10 |

> **Nota de alcance.** Igual que el EDA-1: esto es un EDA sobre un set ya entregado, no un proyecto de ingeniería de datos. El objetivo es caracterizar la señal de mercado disponible y dejar por escrito sus límites — no corregir el dato ni construir el cruce definitivo (eso, si aplica, es del futuro modelo).

---

## 0. Criterio de cierre (Definition of Done)

Igual que el EDA-1, más un criterio nuevo propio de esta HU:

- [ ] **Notebook reproducible y versionado en el repo** — corre de principio a fin en limpio (`Restart & Run All`), sin rutas locales ni celdas manuales.
- [ ] **Hallazgos con contexto de mercado documentados** — no basta con calidad/distribuciones genéricas: cada hallazgo relevante debe decir qué aporta (o no) para entender cotización vs. piso/plaza.
- [ ] **Evidencia comparable con el EDA-1** — mismas 7 dimensiones de calidad, mismas fases de análisis, mismo formato de fichas de hallazgo y tabla de limitaciones. Si una fase no aplica a este dataset (ej. no hay coordenadas), se declara explícitamente por qué, no se omite en silencio.

Criterios de aceptación transversales (idénticos al EDA-1):

- [ ] Todo número afirmado en el informe es reproducible desde una celda del notebook.
- [ ] Ninguna conclusión se apoya en una columna cuyo significado no esté confirmado (aquí ya hay diccionario — pero donde el propio diccionario dice "pendiente de confirmar con negocio", se respeta esa etiqueta, no se asume).
- [ ] Las decisiones tomadas ante ambigüedad quedan registradas en el log de supuestos (§13).

---

## 1. Fase 0 — Encuadre antes de tocar el dato

**Objetivo:** a diferencia del EDA-1 (donde el diccionario había que reconstruirlo), aquí el diccionario y el linaje **ya existen** (HU-10.3). El encuadre se enfoca en traducir eso a un plan de análisis, no en descubrirlo.

### 1.1 Qué pregunta de negocio responde este EDA

| # | Pregunta | Columnas que la responderían | ¿Set suficiente? |
| :---- | :---- | :---- | :---- |
| P1 | ¿Qué tan cerca cotiza CabinaFletes respecto al piso SICE-TAC / la plaza SAP vigente en el mismo período? | `cotizacion_lineas.ValorFlete`+`Fuente` vs. `VALORSICECONHORA` (SICE-TAC) o `VALOR` (SAP), cruzados por ruta+período | Parcial — ver §1.3, la cobertura real de cruce es chica |
| P2 | ¿El motor de costeo realmente usa la fuente que dice usar (`Fuente=Sicetac`/`Tarifario`)? | Ya verificado por HU-10.3 (14/15 casos coinciden exacto) — este EDA solo debe reconfirmarlo con código propio, no es hallazgo nuevo | Sí, ya resuelto — reconfirmar, no reinvestigar |
| P3 | ¿Qué variación de precio hay en el mercado (SICE-TAC/SAP) para las mismas rutas/líneas de negocio que cotiza CabinaFletes? | `tblRutasSicetac`/`tblSAPTarifas` segmentado por las mismas dimensiones que usa `lineas_solicitud` | Sí para el lado externo solo; el cruce contra lo propio depende de qué tan bien se puedan enlazar (ver §1.3) |
| P4 | ¿El bajo volumen real (13 solicitudes) permite alguna conclusión generalizable? | Todas | No — ya declarado como limitación estructural desde HU-10.3, se hereda tal cual |

### 1.2 Definiciones — heredadas de HU-10.3, no se re-derivan

El diccionario (`diccionario-y-linaje.md`) y el reporte de inconsistencias ya resuelven la mayoría de lo que en el EDA-1 costó varios pasos descubrir. Estado heredado:

| # | Pregunta | Estado | Fuente |
| :---- | :---- | :---- | :---- |
| 1 | ¿Qué es cada columna? | ✅ Resuelto | `diccionario-y-linaje.md`, autocontenido |
| 2 | ¿Cuál es el piso SICE-TAC efectivo? | ✅ Resuelto | `VALORSICECONHORA`, no `VALOR` (verificado 6/6 en HU-10.3) |
| 3 | ¿La tarifa de plaza SAP es un solo valor por ruta? | ⚠️ Parcial | Puede haber múltiples tarifas "vigentes" simultáneas (caso extremo: 567 candidatos Villavicencio↔Buenaventura en SICE-TAC) — no asumir una tarifa única por ruta |
| 4 | ¿`tblSAPTarifasLog` es "tarifa ganada" o "log de auditoría"? | ❌ Abierto, ya escalado | Pendiente de confirmar con negocio (documentado en `00-fuentes/fuentes-corporativas/README.md`) — este EDA no debe asumir ninguna de las dos sin más evidencia |
| 5 | ¿`VIGENCIA` de SAP está corregida? | ✅ Resuelto (es cruda) | Se entrega sin `AjusteVigenciaPlaza` a propósito — el 95,8% de `tblSAPTarifas` completa está vencida a la fecha de hoy (hallazgo previo, HU-09.2) |
| 6 | ¿Cómo se cruza `Ruta` entre lado propio y externo? | ❌ Abierto, es trabajo de este EDA | Lado propio y SICE-TAC usan texto libre sin código DANE; SAP usa código DANE o zona (`ZC_`/`ZTC_`). Ninguna normalización está hecha — es la pieza central que este EDA debe resolver o declarar como limitación |
| 7 | ¿Qué tan bien se cruza `LineaNegocio`/`ClaseViaje`/`ClaseContenedor`? | ⚠️ Por cuantificar | El diccionario dice que son "la misma llave" en ambos lados, pero **no dice qué tan seguido está poblada** — ver hallazgo preliminar en §1.4 |

**Solo quedan 2 preguntas de fondo para este EDA** (6 y 7) — el resto ya viene resuelto. Esto es una diferencia importante con el EDA-1: aquí el trabajo pesado es de **cruce y cuantificación de cobertura**, no de descubrimiento de significado.

### 1.3 Reconciliación diccionario ↔ dataset real

Ejercicio obligatorio, igual que en el EDA-1, pero más corto porque el diccionario ya es de alta calidad:

- Confirmar que las columnas documentadas existen tal cual en cada CSV (spot-check, no se espera sorpresa).
- Confirmar tipos declarados (fecha, GUID, booleano, categórico) vs. inferidos al cargar.
- **Cuantificar, no asumir, la cobertura real de las llaves de cruce** (§1.2 punto 7) — este es el número más importante de toda la fase de encuadre, porque determina qué tan grande puede ser cualquier análisis de "señal de mercado" más adelante.

### 1.4 Hallazgo preliminar ya detectado (a verificar formalmente en el notebook)

Durante el encuadre de este plan se encontró, en al menos 3 archivos (`cotizacion_lineas.csv`, `solicitudes.csv`, `piso-sicetac-tblRutasSicetac-muestra-202608.csv`), **una fila extra con valores tipo separador** (`"------"`, `"---------"`, `"----------"`) — el conteo de filas real de cada archivo sale exactamente 1 fila más alto que lo que documenta el acta de entrega (`README.md`). Esto es muy similar en espíritu al hallazgo del diccionario embebido del EDA-1 (Anexo D): un artefacto de exportación colado como si fuera un registro real.

**Regla derivada para la Fase 1 (carga defensiva):** antes de cualquier análisis, verificar en **los 17 archivos** (no solo los 3 donde ya se detectó) si existe una fila final de este tipo, y excluirla explícitamente con evidencia (no asumir que solo afecta a esos 3).

### 1.5 El sesgo de muestreo de `piso-sicetac` no es opcional de mencionar — es estructural

A diferencia de todo lo demás en este set, `piso-sicetac-tblRutasSicetac-muestra-202608.csv` **no es una muestra aleatoria** de `tblRutasSicetac` (que pesa 824 MB completa). Es una muestra **estratificada a propósito**: 126 filas "núcleo" (6 por cada combinación real tipo de carga × condición × unidad de transporte) + 25 filas deliberadamente elegidas por ser **extremas** (valor máximo, valor mínimo, ambigüedad de 567 candidatos, vía no estándar, horas máximas — ver columna `Muestra`).

**Implicación que hay que respetar desde la Fase 5 (univariado) en adelante:** calcular "la distribución de `VALORSICECONHORA`" sobre este archivo tal cual, sin filtrar por `Muestra == "nucleo"` primero, daría una imagen **artificialmente sesgada hacia los extremos** (16,6% de las filas son outliers elegidos a propósito, no naturales). Cualquier estadístico descriptivo debe calcularse sobre el subconjunto `nucleo`, y los `rara_*` se analizan aparte, como casos de referencia, no como parte de la distribución típica.

**Entregable de la fase:** tabla de reconciliación (breve, se espera que confirme el diccionario existente) + cuantificación de cobertura de llaves de cruce + confirmación de la fila fantasma en los 17 archivos.

---

## 2. Fase 1 — Montaje reproducible

**Objetivo:** igual que el EDA-1 — que el notebook corra igual hoy, en el equipo de otro, y en seis meses.

### 2.1 Estructura de repo

Ya existe (ver `README.md` de esta carpeta):

```
analitica/03-eda-extraccion-4-fuentes/
├── README.md
├── PLAN_EDA_Extraccion_4_Fuentes.md   # este plan
├── requirements.txt                    # mismas versiones que el EDA-1, para comparabilidad
├── datos/
│   └── interim/                        # tablas tipadas/unidas parcialmente, generadas por código
├── notebooks/
└── reportes/
    ├── figures/
    ├── hallazgos.md
    └── limitaciones.md
```

No se copian los datos crudos aquí — se leen directamente desde
`../01-set-final/v1.0.0-piloto/datos/` (ya es un snapshot congelado, autocontenido; duplicarlo otra vez sería redundante).

### 2.2 Reglas de reproducibilidad (idénticas al EDA-1)

- Versiones fijas de librerías (`requirements.txt`, ya creado con las mismas versiones del EDA-1).
- Rutas relativas al repo, nunca `C:\Users\...`.
- Semilla fija donde haya muestreo (`random_state=42`) — aunque aquí no se espera muestrear de nuevo, `piso-sicetac` ya viene muestreado por HU-10.2.
- Hash MD5 de cada uno de los 17 archivos crudos registrado en la primera celda (más archivos que en el EDA-1 — un solo hash no alcanza).
- Fecha y hora de ejecución impresas en la primera celda.

### 2.3 Versionado

- Rama de esta HU (`40-s2-hu-112-eda-2-...`), PR a `QA` (CI verde + 1 revisor); Kevin (@kcano-ux) valida y hace el PR `QA` → `main`.
- Commits atómicos con mensaje descriptivo.
- PR con el informe de hallazgos en la descripción, referenciando el issue de esta HU.

### 2.4 Carga defensiva — con la lección del EDA-1 ya aplicada

- **Primera celda: hash de los 17 archivos.**
- **Segunda: verificar la fila fantasma tipo "------" en cada uno de los 17 archivos** (§1.4) — contar filas totales vs. lo documentado en el acta de entrega, y excluir explícitamente cualquier fila separador encontrada, dejando evidencia (cuántas, en cuáles archivos).
- Leer con `dtype=str` en una primera pasada (igual que el EDA-1), tipar después con conversión controlada.
- `piso-sicetac`: separar `Muestra == "nucleo"` de las `rara_*` **desde la carga**, no más adelante — es la lección de §1.5.
- Persistir a Parquet en `datos/interim/`, un archivo por tabla (no aplanar).
- Normalizar nombres de columna a camelCase, igual que el EDA-1 (consistencia entre ambos EDA).

---

## 3. Fase 2 — Inventario y perfilado estructural

**Objetivo:** igual que el EDA-1, pero repetido **por cada una de las 17 tablas** (no una sola tabla ancha).

### 3.1 Checklist (por tabla)

- [ ] Dimensiones y peso en memoria (todas son chicas salvo las 3 tablas SAP/clientes).
- [ ] Listado de columnas: tipo declarado (según diccionario) vs. inferido, nulos, % nulos, únicos, ejemplo.
- [ ] `head()`, `tail()` y muestra aleatoria — con la salvedad de que las tablas del lado propio son tan chicas (13-72 filas) que puede tener sentido mostrarlas completas en vez de una muestra.
- [ ] Clasificación de columnas por naturaleza (identificador/fecha/categórica/numérica/booleana) — ya está prácticamente hecha en el diccionario, aquí se verifica contra el dato real.
- [ ] Detección de columnas constantes o cuasi-constantes (ej. `TYPE` en `catalogo-clientes` ya se sabe que es siempre `'2'` — confirmar, no autoexcluir sin verificar).

### 3.2 Foco especial: cobertura de las llaves de cruce

Esta es la tabla que reemplaza al "Anexo A" del EDA-1 como entregable central de esta fase — no es solo perfilado, es la base numérica de todo lo que sigue:

| Llave | Tabla origen | % de filas con la llave poblada | Cuántas de esas encuentran match en la tabla destino |
| :---- | :---- | :---- | :---- |
| `LineaNegocio` | `lineas_solicitud` | Solo ~5 de 44 filas la traen poblada (hallazgo preliminar, cuantificar exacto) | Por calcular |
| `ClaseViaje` | `lineas_solicitud` | Similar — cuantificar exacto | Por calcular |
| `ClaseContenedor` | `lineas_solicitud` | Similar — cuantificar exacto | Por calcular |
| NIT (cliente) | `catalogo_clientes` ↔ `catalogo-clientes-BUT000` | Por calcular | Por calcular |
| Ruta (texto libre → DANE) | `catalogo_rutas` ↔ `tarifa-plaza`/`piso-sicetac` | Sin normalizar todavía — 0% hasta que se resuelva §4 (unicidad/normalización) | — |

**Por qué esto va primero y no al final:** si la cobertura real de cruce es tan baja como sugiere el hallazgo preliminar (~11% de `lineas_solicitud`), hay que saberlo **antes** de diseñar el análisis bivariado de la Fase 6 — determina si "la señal de mercado" se puede evaluar sobre decenas de casos o solo sobre un puñado.

---

## 4. Fase 3 — Unicidad y granularidad

**Objetivo:** igual que el EDA-1 — entender qué representa una fila, en **cada una** de las 17 tablas (aquí es más simple: el diccionario ya declara el grano de cada una, hay que verificarlo, no descubrirlo).

### 4.1 Pruebas por tabla

- [ ] Duplicados de fila completa, por tabla.
- [ ] Confirmar que `Id`/`ID` es clave única en cada tabla (se espera que sí — son GUID o autoincrementales de sistema, a diferencia del `FO` del EDA-1 que resultó no serlo).
- [ ] Confirmar cardinalidad de las relaciones 1-a-muchos documentadas (ej. una `solicitud` → varias `lineas_solicitud`; una `cotizacion` → varias `cotizacion_lineas`) — contar cuántas de cada lado, ver si hay huérfanos (línea sin solicitud padre, cotización sin línea, etc.).
- [ ] **Grano de `piso-sicetac`:** confirmar que `Muestra` no rompe la unicidad esperada (¿puede una misma ruta aparecer tanto en `nucleo` como en una categoría `rara_*`? Verificar, no asumir que son mutuamente excluyentes).
- [ ] **Grano de `tblSAPTarifasLog`:** cada fila es una versión histórica de una tarifa (`ID_TARIFA` la enlaza a `tblSAPTarifas`) — confirmar cuántas versiones tiene en promedio cada tarifa, y si eso es consistente con "auditoría de cada edición" (la hipótesis no confirmada del diccionario) o con otra explicación.

### 4.2 Huérfanos y referencias rotas (equivalente a integridad referencial, adelantado aquí porque en un modelo relacional es parte de la granularidad)

- ¿Toda `lineas_solicitud.SolicitudId` existe en `solicitudes.Id`?
- ¿Toda `cotizacion_lineas.CotizacionId` existe en `cotizaciones.Id`? ¿Y `LineaSolicitudId` en `lineas_solicitud.Id`?
- ¿Todo `RutaId`/`ConfigVehiculoId`/`ClienteId` resuelve contra su catálogo?

**Entregable de la fase:** mapa de cardinalidad real entre las 13 tablas propias (cuántos hijos por padre, huérfanos si los hay) — el equivalente aquí a la "clave compuesta" que hubo que buscar en el EDA-1.

---

## 5. Fase 4 — Calidad del dato

**Objetivo:** igual que el EDA-1 — cuantificar 7 dimensiones, un número por cada una, no un adjetivo. Aquí gran parte del trabajo de calidad **ya está hecho** por `reporte-inconsistencias.md` (HU-10.3); esta fase reconfirma esos 5 hallazgos con código propio y busca lo que ese reporte no cubrió (llaves de cruce, sesgo de muestreo, fila fantasma).

### 5.1 Completitud

- % de nulos por columna, por tabla — con foco en `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` (§3.2).
- Confirmar con código el hallazgo ya documentado: **13 solicitudes reales** es el volumen de referencia — cualquier análisis debe recordarlo en cada tabla derivada.

### 5.2 Unicidad

Cubierto en §4.

### 5.3 Validez (rangos y formatos)

- Reconfirmar con código los 2 hallazgos puntuales ya conocidos: `Deadline` anterior a `CreatedAt` (1 caso, `SolicitudId 3EAB0AB6-...`) y la ruta malformada `"b"`/`"j"` (`LineaCotizacionId CA0FC8A8-...`).
- Verificar la fila fantasma (§1.4) en los 17 archivos — este es un hallazgo **nuevo** de este EDA, no heredado.
- Rangos físicos: `ProbabilidadCierre` entre 0-100, `ScorePrioridad` entre 0-100, valores monetarios no negativos (salvo que el negocio confirme que existen notas crédito, como en el EDA-1).

### 5.4 Consistencia (dominios y reglas cruzadas)

- **Reconfirmar el hallazgo más importante del set (HU-10.3):** de 15 líneas verificables (`Fuente` en `Sicetac`/`Tarifario`), 14 coinciden exacto contra la fuente externa correspondiente, usando `VALORSICECONHORA` para `Sicetac` (no `VALOR`) y `VALOR` para `Tarifario`. Reproducir esta verificación con código propio, no solo citar el hallazgo previo.
- Verificar que `TYPE` en `catalogo-clientes-BUT000` sea consistentemente `'2'` (ya filtrado en el extracto, según el diccionario).
- Verificar consistencia de las banderas booleanas (`Exclusivo`/`Consolidado`/`RetornaContenedor`, etc.) — recordar que en `tarifa-plaza`/`historico-sap` estas vienen como texto `True`/`False` (efecto secundario documentado de la corrección de comillas RFC4180 de HU-10.2), no como `1`/`0`.

### 5.5 Exactitud (plausibilidad)

- Confirmar que `ValorFlete` de `cotizacion_lineas` con `Fuente=Manual` no tiene por qué coincidir con ninguna fuente externa (es la categoría "sin verificación cruzada posible", ya documentada).
- Para las líneas con match real contra SICE-TAC/SAP (los "candidatos" de §3.2): calcular la diferencia (`ValorFlete` − piso/plaza) y su signo — este número es el corazón de la "señal de mercado" que pide la HU, se profundiza en la Fase 6.

### 5.6 Integridad referencial

Cubierto en §4.2 (huérfanos), más:

- Confirmar el hallazgo ya conocido de **permisos de escritura de `consultascubos` en `DWCubosOperacionesEBS`** — no es nuevo para investigar, solo repetirlo como recordatorio (ya reportado a IT desde HU-10.2/10.3, no bloquea este EDA).

### 5.7 Oportunidad y cobertura temporal

- Ventana real de `tblSAPTarifas`/`tblSAPTarifasLog`: desde 2024-01-31 (ya documentado, cubre el 100% de ambas tablas).
- `tblRutasSicetac`: un solo período (`PERIODO=202608`) — **no tiene profundidad histórica real**, a diferencia de su nombre. Cualquier intento de "serie de tiempo" sobre SICE-TAC no es posible con este archivo — es una limitación a declarar, no a intentar rodear.
- Ventana del lado propio: `CreatedAt >= 2026-08-22` hasta la fecha de extracción (2026-09-01) — 10 días de datos reales.
- **Cruce de período:** el lado propio (agosto-septiembre 2026) solo puede compararse contra SICE-TAC en su único período disponible (agosto 2026, coincide) — contra SAP sí hay más flexibilidad porque `VIGENCIA` tiene profundidad real desde 2024.

### 5.8 Salida de la fase

Misma tabla resumen de calidad que el EDA-1 (dimensión, regla, registros afectados, %, severidad, decisión, responsable) — con una columna adicional: **"¿Ya estaba en `reporte-inconsistencias.md`?"** para distinguir lo heredado de HU-10.3 de lo nuevo que aporte este EDA.

---

## 6. Fase 5 — Análisis univariado

**Objetivo:** conocer la forma de cada variable relevante, **tabla por tabla** — con la salvedad de §1.5 para `piso-sicetac`.

### 6.1 Numéricas

- `ValorFlete`, `ValorAprovisionamiento`, `ValorMercancia` (lado propio); `VALOR`/`VALORSICECONHORA`/`VALORHORA` (SICE-TAC, **solo sobre `Muestra == "nucleo"`**); `VALOR` (SAP, aquí sí es toda la tabla, no muestreada).
- Dado el volumen chico del lado propio (≤72 filas por tabla), los estadísticos se acompañan siempre del **valor exacto de cada fila**, no solo del resumen — a esta escala, mostrar todos los datos es más honesto que resumir.
- Para SAP (23.020 y 25.318 filas, sí amerita histograma/percentiles como en el EDA-1).

### 6.2 Categóricas

- `Tipo`, `Modalidad`, `Estado` (solicitudes); `Fuente` (cotizacion_lineas — la más importante de esta fase); `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` (con el recordatorio de que la mayoría son nulos).
- Índice de concentración — con la salvedad de que a este volumen (13-48 filas) "concentración" puede ser un solo caso repetido, hay que decirlo así de literal, no disfrazarlo de porcentaje.

### 6.3 Fecha

- Distribución de `CreatedAt` en el lado propio (10 días de ventana — no da para estacionalidad, ya se sabe).
- `VIGENCIA` de SAP: distribución completa desde 2024 — aquí sí aplica ver si hay estacionalidad/tendencia real.

### 6.4 Redundancia entre columnas

- `TIPOVEHICULO`/`VehiculoDescripcion` y `LINEANEGOCIO`/`LineaNegocioDescripcion` en las tablas SAP son pares código↔texto — confirmar 1 a 1 (a diferencia del EDA-1, donde `Tipo` vs `ID Medio Transporte FO` sí tenían discrepancias reales).

---

## 7. Fase 6 — Análisis bivariado y multivariado (el corazón de esta HU)

**Objetivo:** esta es la fase que responde la pregunta central de la HU — "qué señal aparece al cruzar el contexto de mercado". Todo lo anterior existe para llegar aquí con confianza en los números.

### 7.1 Cruce cotización propia ↔ piso SICE-TAC

Solo sobre las líneas con `Fuente = "Sicetac"` (§3.2 ya debería haber cuantificado cuántas son) y que además logren un match real contra `piso-sicetac` (`Muestra == "nucleo"`, por ruta/tipo de carga/condición/unidad de transporte):

- `ValorFlete` (propio) vs. `VALORSICECONHORA` (mercado) — diferencia absoluta y %.
- Reconfirmar HU-10.3: ¿siguen coincidiendo exacto, o hay diferencia cuando se amplía más allá de las 6 líneas ya verificadas?
- Documentar explícitamente cuántas líneas con `Fuente=Sicetac` **no** lograron match (por ejemplo, si su ruta cae en alguna de las 567 candidatas de Villavicencio↔Buenaventura, ¿contra cuál de las 567 se comparó, y con qué criterio de desempate?).

### 7.2 Cruce cotización propia ↔ plaza SAP

Mismo ejercicio para `Fuente = "Tarifario"`, cruzando por `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` contra `tblSAPTarifas.VALOR` — con la salvedad de §1.2 punto 3 (puede haber más de una tarifa vigente simultánea para la misma combinación, hay que decidir/documentar el criterio de desempate, no asumir que hay una sola).

### 7.3 Variación del mercado en sí (sin depender del cruce con lo propio)

Aunque el cruce directo sea chico, el lado externo por sí solo permite caracterizar el mercado:

- Dispersión de `VALOR`/`VALORSICECONHORA` en SICE-TAC por tipo de carga/condición/unidad de transporte (**solo `nucleo`**).
- Dispersión de `VALOR` en `tblSAPTarifas` por `LineaNegocio`/`TIPOVEHICULO` — esto es información de mercado con valor propio, independiente de que se pueda cruzar contra las 13 solicitudes reales o no.
- Los 25 casos `rara_*` de SICE-TAC se analizan aparte, como catálogo de "situaciones extremas conocidas" (ya narradas en el README de la fuente), no se mezclan con `nucleo`.

### 7.4 Categórica ↔ categórica

- `Modalidad` (propio) × líneas de negocio disponibles en SAP — ¿el catálogo de modalidades propias tiene equivalente claro del lado SAP, o hay conceptos que no cruzan?
- `Fuente` × `Resultado` de la cotización (`Ganada`/`Perdida`/`SinDefinir`) — con el aviso de que a 18 cotizaciones cualquier patrón es anecdótico, no estadístico.

### 7.5 Multivariado

- Dado el volumen (máximo unas pocas decenas de casos con cruce real), **no aplica clustering ni PCA** como en el EDA-1 — sería sobreajustar a ruido. Se declara explícitamente como no aplicable, no se fuerza.

---

## 8. Fase 7 — Análisis temporal

- Sobre SAP (`VIGENCIA` desde 2024): tendencia del valor de tarifa en el tiempo, por línea de negocio — esto sí tiene profundidad suficiente para ser informativo, a diferencia del EDA-1 donde la ventana era de 13 meses.
- Sobre lo propio: solo se puede describir la ventana de 10 días (`CreatedAt`), sin pretender estacionalidad.
- SICE-TAC: **no aplica** — un solo período (§5.7), se declara como limitación, no se fuerza un análisis temporal sobre un solo punto.
- Si el patrón de ajuste anual de SICE-TAC/salario mínimo del EDA-1 (marzo 2026) es relevante aquí: verificar si el histórico SAP (`tblSAPTarifasLog`) muestra el mismo tipo de salto en sus fechas de vigencia — sería una confirmación cruzada entre los dos EDA.

---

## 9. Fase 8 — Análisis geográfico

**Objetivo:** esta fase depende por completo de resolver (o declarar no resuelto) el cruce de rutas §1.2 punto 6.

- Normalizar `NOMBRERUTA` (SICE-TAC, texto libre "ORIGEN - DESTINO") y `catalogo_rutas` (propio, texto libre) contra código DANE — mismo tipo de trabajo que la normalización de ciudades del EDA-1 (`rapidfuzz`), pero aquí el objetivo final es distinto: no es solo detectar variantes del mismo nombre, es **enlazar contra un código DANE real** para poder cruzar contra SAP (que ya trae código DANE/zona).
- Si la normalización no alcanza a resolverse con confianza en el tiempo disponible, **documentarlo como limitación explícita** (ya está anotado en `diccionario-y-linaje.md` como confiabilidad "Media" para esta llave) — no forzar un cruce de baja confianza y presentarlo como hallazgo sólido.
- Top orígenes/destinos por separado en cada fuente (propio, SICE-TAC, SAP) — aunque no se logre el cruce fino, comparar la cobertura geográfica general de las 3 fuentes ya es información útil.

---

## 10. Fase 9 — Outliers y anomalías

- **En SICE-TAC:** los 25 casos `rara_*` ya son, por diseño, los outliers documentados por la fuente — no hay que "descubrirlos", hay que caracterizarlos (§7.3) y no tratarlos como ruido.
- **En SAP:** aplicar IQR/z-score robusto sobre `VALOR`, segmentado por `LINEANEGOCIO`/`TIPOVEHICULO` (misma lógica de segmentar-antes-de-comparar del EDA-1, notebook `06`).
- **En lo propio:** a 13-48 filas, cualquier valor atípico se revisa uno por uno (no hace falta un método estadístico, alcanza con mirar la tabla completa).
- La fila fantasma "------" (§1.4), una vez removida en la carga, no debería volver a aparecer aquí — si aparece, es señal de que la limpieza de la Fase 1 no fue completa.

---

## 11. Fase 10 — Sesgos y representatividad

Preguntas a responder con evidencia (adaptadas del EDA-1 a esta fuente):

1. **Sesgo de volumen — ya declarado por HU-10.3, se hereda.** 13 solicitudes reales no son producción; cualquier conclusión de este EDA sobre "qué tan seguido cotiza bien CabinaFletes" está limitada a esa muestra.
2. **Sesgo de muestreo deliberado en SICE-TAC (§1.5).** La muestra sobre-representa casos extremos a propósito — cualquier "% que tal cosa pasa" calculado sobre el archivo completo (sin separar `nucleo`/`rara_*`) está sesgado y sería un error real, no solo una imprecisión.
3. **Sesgo de cobertura de llaves de cruce.** Si solo ~11% de `lineas_solicitud` tiene `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` poblado (§3.2), cualquier conclusión de "señal de mercado" solo describe ese subconjunto, no el total de líneas cotizadas.
4. **Sesgo temporal de la ventana de SICE-TAC.** Un solo período (agosto 2026) — no se puede saber si ese mes fue típico o atípico del mercado.
5. **Sesgo de `VIGENCIA` sin corregir en SAP.** 95,8% de `tblSAPTarifas` completa está vencida a la fecha de hoy (hallazgo previo HU-09.2) — cualquier análisis que use `VIGENCIA` para filtrar "tarifas activas hoy" debe tener esto en cuenta.
6. **Sesgo de selección en clientes SAP.** Se excluyeron personas naturales (`TYPE='1'`, 91.701 de 130.933) — el catálogo de clientes de esta HU es solo organizaciones, coherente con el propósito (contexto de mercado B2B), pero hay que declararlo.

---

## 12. Fase 11 — Vacíos y limitaciones

**Objetivo:** igual que el EDA-1 — dejar por escrito qué preguntas este cruce **no** puede responder.

### 12.1 Vacíos ya conocidos, heredados de HU-10.2/10.3 (no se re-descubren, se documentan)

| Vacío | Qué impide analizar |
| :---- | :---- |
| Sin `AjusteVigenciaPlaza` aplicado | No se puede saber, solo con este extracto, qué tarifa SAP estaba realmente vigente en la práctica en cada momento |
| Sin normalización de ruta → DANE | Cruce geográfico fino entre las 3 fuentes queda limitado o no resuelto (ver §9) |
| Ambigüedad de "tarifa vigente" (SICE-TAC y SAP) | No hay una tarifa única por ruta — cualquier comparación puntual necesita un criterio de desempate explícito, documentado, no implícito |
| `tblSAPTarifasLog`: ¿tarifa ganada o auditoría? | No se puede usar como proxy de "qué tarifa se aplicó de verdad" sin esa confirmación |
| Volumen real bajo (13 solicitudes) | Ninguna conclusión de este EDA es generalizable a producción real |
| SICE-TAC de un solo período | No hay análisis de tendencia temporal posible sobre el piso SICE-TAC con este archivo |

### 12.2 Formato del entregable

Misma tabla que el EDA-1 en `reportes/limitaciones.md` (ID, pregunta, causa, tipo, qué se necesitaría) — con una columna adicional marcando si la limitación ya estaba declarada por HU-10.3 o es nueva de este EDA.

---

## 13. Fase 12 — Síntesis, documentación y cierre

### 13.1 Informe de hallazgos

Misma ficha estándar que el EDA-1 (ID, Título, Dimensión, Evidencia, Ejemplo, Hipótesis, Verificación, Severidad, Impacto, Recomendación). Candidatos ya visibles desde este plan, a confirmar con el notebook:

- H-001: fila fantasma tipo "------" en al menos 3 (verificar en 17) archivos.
- H-002: cobertura real de las llaves `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` (cuantificar el ~11% preliminar).
- H-003: reconfirmación (o matiz nuevo) del hallazgo HU-10.3 de que `Fuente=Sicetac`/`Tarifario` coincide con la fuente externa.
- H-004: caracterización del mercado SICE-TAC/SAP por segmento, independiente del cruce con lo propio.
- H-005: el sesgo de muestreo deliberado de SICE-TAC, y su efecto si se ignora.

### 13.2 Log de supuestos

Igual formato que el EDA-1 — toda decisión ante ambigüedad (ej. qué criterio de desempate se usó ante múltiples tarifas vigentes) con su justificación y reversibilidad.

### 13.3 Comparativa con propuesta de modelo(s) — entregable de la HU, no de este notebook

El alcance de la HU pide, además de los dos EDA, una **comparativa con propuesta de modelo(s)** (sin construir ningún modelo este sprint). Ese documento vive aparte (no dentro de este plan ni de `03-eda-extraccion-4-fuentes/`) y toma como insumo los hallazgos de **ambos** EDA — se aborda una vez este EDA-2 esté cerrado.

### 13.4 Verificación final antes del PR

Idéntica al EDA-1 (`Restart & Run All` en limpio, números spot-check, sin rutas absolutas ni credenciales, gráficos con título/ejes/unidades, README actualizado, revisión por un par, las 3 condiciones del criterio de cierre marcadas).

---

## 14. Riesgos de este EDA

| ID | Riesgo | Mitigación |
| :---- | :---- | :---- |
| R-01 | La cobertura real de cruce (llaves de negocio, ruta→DANE) resulta tan baja que "la señal de mercado" se reduce a un puñado de casos | Cuantificar temprano (Fase 2, §3.2) y ajustar expectativas del entregable en consecuencia — no forzar un análisis estadístico sobre 3-5 filas |
| R-02 | Tratar la muestra de SICE-TAC como representativa sin filtrar `Muestra=="nucleo"` primero | Regla explícita desde §1.5, aplicada en la carga (Fase 1), no al final |
| R-03 | Confundir `tblSAPTarifasLog` con "tarifa realmente aplicada" sin la confirmación de negocio pendiente | Tratar como fuente de frecuencia de uso (proxy ya usado en HU-09.2), no como hecho consumado |
| R-04 | Que la fila fantasma "------" no se detecte en algún archivo por no revisar los 17 completos | Checklist explícito de los 17 archivos en la Fase 1, no solo los 3 ya detectados |
| R-05 | El negocio no confirma a tiempo las preguntas abiertas de §1.2 | Entregar con las limitaciones marcadas como "no resuelto por falta de definición", igual criterio que el EDA-1 (R-05 de ese plan) |

---

**Supuestos de este plan:** mismo stack que el EDA-1 (Python: pandas, matplotlib/seaborn, Jupyter) y repo Git — se reutiliza `requirements.txt` con las mismas versiones para que ambos EDA sean directamente comparables, tal como pide el criterio de cierre de esta HU.
