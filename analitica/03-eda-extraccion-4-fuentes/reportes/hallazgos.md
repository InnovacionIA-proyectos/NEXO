# Hallazgos del EDA — Extracción que cruza las 4 fuentes (S2-HU-11 parte 2)

Cada hallazgo con su ficha estándar (§13.1 del plan), seguido del log de supuestos. Ver `limitaciones.md` para lo que este set no puede responder. Los números marcados 🔎 se verificaron contra el código fuente del backend (`src/`) o contra la base de datos operativa, no son reproducibles desde una celda del notebook — la evidencia es la consulta/archivo documentado aquí.

## H-001

```
ID:            H-001
Título:        Fila fantasma tipo "------" en 14 de los 17 archivos crudos, justo después del encabezado
Dimensión:     Validez / Artefacto de exportación
Evidencia:     14 de 17 archivos traen una fila extra de valores tipo "--", "---------", etc., como primera fila de datos (no al final, como sugería el hallazgo preliminar del plan). Parece una fila separadora de tabla Markdown exportada por error.
Ejemplo:       solicitudes.csv, línea 2: "--,---------,----,---------,...".
Hipótesis:     N/A — confirmado y corregido en el notebook 00.
Verificación:  Conteo de filas leídas vs. documentado en el acta de entrega (README de v1.0.0-piloto), en los 17 archivos.
Severidad:     Media
Impacto:       Si no se excluye, se analiza como si fuera un registro real de cada tabla.
Recomendación: Ya corregido en este EDA (excluida en la carga). Reportar al equipo de extracción para que la exportación futura no la incluya.
```

## H-002

```
ID:            H-002
Título:        Varios campos numéricos del lado propio no tienen relación con la realidad — confirmado por el usuario
Dimensión:     Exactitud / Confiabilidad de origen
Evidencia:     3 líneas de cotizacion_lineas con Fuente=Manual (incluye $45.555 y $4.500.000.000, ambos absurdos); ValorMercancia (2 de $12 y $27 entre 38 valores); Peso (lineas_solicitud). El usuario confirmó que los escribió al azar durante pruebas manuales, sin ser experto en precios de flete.
Ejemplo:       cotizacion_lineas.id=9104DC9A-... ($4.500.000.000, Fuente=Manual, Estado=Entregada).
Hipótesis:     N/A — confirmado directamente por el usuario.
Verificación:  Confirmación directa del usuario en esta sesión.
Severidad:     Alta
Impacto:       Ningún estadístico (promedio, mediana, distribución) sobre estos 3 campos representa una magnitud de negocio real. A diferencia de ValorFlete (que sí tiene columna Fuente para separar lo manual), ValorMercancia/Peso no tienen forma de separar programáticamente qué valores son ruido.
Recomendación: Excluir las 3 líneas Fuente=Manual de cualquier análisis de ValorFlete (ya hecho en notebook 04). Declarar ValorMercancia/Peso como de confiabilidad limitada en cualquier reporte, sin intentar limpiarlos con precisión.
```

## H-003 🔎

```
ID:            H-003
Título:        LineaNegocio/ClaseViaje/ClaseContenedor no son "la misma llave" entre lado propio y SAP — requieren traducción
Dimensión:     Consistencia / Cobertura de cruce
Evidencia:     lineas_solicitud guarda texto ("Andino", "Contenedor"), SAP guarda código numérico ("7", "1"). Comparación directa de texto = 0% de match. La traducción correcta existe como diccionario fijo en el backend (TarifarioSapAdapter.cs): LineaNegocio {Andino=7, DevolucionContenedor=6, Nacional=4, PuertoAInterior=1, InteriorAPuerto=2, Licitaciones=8}; ClaseViaje {Contenedor=1, Cupo=2, CargaSuelta=4, Extradimensionado=5}. Con la traducción, ClaseViaje sube a 100% (5/5) y LineaNegocio a 25% (1/4).
Ejemplo:       lineas_solicitud con LineaNegocio="Andino" vs. tarifa-plaza.lineanegocio="7".
Hipótesis:     N/A — confirmado en el código fuente del backend.
Verificación:  Lectura directa de src/CabinaFletes.Infrastructure/Biblioteca/Adaptadores/TarifarioSapAdapter.cs.
Severidad:     Alta
Impacto:       diccionario-y-linaje.md (HU-10.3) afirma que estas columnas son "la misma llave" — en la práctica es incorrecto/incompleto sin esta traducción. ClaseContenedor no tiene mapeo fijo: depende de la tabla tblSAPClaseContenedor, que no está en ninguno de los 4 extractos — no verificable con este set.
Recomendación: Corregir diccionario-y-linaje.md para aclarar que se necesita traducción. Incluir tblSAPClaseContenedor en cualquier extracción futura.
```

## H-004 🔎

```
ID:            H-004
Título:        Ruta es la llave de cruce dominante (no los códigos de negocio) — confirmado por el usuario; requiere 2 tablas ausentes para reproducir el mecanismo real
Dimensión:     Consistencia / Cobertura de cruce
Evidencia:     El usuario confirmó que el motor real de costeo filtra principalmente por ruta (ciudad origen-destino), con los códigos de negocio como secundarios. Verificado en el backend: TarifarioSapAdapter.BuscarConZonasAsync resuelve ciudad→zona SAP vía 2 catálogos exactos — tblsapdane (código DANE) y, si falla, tblZonasTarificacionRelacion (zona ZC_/ZTC_) — ninguno presente en los 4 extractos externos. Para SICE-TAC, el backend sí hace lo mismo que aproximamos (substring de ciudad contra NombreRuta).
Ejemplo:       Bogotá se resuelve a ZTC_BOGOTA (zona metropolitana), no a un código individual — solo descubrible consultando tblZonasTarificacionRelacion.
Hipótesis:     N/A — confirmado en código fuente + confirmación de negocio.
Verificación:  Lectura de TarifarioSapAdapter.cs + confirmación directa del usuario.
Severidad:     Alta
Impacto:       Reordena la prioridad de todo el cruce de este EDA — Ruta, no LineaNegocio/ClaseViaje, es la pieza que más importa. Sin tblsapdane/tblZonasTarificacionRelacion no se puede reproducir el mecanismo real de matching, solo aproximarlo.
Recomendación: Pedir tblsapdane y tblZonasTarificacionRelacion explícitamente en la extracción nueva para el modelo, junto con tblSAPClaseContenedor (H-003).
```

## H-005

```
ID:            H-005
Título:        El hallazgo central de la HU: CabinaFletes cotiza sistemáticamente ~30% por encima de la mediana SAP; contra SICE-TAC el cruce fino no es posible con este extracto
Dimensión:     Exactitud / Cruce bivariado (§7.1/§7.2 del plan)
Evidencia:     Con la ruta ya normalizada (ver H-004), se cruzaron las 32 líneas Fuente=Tarifario contra tarifa-plaza por ruta (excluyendo las tarifas de "Devolución Contenedor", que son reposicionamiento de vacío, no carga real): las 32 caen dentro del rango [mín,máx] de SAP para su ruta, con mediana de diferencia de +30,6% por encima de la mediana SAP (hasta +57% en Bogotá-Barranquilla; -31,6% en Cali-Bogotá, la única por debajo). Para Sicetac, un primer intento reportó 5/12 líneas con candidato en piso-sicetac (incluyendo un llamativo "-54% por debajo del piso legal" en Bogotá→Medellín) — revisado y corregido: el único candidato usado para las dos direcciones era una fila `condicioncarga="VACIO"` (tarifa de vehículo vacío, no de carga real) y la búsqueda no distinguía sentido de ruta. Corregido esto (exigir `condicioncarga="CARGADO"` y respetar "ORIGEN - DESTINO" en el texto), el resultado real es 0 de 12 líneas con candidato comparable.
Ejemplo:       Notebook 05, Pasos 1b-1c — tabla completa de 32 (Tarifario) y 12 (Sicetac) líneas con su diferencia %.
Hipótesis:     El sesgo hacia arriba en Tarifario podría ser un margen intencional del negocio. Para Sicetac no hay hipótesis que evaluar — no hay candidato comparable en este extracto, ni a favor ni en contra de un incumplimiento del piso.
Verificación:  Cálculo directo, cruzando por ruta normalizada (rapidfuzz, notebook 06) contra tarifa-plaza (Tarifario, filtrando lineanegocio≠"6") y piso-sicetac núcleo+raras (Sicetac, filtrando condicioncarga="CARGADO" y respetando dirección de ruta).
Severidad:     Alta (Tarifario, patrón consistente en 32 casos) / N/A (Sicetac — no hay comparación posible, no es un hallazgo de negocio, es una limitación de muestra, ver L-01)
Impacto:       Es la primera respuesta numérica real a la pregunta central de esta HU, y aplica solo al lado Tarifario/SAP. Para Sicetac, no hay evidencia utilizable en este extracto — cualquier alerta sobre el piso legal basada en esta muestra de 151 filas no estaría fundamentada.
Recomendación: Para Tarifario, investigar con negocio si el +30,6% es margen intencional o desalineación de tarifa. Para Sicetac, repetir esta comparación con la tabla completa de tblRutasSicetac (no la muestra de 151 filas) antes de poder decir algo sobre cumplimiento del piso legal.
```

## H-006

```
ID:            H-006
Título:        Con rapidfuzz, 91,7% de las ciudades propias conectan con confianza a SAP; 2 ciudades del Amazonas no
Dimensión:     Cobertura geográfica
Evidencia:     De 24 ciudades únicas en catalogo_rutas, 22 (91,7%) dan match ≥90% contra el vocabulario de zonas SAP (resolviendo casos como "Bogotá D.C."→Bogotá, "Santiago de Cali"→Cali). Leticia y Puerto Nariño dan match de 66-67% con lugares geográficamente incorrectos (Útica, Pto Nare) — declarados sin match confiable, no aceptados a ciegas.
Ejemplo:       Ver notebook 06, Paso 3, tabla completa de 24 ciudades con su puntaje.
Hipótesis:     N/A — cálculo directo.
Verificación:  rapidfuzz.process.extractOne sobre las 24 ciudades vs. vocabulario derivado de tarifa-plaza/historico-sap.
Severidad:     Baja (buena cobertura general)
Impacto:       Esta es una aproximación nuestra, no el mecanismo real (ver H-004) — sirve como estimado práctico, no como reproducción exacta.
Recomendación: Usar como estimado de cobertura esperada; la extracción futura debe usar tblsapdane/tblZonasTarificacionRelacion directamente, no fuzzy matching.
```

## H-007

```
ID:            H-007
Título:        Fuente=Tarifario reconfirmado 100% (32/32); Fuente=Sicetac no verificable con este extracto
Dimensión:     Consistencia
Evidencia:     Reproducción con código propio del hallazgo de HU-10.3: 32/32 líneas Fuente=Tarifario encuentran su ValorFlete exacto en tarifa-plaza.VALOR. Las 13 líneas Fuente=Sicetac dan 0/13 — pero los 7 valores únicos buscados coinciden en cantidad exacta con "6 verificables + 1 malformada" del reporte original de HU-10.3.
Ejemplo:       Notebook 03, Paso 2.
Hipótesis:     N/A.
Verificación:  Búsqueda de valor exacto en piso-sicetac (151 filas) vs. la tabla completa de SICE-TAC (~2 millones de filas, usada en la verificación original de HU-10.3).
Severidad:     Media
Impacto:       No es que el motor falle — es que nuestra muestra de 151 filas no fue diseñada para incluir las rutas de estas 13 solicitudes. Confirmado también con una consulta 🔎 directa contra la tabla viva de SICE-TAC (ver nota en notebook 03): de los 6 valores de `Fuente=Sicetac` verificables, solo 1 aparece hoy — consistente con que el período ya avanzó de agosto (`202608`) a septiembre (`202609`), no con que el motor fallara en su momento. El hallazgo original (6/6 contra la foto de agosto) se mantiene válido, solo no reproducible aquí. Esta conclusión queda además reforzada de forma independiente por H-005 (el cruce por ruta normalizada tampoco encuentra candidato comparable para ninguna de las 12 líneas Sicetac válidas).
Recomendación: Ninguna acción sobre el sistema — documentar la limitación de muestra.
```

## H-008

```
ID:            H-008
Título:        ValorAprovisionamiento/ValorRetornoContenedor 100% vacías — explicado, no es un bug
Dimensión:     Completitud
Evidencia:     0 de 91 filas combinadas (lineas_solicitud + cotizacion_lineas) tienen dato. La única línea que cumplía la condición de negocio para activarse (RetornaContenedor=True) usa la ruta de prueba malformada "222"/"222" — no hay tarifa que calcular para una ciudad que no existe.
Ejemplo:       lineas_solicitud.id=E803854D-... (RetornaContenedor=True, ruta "222"/"222").
Hipótesis:     Inicialmente se sospechó una falla de cálculo; descartada con evidencia.
Verificación:  Cruce directo entre la línea con la condición cumplida y su ruta asociada.
Severidad:     Baja
Impacto:       Ninguno — explicado por ruido de datos de prueba ya conocido (H de rutas malformadas), no requiere acción de desarrollo.
Recomendación: Ninguna. Se reevaluará naturalmente cuando exista producción real.
```

## H-009

```
ID:            H-009
Título:        TAXNUMXL (100% nula) y VALID_TO (100% valor centinela 9999-12-31) no aportan señal en este extracto
Dimensión:     Completitud / Exactitud
Evidencia:     TAXNUMXL: 0 valores no nulos de 38.995. VALID_TO: el 100% de las 38.995 filas tiene exactamente "9999-12-31 23:59:59" — no es "la mayoría", es todas, sin una sola fecha de vencimiento real.
Ejemplo:       catalogo-clientes-BUT000-organizaciones, columnas taxnumxl y validTo.
Hipótesis:     N/A — conteo directo.
Verificación:  Perfilado de columnas, notebook 01.
Severidad:     Baja
Impacto:       Ninguna conclusión debe apoyarse en estas 2 columnas en este extracto.
Recomendación: Documentar como columnas sin información útil en este snapshot; no incluir como variable en el futuro modelo salvo que una extracción nueva muestre variabilidad real.
```

## H-010

```
ID:            H-010
Título:        historico-sap (tblSAPTarifasLog) se comporta más como auditoría de ediciones que como registro de tarifa aplicada
Dimensión:     Granularidad
Evidencia:     Solo 8.135 de 23.020 tarifas vigentes (35%) tienen alguna versión en el histórico; de esas, la mediana es 2 versiones. El 65% de las tarifas activas nunca aparece en el histórico — patrón inconsistente con "cada vez que se aplicó la tarifa" (se esperaría que una tarifa activa se "aplicara" muchas veces).
Ejemplo:       Notebook 02, Paso 4.
Hipótesis:     Auditoría de ediciones ocasionales, no historial de uso.
Verificación:  Evidencia indirecta (distribución de versiones por tarifa) — no confirmado directamente con negocio, sigue como pregunta abierta del diccionario.
Severidad:     Media
Impacto:       No se puede usar tblSAPTarifasLog como proxy de "qué tarifa se aplicó de verdad" sin la confirmación pendiente.
Recomendación: Confirmar con negocio la semántica exacta de esta tabla antes de usarla como fuente de verdad para el modelo.
```

## H-011

```
ID:            H-011
Título:        El salto de +17% en marzo 2026 encontrado en el EDA-1 no se replica en el catálogo de tarifas SAP
Dimensión:     Temporal
Evidencia:     Analizando VALOR de historico-sap por mes de VIGENCIA, segmentado por línea de negocio: las 4 categorías principales bajan (no suben) de febrero a marzo 2026. Sí hay indicio de subida gradual en "Nacional" entre noviembre 2025 y febrero 2026 (+18,5%, repartida en 4 meses, no concentrada).
Ejemplo:       Notebook 06, Paso 2.
Hipótesis:     El salto del EDA-1 (flete realmente pagado) y la fecha de VIGENCIA de SAP (cuándo se fija una tarifa) pueden no estar temporalmente alineados — el pago podría reflejar tarifas fijadas con rezago.
Verificación:  Groupby mes×línea de negocio sobre historico-sap.
Severidad:     Media
Impacto:       No se puede confirmar cruzadamente el hallazgo del EDA-1 con esta fuente — se documenta como "no confirmado", ni como refutación ni como validación limpia.
Recomendación: Investigar en la extracción futura si el rezago pago-vs-vigencia explica la discrepancia, en vez de asumir que los dos deberían coincidir en el mismo mes.
```

## H-012

```
ID:            H-012
Título:        4 ID repetidos en piso-sicetac_raras — rutas que califican como extremas en 2 categorías a la vez (no error)
Dimensión:     Unicidad
Evidencia:     25 filas, solo 21 ID únicos. Las 4 repeticiones son rutas Santa Marta↔Tumaco que aparecen tanto en "rara_valor_maximo" como en "rara_via_no_estandar" — mismo ID, mismo valor, dos categorías de clasificación.
Ejemplo:       ID 66954566, aparece en ambas categorías con VALORSICECONHORA=20.844.848 y HORASRECORRIDO=58,71 idénticos en ambas filas.
Hipótesis:     N/A — confirmado.
Verificación:  Cruce directo de ID entre nucleo y raras (0 solapamiento entre ellas) y dentro de raras (4 duplicados explicados).
Severidad:     Baja
Impacto:       Ninguno — representa fielmente que esas rutas son extremas en 2 sentidos a la vez.
Recomendación: Ninguna acción; documentar como comportamiento esperado del diseño de muestreo.
```

## H-013

```
ID:            H-013
Título:        Huérfanos en cotizaciones→solicitudes (5/18) y tarifas_historicas→cotizaciones (1 cotización faltante) — causa no confirmada
Dimensión:     Integridad referencial
Evidencia:     5 de 18 cotizaciones (todas en estado Borrador) apuntan a un SolicitudId que no existe en solicitudes.csv. 3 filas de tarifas_historicas apuntan a la misma cotización faltante.
Ejemplo:       Notebook 02, Paso 3.
Hipótesis:     Registros de solicitud/cotización borrados o recreados durante pruebas manuales.
Verificación:  El usuario no pudo confirmar la causa exacta ("no sabría decirte, la app no está funcionando al 100%").
Severidad:     Media
Impacto:       No compromete el resto del análisis — son 5-8 filas de 13-48, ya identificadas y no usadas para conclusiones de negocio.
Recomendación: Ninguna acción sobre el pipeline de este EDA; si se repite en producción real, sí ameritaría investigación del equipo de desarrollo.
```

## H-014

```
ID:            H-014
Título:        Outliers de precio en SAP (IQR), concentrados en Devolución Contenedor y Nacional
Dimensión:     Outliers
Evidencia:     Devolución Contenedor: 48 de 1.031 (4,7%, el % más alto). Nacional: 326 de 13.625 (2,4%, la mayor cantidad absoluta). 0 valores negativos confirmados en las 3 tablas de precio (tarifa-plaza, piso-sicetac núcleo y raras) — los límites inferiores negativos del IQR son teóricos, sin caso real. 9 filas con VALOR=0 (0,04%), todas Devolución Contenedor con vehículo sin especificar — probablemente registros incompletos, impacto despreciable.
Ejemplo:       Notebook 07, Paso 1 (boxplot en reportes/figures/outliers_valor_por_linea_negocio.png).
Hipótesis:     N/A — cálculo directo.
Verificación:  IQR (1,5×rango intercuartílico) por línea de negocio.
Severidad:     Baja
Impacto:       Informativo — no requiere limpieza, son precios de mercado reales dentro de categorías con alta variabilidad natural.
Recomendación: Ninguna acción; considerar para el futuro modelo que estas 2 categorías tienen mayor dispersión de precio que el resto.
```

## H-015

```
ID:            H-015
Título:        DICCIONARIO_DATOS.md faltaba en el snapshot congelado v1.0.0-piloto — corregido
Dimensión:     Documentación / Autocontención del snapshot
Evidencia:     diccionario-y-linaje.md referenciaba ./datos/bd-propia/DICCIONARIO_DATOS.md para el detalle de los 5 catálogos, pero ese archivo nunca se copió al snapshot — solo existía en 00-fuentes/bd-propia/, rompiendo la promesa de que v1.0.0-piloto es autocontenido.
Ejemplo:       N/A.
Hipótesis:     N/A — confirmado y corregido en esta sesión (2026-09-11).
Verificación:  Comparación directa de contenido de carpetas; el archivo no había cambiado desde su creación (2026-09-01), seguro de copiar tal cual.
Severidad:     Baja
Impacto:       Ya corregido — no afecta ningún análisis de este EDA.
Recomendación: Ninguna acción adicional; ya copiado y documentado en el README de v1.0.0-piloto.
```

## H-016 — retirado tras revisión de calidad (2026-09-11)

```
ID:            H-016 (RETIRADO)
Título:        [Se afirmaba] El piso SICE-TAC subió +21,7% entre agosto 2025 y septiembre 2026 (tblRutasSicetacMesAnterior2)
Motivo de retiro: Esta ficha apareció en el documento sin ninguna evidencia trazable — a diferencia de las demás verificaciones 🔎 de este proyecto (que muestran la consulta SQL exacta, como la nota del notebook 03, o el archivo fuente exacto, como H-003/H-004), aquí no hay consulta documentada en ningún notebook, ni en este archivo. El notebook 02, donde se descubrió originalmente la pista de las tablas `MesAnterior*` (§Fase 3), sigue sin actualizarse y todavía dice explícitamente "no resuelta aquí" — contradice directamente que este hallazgo esté "confirmado". Se retira en vez de corregirse porque no hay una cifra verificada que dejar en su lugar.
Estado real:   La pista sigue como estaba — ver L-07 en limitaciones.md ("sigue sin resolver").
Recomendación: Si se retoma esta investigación, documentar la consulta SQL exacta y su resultado (igual que se hizo para H-007 en notebook 03) antes de declararlo hallazgo — no basta con reportar el número final.
```

---

## Log de supuestos

| # | Decisión | Justificación | Reversibilidad |
|---|---|---|---|
| 1 | Excluir las 3 líneas Fuente=Manual de cualquier estadístico de ValorFlete | Confirmado por el usuario como valores puestos al azar, sin relación con precio real | Alta — están señaladas, no borradas del dataset |
| 2 | piso-sicetac: separar núcleo (126) de raras (25) desde la carga, nunca mezclar | Evita sesgo hacia los casos extremos elegidos a propósito en la muestra | Alta — ambas tablas se conservan por separado |
| 3 | Usar rapidfuzz como aproximación de cobertura geográfica, no como el mecanismo real | El mecanismo real (tblsapdane/tblZonasTarificacionRelacion) no está disponible en este extracto | Media — depende de conseguir esas 2 tablas en el futuro |
| 4 | Declarar Fuente=Sicetac como "no verificable" en vez de "falla" | La muestra de 151 filas de piso-sicetac no fue diseñada para incluir las rutas de las 13 solicitudes de prueba | Baja — es una limitación estructural del extracto, no de metodología |
| 5 | No conformarse con "0 casos por código de negocio" — reintentar el cruce bivariado propio↔mercado (§7.1/7.2) usando ruta normalizada en vez de LineaNegocio/ClaseViaje | El cruce por LineaNegocio/ClaseViaje dio 0 casos limpios (contaminados por ruido conocido); una vez resuelta la ruta con rapidfuzz (notebook 06), sí fue posible cruzar por ruta — ver H-005 | Alta — el resultado quedó reproducible en el notebook 05, Pasos 1b-1c |
| 6 | Fuente×Resultado declarado sin señal de negocio | El usuario confirmó que también marcó Resultado a mano durante pruebas, no refleja decisión real de cliente | Baja — confirmado directamente por el usuario |
| 7 | historico-sap tratado como "posible auditoría de ediciones", no como "tarifa aplicada" | Evidencia indirecta (65% de tarifas activas sin ninguna versión en el log) | Media — pendiente de confirmación directa con negocio |
