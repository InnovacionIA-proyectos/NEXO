# Hallazgos del EDA — Viajes Buenaventura 2025-2026

Cada hallazgo con su ficha estándar (§13.1 del plan), seguido del log de supuestos y las recomendaciones al negocio (§13.2-13.3). Ver `limitaciones.md` para lo que este set no puede responder, y el notebook `07_sintesis_y_limitaciones.ipynb` para las celdas que reproducen los números marcados sin 🔎.

**Nota de trazabilidad:** los hallazgos marcados 🔎 se verificaron contra la base de datos operativa de la empresa (`tblSAPTarifas`, `FactFWOFUFO`, vía DBeaver) — no son reproducibles desde una celda del notebook, la evidencia es la consulta SQL documentada aquí mismo.

## H-001
```
ID:            H-001
Título:        FO no es clave única — carga consolidada de múltiples clientes en un mismo viaje (CONFIRMADO)
Dimensión:     Unicidad / Granularidad
Evidencia:     128 valores de FO repetidos sobre 41.107 únicos (130 filas de más). Sobre los 128 grupos completos: 100% comparte Vehículo y Fecha Creación FO, difieren en BP Cliente y/o Peso Neto. Confirmado a escala de toda la compañía: para FOs con más de un FWO en FactFWOFUFO, el número de clientes distintos casi siempre iguala al número de FWO (nunca lo supera) — cada FWO adicional es casi siempre un cliente distinto.
Ejemplo:       FO 301205267 — cliente distinto pero tarifa idéntica entre ambas filas (caso sin resolver del todo)
Hipótesis:     N/A — confirmado directamente, ya no es hipótesis.
Verificación:  Confirmado contra FactFWOFUFO (jerarquía FO/FWO/FU) en dos pasadas: (1) a nivel viaje, la distancia varía en solo 0,73% de 90.285 viajes con más de un FWO (esperado a nivel FO) vs. el origen que varía en 5,35% (esperado a nivel FWOP); (2) consulta directa FO↔FWO↔cliente confirma que cada FWO adicional casi siempre corresponde a un cliente distinto.
Severidad:     Alta
Impacto:       Cualquier conteo de "número de viajes" o métrica de ingreso que no sume por FO+cliente está mal calculada.
Recomendación: Usar clave compuesta FO+BP Cliente+Peso Neto+Destino para el grano real; sumar (no deduplicar) tarifa/flete por fila para métricas de ingreso.
```

## H-002
```
ID:            H-002
Título:        Tarifa Venta al Cliente no es confiable — 72,2% de las filas idénticas a Flete Producción FO
Dimensión:     Calidad / Exactitud
Evidencia:     En 72,22% de las filas válidas (tarifa/flete > 0), Tarifa Venta al Cliente = Flete Producción FO exactamente, en columnas con miles de valores únicos posibles (imposible por azar).
Ejemplo:       Ver notebook 04, Paso 6, para el listado completo de filas afectadas.
Hipótesis:     Mal capturada desde los sistemas de origen en este extracto específico.
Verificación:  Confirmado directamente por el negocio — posible que se capture bien en otra tabla, no en esta.
Severidad:     Alta
Impacto:       Descarta cualquier análisis de margen/rentabilidad calculado sobre este set.
Recomendación: No usar esta columna para rentabilidad; identificar la tabla fuente correcta si se necesita después. El modelo predictivo debe enfocarse en Flete Producción FO, no en tarifa/margen.
```

## H-003 🔎
```
ID:            H-003
Título:        Salto de +16,7% en el flete mediano en marzo 2026, coincide con salario mínimo/SICE-TAC
Dimensión:     Temporal / Exactitud
Evidencia:     Flete mediano estable (~$2,0M) abril-noviembre 2025; sube a $2,38M en marzo 2026 (+16,7%, el mayor cambio de la serie). Confirmado parejo en casi todos los vehículos (+2% a +31%) y en Propio (+13%)/Tercero (+15%).
Hipótesis:     Aumento del 23% del salario mínimo colombiano (vigente desde enero 2026), trasladado al cálculo del SICE-TAC (actualizado progresivamente desde enero 2026, formalizado en mayo 2026).
Verificación:  Búsqueda externa (decretos de salario mínimo, resoluciones de MinTransporte) + tblSAPTarifas muestra un pico de tarifas nuevas cargadas en diciembre 2025 (1.618 vs. ~700-900 de meses vecinos).
Severidad:     Media (no es un error — es información de negocio real, pero afecta cualquier análisis de precio en el tiempo)
Impacto:       Cualquier análisis de tendencia de precio debe segmentar antes/después de marzo 2026.
Recomendación: Confirmar con el negocio si el patrón se repite cada año; incluir un componente temporal/anual explícito en el futuro modelo predictivo.
```

## H-004 🔎
```
ID:            H-004
Título:        Sesgo de selección y supervivencia confirmado — el set solo contiene viajes concluidos
Dimensión:     Cobertura / Sesgo
Evidencia:     En FactFWOFUFO, misma ventana de fechas, compañía completa: 75.969 FO Concluido/Ejecutado, 8.034 Anulado, 115.216 En tratamiento.
Hipótesis:     El extracto solo contiene FOs Concluido/Ejecutado.
Verificación:  Consulta directa a FactFWOFUFO vía DBeaver (no verificado con el filtro exacto de Buenaventura, pendiente cruce de ubicación).
Severidad:     Alta
Impacto:       No se puede analizar tasa de cancelación, viajes fallidos, ni el comportamiento de viajes en curso — el set solo habla de "éxitos".
Recomendación: Si se necesita analizar cancelaciones/fallas, solicitar una extracción adicional con todos los estados.
```

## H-005 🔎
```
ID:            H-005
Título:        Error de dato confirmado — flete de $39,6M-$46,8M en Buenaventura→Cota
Dimensión:     Exactitud / Outlier
Evidencia:     5 FO (301268956, 301269826, 301266660, 301267947, 301267211) con flete entre $39,6M y $46,8M, distancia 521,001 km (valor no redondo, atípico).
Ejemplo:       FO 301268956 — $46.800.000, 521,001 km, peso 11.752 kg.
Hipótesis:     Error de captura (cero de más o decimal corrido).
Verificación:  Confirmado contra tblSAPTarifas — la tarifa oficial legítima para esa ruta (todos los pesos/vehículos, incluida carga plena) va de $1,1M a $7,4M, nunca la supera.
Severidad:     Alta (magnitud), baja en volumen (5 registros)
Impacto:       Infla cualquier estadístico de flete que no excluya outliers.
Recomendación: Reportar estos 5 FO al negocio para corrección; excluir de análisis hasta que se corrijan.
```

## H-006 🔎
```
ID:            H-006
Título:        $800.000 es un valor de relleno del sistema, no una tarifa mínima real
Dimensión:     Calidad / Outlier
Evidencia:     Aparece como VALOR en decenas de combinaciones ruta+vehículo+peso sin relación entre sí en tblSAPTarifas (desde 0 kg hasta 34 toneladas), en rutas poco frecuentes, con vigencias sin patrón entre 2024 y 2026.
Ejemplo:       6 viajes TC_CAMNETA en el extracto con flete exacto de $800.000 en distancias de 118 a 505 km (notebook 06, Paso 2).
Hipótesis:     Valor por defecto para combinaciones sin tarifa configurada.
Verificación:  Confirmado consultando tblSAPTarifas directamente.
Severidad:     Media
Impacto:       Cualquier análisis que trate estos valores como tarifas reales calculadas distorsiona el flete de esas rutas/vehículos.
Recomendación: Marcar estos registros como "valor de sistema no confiable", no como caso legítimo ni error de captura.
```

## H-007
```
ID:            H-007
Título:        De 5 columnas de vehículo/equipo, solo hay 2 dimensiones reales de información
Dimensión:     Redundancia / Consistencia
Evidencia:     V de Cramér entre Vehículo, Tipo, ID Medio Transporte FO, Clase Equipo FOP, Grupo de Equipo FWOP. Vehículo↔Tipo = 1,00 (verificado directo: 0 de 3.418 vehículos con más de un Tipo). Clase Equipo FOP↔Grupo de Equipo FWOP = 1,00 (jerarquía confirmada con mapa completo).
Ejemplo:       Notebook 03, Paso 7.
Hipótesis:     N/A — confirmado, no es una sospecha.
Verificación:  Verificación directa contra los datos (conteo de vehículos con más de un tipo) y mapa completo grupo↔clase obtenido de la base operativa.
Severidad:     Baja (no es un defecto, es una oportunidad de simplificación)
Impacto:       Usar las 5 columnas como variables independientes en un modelo introduce redundancia sin aportar información nueva.
Recomendación: Usar ID Medio Transporte FO (tipo de camión) + Clase Equipo FOP (tipo de equipo/contenedor) como las 2 dimensiones reales; Vehículo como identificador aparte, no como categoría; descartar Tipo. Ya aplicado en notebooks 04-06.
```

## H-008 🔎
```
ID:            H-008
Título:        Las ciudades de origen/destino se agrupan en "zonas de tarificación" no visibles en el extracto
Dimensión:     Columna faltante / Cobertura
Evidencia:     tblZonasTarificacionRelacion (BD operativa) agrupa decenas de municipios satélite bajo zonas amplias (ej. ZTC_BOGOTA incluye Bogotá, Funza, Madrid, Mosquera, Cota; ZTC_VABURRA incluye Medellín, Bello, Envigado, Itagüí) — coincide exactamente con las ciudades satélite repetidas en el top de orígenes/destinos del notebook 05.
Ejemplo:       Notebook 05 (top de ciudades) comparado contra la consulta directa a tblZonasTarificacionRelacion.
Hipótesis:     N/A — confirmado.
Verificación:  Consulta directa a la base de datos operativa vía DBeaver.
Severidad:     Media
Impacto:       Tratar cada ciudad como categoría independiente infla la cardinalidad sin necesidad — la señal real de precio/ruta vive a nivel de zona, no de municipio.
Recomendación: Para el futuro modelo, mapear cada ciudad a su zona de tarificación (reduce ~113/122 ciudades a ~15-20 zonas) en vez de usar el nombre de ciudad crudo — requiere obtener la tabla completa de mapeo ciudad→zona.
```

## H-009
```
ID:            H-009
Título:        21,7% de Origen=Destino, mayoritariamente legítimo (descontenerización), con 978 casos residuales sospechosos
Dimensión:     Exactitud / Plausibilidad
Evidencia:     8.966 registros con Origen=Destino (21,74%). OHUB+OH3P (descontenerización) explican el 88,7% de esos casos, casi universal dentro de esas clases. 978 registros OPIM+OPTN (2,4% del set) contradicen la definición de su propia clase.
Ejemplo:       974 de 978 tienen Distancia Total=10 (la misma "firma" de descontenerización); 100% de los 978 tienen Origen=BUENAVENTURA.
Hipótesis:     La mayoría de los 978 son descontenerización real mal etiquetada como OPIM/OPTN en vez de OHUB/OH3P.
Verificación:  Cruce contra Distancia Total y contra el catálogo CLASE FWO; hipótesis alternativa (viajes intraurbanos legítimos) descartada porque el 100% tiene a Buenaventura como origen.
Severidad:     Media (defecto acotado, no todo el 21,7%)
Impacto:       Sin este refinamiento se sobreestima el defecto real (parecía ser 8.966, en realidad son 978, y de esos solo 4 ameritan revisión individual real).
Recomendación: Segmentar y excluir de análisis punto a punto todo Origen=Destino; de los 978 residuales, solo 4 (distancias 521, 521, 233,5 y 505 km) ameritan revisión individual.
```

## H-010
```
ID:            H-010
Título:        Concentración de clientes baja (HHI=331) pese a que el top 10 concentra 39,7% del volumen
Dimensión:     Sesgo / Representatividad
Evidencia:     Top 10 clientes = 39,7% del volumen; HHI = 331 (umbral de "no concentrado" es <1.500). Cliente más grande (580) tiene 14,68%; el resto se reparte entre 537 clientes en total.
Ejemplo:       Notebook 06, Paso 4.
Hipótesis:     N/A — confirmado con cálculo directo.
Verificación:  Cálculo directo sobre el dataset.
Severidad:     Baja (buena noticia, no un problema)
Impacto:       Las conclusiones generales del EDA no están sesgadas por 2-3 clientes dominantes — reflejan un negocio con base amplia.
Recomendación: Ninguna acción requerida; documentar como evidencia de representatividad.
```

## H-011
```
ID:            H-011
Título:        Distancia Total es un valor planificado/de tabla, no una medición real del viaje
Dimensión:     Calidad / Exactitud
Evidencia:     Varios pares origen-destino con volumen alto (127-316 viajes) registran exactamente la misma distancia en el 100% de sus viajes (desviación estándar = 0,0) — ej. Barbosa→Buenaventura siempre 512,0 km; Bello→Buenaventura siempre 470,0 km.
Ejemplo:       Notebook 05, Paso 7.
Hipótesis:     N/A — confirmado.
Verificación:  Verificación directa (cálculo de desviación estándar por par O-D sobre múltiples pares).
Severidad:     Media
Impacto:       Resuelve una pregunta abierta del plan (§1.2, punto 6); Distancia Total no sirve para verificar la ruta física real recorrida, solo como referencia de tarifa/zona.
Recomendación: No usar Distancia Total como proxy de "kilómetros realmente recorridos" en análisis operativos (ej. consumo de combustible, desgaste de flota); sí es válida como variable de tarifa/ruta para el modelo predictivo de flete.
```

## H-012 🔎
```
ID:            H-012
Título:        Flete negativo (-$49.978) es un descuento operacional real, no un error de signo
Dimensión:     Exactitud / Outlier
Evidencia:     FO 301291984, único registro con Flete Producción FO negativo en todo el extracto. La fila completa en FactFWOFUFO muestra un campo explícito Importe Descuento Operacional FO = -2.300.000 COP.
Ejemplo:       FO 301291984 — Clase Expedición FWO = "LTL (carga incompleta del camión)", 467 kg, TC_CAMNETA, Buenaventura→Guarne.
Hipótesis:     N/A — confirmado.
Verificación:  Consulta directa a FactFWOFUFO, fila completa del FO.
Severidad:     Baja (caso único, ya explicado)
Impacto:       Ninguno adicional para el EDA — es un ajuste de negocio legítimo, no requiere corrección ni exclusión del análisis descriptivo. Para el futuro modelo predictivo sí es relevante: es el único valor negativo de la variable objetivo, y una transformación logarítmica del flete (necesaria por las colas largas, ver notebook 04) no admite valores negativos.
Recomendación: Reclasificar de "por_confirmar" a "caso_legítimo" en cualquier análisis de outliers (ya reclasificado aquí). Para el modelo predictivo de Flete Producción FO: excluir esta fila (FO 301291984) del set de entrenamiento, documentando el motivo explícitamente (descuento operacional aplicado, no error) — no descartarla en silencio.
```

## H-013 🔎

```
ID:            H-013
Título:        "1" en ID Medio Transporte FO es un valor de relleno del sistema ("0001"), no un código de vehículo real — confirmado a escala de toda la compañía
Dimensión:     Calidad / Exactitud
Evidencia:     2 registros del extracto (FO 301231729, FO 301358477) con idMedioTransporteFo = "1". Consultados directamente en FactFWOFUFO: el valor real es "0001" (texto), no "1" — la exportación a Excel lo leyó como número y perdió los ceros a la izquierda. A escala de toda la compañía, "0001" aparece en 1.114 registros de FactFWOFUFO (~0,56% de los ~199.219 FO de la ventana, ver H-004), asociado a decenas de combinaciones incompatibles de Clase Equipo FOP/Grupo de Equipo FWOP — camiones chicos, camiones de 34 toneladas, contenedores de 20 y 40 pies, en ambos grupos CAM y CN — imposible si fuera el código real de un tipo de vehículo específico.
Ejemplo:       FO 301231729 (Clase Equipo FOP=CNT20.ST, Grupo=CN) y FO 301358477 (Clase Equipo FOP=C2.8T, Grupo=CAM) — mismo "0001" en ambos, con configuraciones de equipo totalmente distintas.
Hipótesis:     N/A — confirmado en dos partes: (1) el "1" del extracto es un bug de exportación (conversión numérica que quita los ceros a la izquierda de "0001"); (2) "0001" en sí es un valor de relleno/placeholder del sistema origen, no un código de vehículo real. (Una primera hipótesis de esta misma investigación — inferir el valor real cruzando con `Tipo`, ej. TC_TURBO/TC_SENCI — se descartó al consultar FactFWOFUFO directamente: no aplica, el valor real ya era "0001").
Verificación:  Consulta directa a FactFWOFUFO por los 2 FO del extracto, y conteo a escala de toda la compañía (WHERE "ID Medio Transporte FO" = '0001') — 2026-09-14.
Severidad:     Baja para este extracto (2 de 41.237 filas, 0,005%); a vigilar si se amplía el alcance geográfico (1.114 de ~199.219 FO a nivel compañía, ~0,56%)
Impacto:       No se puede recuperar el tipo de vehículo real de estas filas con la información disponible — cualquier intento de inferirlo (ej. por `Tipo`) no es confiable.
Recomendación: Para el futuro modelo, no eliminar estas filas (el resto de sus datos — peso, distancia, ruta, flete — sigue siendo válido) ni inventar un valor — codificar `ID Medio Transporte FO` como categoría explícita "sin_clasificar"/"desconocido" en vez de un tipo de vehículo real. Reportar al negocio como hallazgo de escala de compañía (1.114 registros), no como anécdota local de Buenaventura.
```

---

## Log de supuestos

Toda decisión tomada ante ambigüedad en este EDA, con su justificación y qué tan reversible es.

| # | Decisión | Justificación | Reversibilidad |
|---|---|---|---|
| 1 | Nombres de columna en camelCase en el código | Decisión de equipo (notebook `00`), no afecta el contenido del dato | Alta — solo convención de código |
| 2 | Clave práctica `FO`+`BP Cliente`+`Peso Neto`+`Destino` para el grano | Única sobre las 41.237 filas; la hipótesis de carga consolidada que la sustenta ya se confirmó directamente en `FactFWOFUFO` (H-001) | Baja — confirmada con evidencia directa, ya no depende de que el negocio la valide |
| 3 | `Origen = Destino` tratado como segmento aparte, no como error | Explicado por descontenerización (88,7% de los casos) | Media — depende de confirmación de negocio en los 978 casos residuales |
| 4 | El EDA y el futuro modelo se enfocan en `Flete Producción FO`, no en `Tarifa`/margen | Aclarado directamente por el negocio en esta sesión | Baja — es una decisión de alcance del negocio, no una suposición nuestra |
| 5 | Valores "casi-cero" (tarifa=$1, peso=1kg) tratados como basura y excluidos de cálculos puntuales | Mismo criterio que los ceros exactos, evita distorsionar estadísticos | Alta — se puede recalcular sin excluirlos si se prefiere |
| 6 | Clasificación de outliers (error_dato / caso_legítimo / por_confirmar) | Basada en evidencia cruzada contra la base de datos operativa cuando fue posible | Media — algunos casos (`por_confirmar`) dependen de respuesta del negocio |
| 7 | Hipótesis SICE-TAC/salario mínimo para el salto de flete de marzo 2026 | Evidencia externa (decretos, resoluciones) + interna (`tblSAPTarifas`) fuertemente alineada en el tiempo | Media — no confirmada documentalmente con el proceso interno de la empresa |

## Recomendaciones al negocio

**(a) Campos a agregar en la próxima extracción** — fuente de cada uno, verificada 2026-09-14 contra el inventario completo de columnas de `FactFWOFUFO` y el código del backend de Nexo:

| Campo | Estado | Dónde se consigue |
|---|---|---|
| Fecha/hora de salida, llegada y entrega real (no solo creación administrativa del FO) | ✅ Localizada | `FactFWOFUFO`: `Fecha Hora Recogida FWOP`, `Fecha Hora Entrega FWOP`, `Fecha Inicio Cargue`/`Fecha Fin Cargue`, `Fecha Inicio Descargue`/`Fecha Fin Descargue`, `Fecha Llegada Cliente`/`Fecha Descargue Cliente`, `Fecha Hora Último Descargue`. Solo falta incluirlas en el extracto |
| Estado del viaje (para no depender de cruzar contra `FactFWOFUFO`) | ✅ Localizada | `FactFWOFUFO`: `Estado Ejecución FO`/`FU`/`FWO` y `Estado Ciclo Vida FO`/`FU`/`FWO` (ya usadas para verificar H-004 por consulta aparte; falta incluirlas directamente en el extracto) |
| `BP Conductor FO` (ya existe en la fuente, no se trajo a este extracto) | ✅ Confirmada | Columna `BP Conductor FO`, presente tal cual en `FactFWOFUFO` |
| Coordenadas, o al menos la zona de tarificación explícita | ✅ Confirmada (H-008) | Tabla `tblZonasTarificacionRelacion`, en la base operativa de SAP (no en `DWCubos`/`FactFWOFUFO`). Pista sin confirmar: `FactFWOFUFO.Ruta FO` y `.Corredor` podrían ya traer la ruta resuelta o el corredor comercial — pendiente inspeccionar valores de muestra |
| Unidad de `Peso Neto` confirmada explícitamente (kg vs t) | ✅ Ya confirmada = KG | Dato directo de `FactFWOFUFO` (ver inciso (c) abajo) — falta que la próxima extracción la declare explícitamente desde el inicio, sin depender de ese cruce puntual |
| Número de contrato/tarifario aplicado por viaje | ❌ No localizada como campo directo | Revisado el inventario completo de `FactFWOFUFO`: no hay `IdTarifa`/`NumContrato` explícito (`Importe Contrato Vinculación FWO` es otro concepto — el contrato de vinculación del vehículo, no la tarifa aplicada). 3 alternativas, de más a menos directa: (1) reconstruirlo por coincidencia de valor cruzando cada viaje contra `tblSAPTarifas`/`tblSAPTarifasLog` por ruta+vehículo+vigencia+valor exacto — método ya validado en EDA-2 (H-005/H-007, 32/32 líneas resueltas así); (2) preguntar a IT/SAP si el sistema transaccional (no el cubo) guarda un ID de condición de tarifa no replicado al cubo; (3) si nada resuelve, usar un proxy (cercanía del flete pagado al rango legítimo de `tblSAPTarifas`) en vez del ID exacto |

**(b) Reglas de validación a implementar en origen:**
- Alertar cuando `Tarifa Venta al Cliente` = `Flete Producción FO` exactamente (posible default no actualizado, hoy 72,2% del set).
- Alertar cuando el flete exceda el techo de `tblSAPTarifas` para esa ruta (hubiera evitado el error de $46,8M).
- Corregir el registro de `Clase Equipo FOP = CNT40.HC` mal clasificado bajo grupo `CAM`.
- Por qué `"1"` aparece como `ID Medio Transporte FO` válido — **resuelto** (H-013): es el valor de relleno `"0001"`, no un código de vehículo real; afecta 1.114 registros a nivel compañía (2 en este extracto). Alertar/corregir en origen cuando `ID Medio Transporte FO = "0001"`.

**(c) Definiciones a formalizar en el diccionario:**
- Confirmar si `FO` puede representar carga consolidada de varios clientes — **confirmado** (H-001), ya no es pregunta abierta.
- Unidad de `Peso Neto` — **confirmada = KG** (dato directo de `FactFWOFUFO`). Moneda de las columnas monetarias — **confirmada = COP**. Sigue abierto solo si los montos incluyen IVA o no.
- Las 3 líneas de negocio del catálogo (`OHUC`, `OPSC`, `OPXL`) **sí existen y se usan a nivel de toda la compañía** (`OHUC`: 1.028 registros, `OPSC`: 29.646, `OPXL`: 4 — verificado en `FactFWOFUFO`, sin filtro de ubicación). La pregunta ya no es "¿existen?" sino **¿por qué no aparecen en el corredor de Buenaventura en esta ventana?** — probable especialización por puerto (ej. la contenerización `OHUC` podría concentrarse en puertos del Caribe, no en Buenaventura, que es predominantemente descontenerización). Pendiente confirmar con negocio.
- Confirmar formalmente el criterio de extracción y los estados excluidos (ya tenemos evidencia fuerte: `Concluido`/`Ejecutado` únicamente).
