# Hallazgos del EDA — Viajes Buenaventura 2025-2026

Cada hallazgo con su ficha estándar (§13.1 del plan), seguido del log de supuestos y las recomendaciones al negocio (§13.2-13.3). Ver `limitaciones.md` para lo que este set no puede responder, y el notebook `07_sintesis_y_limitaciones.ipynb` para las celdas que reproducen los números marcados sin 🔎.

**Nota de trazabilidad:** los hallazgos marcados 🔎 se verificaron contra la base de datos operativa de la empresa (`tblSAPTarifas`, `FactFWOFUFO`, vía DBeaver) — no son reproducibles desde una celda del notebook, la evidencia es la consulta SQL documentada aquí mismo.

## H-001
```
ID:            H-001
Título:        FO no es clave única — carga consolidada de múltiples clientes en un mismo viaje
Dimensión:     Unicidad / Granularidad
Evidencia:     128 valores de FO repetidos sobre 41.107 únicos (130 filas de más). Sobre los 128 grupos completos: 100% comparte Vehículo y Fecha Creación FO, difieren en BP Cliente y/o Peso Neto.
Ejemplo:       FO 301205267 — cliente distinto pero tarifa idéntica entre ambas filas (caso sin resolver del todo)
Hipótesis:     FO identifica el viaje del vehículo, no la orden individual de un cliente — un viaje puede consolidar carga de varios clientes.
Verificación:  Confirmado contra FactFWOFUFO (jerarquía FO/FWO/FU): a escala de toda la compañía, en 90.285 viajes con más de un FWO, la distancia varía en solo 0,73% (esperado a nivel FO) vs. el origen que varía en 5,35% (esperado a nivel FWOP).
Severidad:     Alta
Impacto:       Cualquier conteo de "número de viajes" o métrica de ingreso que no sume por FO+cliente está mal calculada.
Recomendación: Usar clave compuesta FO+BP Cliente+Peso Neto+Destino para el grano real; sumar (no deduplicar) tarifa/flete por fila para métricas de ingreso — pendiente de confirmar con negocio.
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

---

## Log de supuestos

Toda decisión tomada ante ambigüedad en este EDA, con su justificación y qué tan reversible es.

| # | Decisión | Justificación | Reversibilidad |
|---|---|---|---|
| 1 | Nombres de columna en camelCase en el código | Decisión de equipo (notebook `00`), no afecta el contenido del dato | Alta — solo convención de código |
| 2 | Clave práctica `FO`+`BP Cliente`+`Peso Neto`+`Destino` para el grano | Única sobre las 41.237 filas, pero no confirmada formalmente con el negocio | Media — se reemplaza si el negocio define el grano oficialmente |
| 3 | `Origen = Destino` tratado como segmento aparte, no como error | Explicado por descontenerización (88,7% de los casos) | Media — depende de confirmación de negocio en los 978 casos residuales |
| 4 | El EDA y el futuro modelo se enfocan en `Flete Producción FO`, no en `Tarifa`/margen | Aclarado directamente por el negocio en esta sesión | Baja — es una decisión de alcance del negocio, no una suposición nuestra |
| 5 | Valores "casi-cero" (tarifa=$1, peso=1kg) tratados como basura y excluidos de cálculos puntuales | Mismo criterio que los ceros exactos, evita distorsionar estadísticos | Alta — se puede recalcular sin excluirlos si se prefiere |
| 6 | Clasificación de outliers (error_dato / caso_legítimo / por_confirmar) | Basada en evidencia cruzada contra la base de datos operativa cuando fue posible | Media — algunos casos (`por_confirmar`) dependen de respuesta del negocio |
| 7 | Hipótesis SICE-TAC/salario mínimo para el salto de flete de marzo 2026 | Evidencia externa (decretos, resoluciones) + interna (`tblSAPTarifas`) fuertemente alineada en el tiempo | Media — no confirmada documentalmente con el proceso interno de la empresa |

## Recomendaciones al negocio

**(a) Campos a agregar en la próxima extracción:**
- Fecha/hora de salida, llegada y entrega real (no solo creación administrativa del FO).
- Estado del viaje (para no depender de cruzar contra `FactFWOFUFO`).
- Costo real de operación (combustible, peajes, mantenimiento).
- `BP Conductor FO` (ya existe en la fuente, no se trajo a este extracto).
- Coordenadas, o al menos la zona de tarificación explícita (`tblZonasTarificacionRelacion`).
- Número de contrato/tarifario aplicado por viaje.
- Unidad de `Peso Neto` confirmada explícitamente (kg vs t).

**(b) Reglas de validación a implementar en origen:**
- Alertar cuando `Tarifa Venta al Cliente` = `Flete Producción FO` exactamente (posible default no actualizado, hoy 72,2% del set).
- Alertar cuando el flete exceda el techo de `tblSAPTarifas` para esa ruta (hubiera evitado el error de $46,8M).
- Corregir el registro de `Clase Equipo FOP = CNT40.HC` mal clasificado bajo grupo `CAM`.
- Revisar por qué `"1"` aparece como `ID Medio Transporte FO` válido (valor basura, 2 registros).

**(c) Definiciones a formalizar en el diccionario:**
- Confirmar si `FO` puede representar carga consolidada de varios clientes (reemplaza la pregunta original sobre si `FO` es clave única).
- Unidad de `Peso Neto` (kg vs t) y moneda/IVA de las columnas monetarias.
- Confirmar las 3 líneas de negocio ausentes del catálogo (`OHUC`, `OPSC`, `OPXL`): sin operación / filtradas en extracción / en otro sistema.
- Confirmar formalmente el criterio de extracción y los estados excluidos (ya tenemos evidencia fuerte: `Concluido`/`Ejecutado` únicamente).
