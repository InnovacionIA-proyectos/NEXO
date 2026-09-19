# Hallazgos del EDA — Extracción que cruza las 4 fuentes

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
ID:            H-003 🔎
Título:        LineaNegocio/ClaseViaje/ClaseContenedor no son "la misma llave" entre lado propio y SAP — requieren traducción (las 3 traducciones ya verificadas, incluyendo ClaseContenedor)
Dimensión:     Consistencia / Cobertura de cruce
Evidencia:     lineas_solicitud guarda texto ("Andino", "Contenedor"), SAP guarda código numérico ("7", "1"). Comparación directa de texto = 0% de match. La traducción correcta existe como diccionario fijo en el backend (TarifarioSapAdapter.cs): LineaNegocio {Andino=7, DevolucionContenedor=6, Nacional=4, PuertoAInterior=1, InteriorAPuerto=2, Licitaciones=8}; ClaseViaje {Contenedor=1, Cupo=2, CargaSuelta=4, Extradimensionado=5}. Con la traducción, ClaseViaje sube a 100% (5/5) y LineaNegocio a 25% (1/4). **Actualización (notebook 06, Paso 3c):** `tblSAPClaseContenedor` ya se consiguió — los 2 valores no nulos de `ClaseContenedor` en `lineas_solicitud` (`CNT20.IT`, `CNT20.ST`) traducen exacto a sus ID SAP (13 y 8) y ambos códigos existen con volumen real en `tarifa-plaza` (171 y 1.641 filas). Las 3 traducciones de este hallazgo quedan verificadas al 100%, ya no solo "confirmadas en código".
Ejemplo:       lineas_solicitud con LineaNegocio="Andino" vs. tarifa-plaza.lineanegocio="7"; ClaseContenedor="CNT20.IT" vs. tarifa-plaza.clasecontenedor="13".
Hipótesis:     N/A — confirmado en el código fuente del backend y, para ClaseContenedor, también contra el catálogo real y los datos.
Verificación:  Lectura directa de src/Nexo.Infrastructure/Biblioteca/Adaptadores/TarifarioSapAdapter.cs. 🔎 ClaseContenedor verificado además con `tblSAPClaseContenedor_202609142051.csv`, notebook 06 Paso 3c.
Severidad:     Baja (ya resuelto)
Impacto:       diccionario-y-linaje.md (del set final) afirma que estas columnas son "la misma llave" — en la práctica es incorrecto/incompleto sin esta traducción, aunque ya está resuelta y documentada. `ClaseContenedor` ya no es "no verificable con este set" — se cerró con el catálogo real (ver limitaciones.md L-03).
Recomendación: Corregir diccionario-y-linaje.md para aclarar que se necesita traducción (ya no falta ninguna tabla para hacerlo).
```

## H-004 🔎

```
ID:            H-004
Título:        Ruta es la llave de cruce dominante (no los códigos de negocio) — confirmado por el usuario y ahora reproducido con las 2 tablas reales (ya conseguidas)
Dimensión:     Consistencia / Cobertura de cruce
Evidencia:     El usuario confirmó que el motor real de costeo filtra principalmente por ruta (ciudad origen-destino), con los códigos de negocio como secundarios. Verificado en el backend: TarifarioSapAdapter.BuscarConZonasAsync resuelve ciudad→zona SAP vía 2 catálogos exactos — tblsapdane (código DANE) y, si falla, tblZonasTarificacionRelacion (zona ZC_/ZTC_). **Actualización (notebook 06, Paso 3b):** ambas tablas ya se consiguieron (`tblSAPDANE_202609142051.csv`, `tblZonasTarificacionRelacion_202609142051.csv`) y se usaron para reproducir el mecanismo real — ver H-006 para el resultado completo (21/24 ciudades resueltas, 3 correcciones reales encontradas sobre la aproximación anterior). Para SICE-TAC, el backend sí hace lo mismo que aproximamos (substring de ciudad contra NombreRuta).
Ejemplo:       Bogotá se resuelve a ZTC_BOGOTA (zona metropolitana), no a un código individual — confirmado ahora consultando tblZonasTarificacionRelacion directamente, no solo por lectura de código.
Hipótesis:     N/A — confirmado en código fuente + confirmación de negocio + reproducción directa con las tablas reales.
Verificación:  Lectura de TarifarioSapAdapter.cs + confirmación directa del usuario + reproducción del mecanismo en notebook 06 Paso 3b.
Severidad:     Alta
Impacto:       Reordena la prioridad de todo el cruce de este EDA — Ruta, no LineaNegocio/ClaseViaje, es la pieza que más importa. Con tblsapdane/tblZonasTarificacionRelacion ya disponibles, el mecanismo de matching por ruta puede reproducirse real, no solo aproximarse (ver H-006).
Recomendación: Ya resuelto — las 2 tablas se consiguieron y se usaron. tblSAPClaseContenedor (H-003) y tblSAPTiposCarroceria (H-017) también se consiguieron y ya se verificaron (notebook 06, Paso 3c) — no queda ninguna tabla pendiente de las identificadas por este EDA.
```

## H-005

```
ID:            H-005 🔎
Título:        Ni SAP ni SICE-TAC muestran que Nexo cotice mal: las 32 líneas Tarifario y las 12 Sicetac quedan explicadas — 28+12 por el algoritmo real de cada motor, 4 por captura manual confirmada
Dimensión:     Exactitud / Cruce bivariado (§7.1/§7.2 del plan)
Evidencia:
Este hallazgo empezó con un +30,6% de sobreprecio contra SAP (punto 1) y un "0 de 12 sin candidato" contra SICE-TAC (punto 4, muestra chica). Los puntos 2-3 y 5-7 muestran que ninguno de los dos era lo que parecía:
  1. Comparación original contra SAP: las 32 cotizaciones caen dentro de lo que cobra SAP en su ruta, pero salen 30,6% más caras que el precio típico (hasta 57%; solo Cali-Bogotá sale más barato, -31,6%).
  2. Angostando por camión y peso: el camión no se pudo usar — la aplicación no está capturando correctamente ese dato al cotizar por Tarifario (confirmado por el usuario, no un hueco de la fase piloto), así que no está guardado en ninguna cotización. El peso sí, y el número bajó a 9,1%.
  3. La pregunta correcta no era el precio típico: el programa no promedia, elige la tarifa SAP más alta que aplica. Reproduciendo esa regla (vigente el día de cada cotización, mismo peso, la más alta), 20/32 (62,5%) dan el valor exacto cobrado en un primer intento.
  4. Comparación original contra SICE-TAC: 0/12 comparables — pero era la muestra curada de 151 filas, que nunca cubrió Medellín/Bogotá/Cali/Barranquilla, no una falla del sistema.
  5. Con el extracto real completo de SICE-TAC de agosto 2026 (~2,1 millones de filas, el mismo que usa el sistema, conseguido por el usuario) y reproduciendo la regla real de selección (vehículo si hay dato, si no tipo de carga "General", vía estándar + menos horas + menor valor), **las 12 de 12 líneas (100%) dan el valor exacto cobrado.**
  6. De las 12 líneas SAP que no coincidían (punto 3): como H-007 ya había confirmado que las 32 líneas SÍ tienen su valor exacto en algún lado de `tarifa-plaza`, se buscó sin restringir zona — apareció bajo `ZTC_NORTE_BOGOTA`, **una segunda zona SAP para el área de Bogotá**, distinta de la genérica `ZTC_BOGOTA`. `tblZonasTarificacionRelacion` confirma que son 2 tablas de precio paralelas (Norte cobra 5-10% más en casi todo rango de peso/vehículo) — la ciudad de Bogotá en sí está mapeada oficialmente a la zona genérica, `ZTC_NORTE_BOGOTA` es para municipios satélite al norte (Chía, Zipaquirá, Cajicá, etc.). `Buenaventura→Bogotá` y `Cali→Bogotá` (8 líneas) coinciden exacto con la zona Norte — la explicación más probable es que el destino real de esas 2 solicitudes de prueba fuera un municipio satélite del norte, simplificado a "Bogotá" en `catalogo_rutas` (no confirmado con certeza). Sube a 28/32.
  7. Las últimas 4 líneas (`Bucaramanga→Bogotá`) no coincidían en ninguna zona ni fecha de vigencia. Se buscó el valor exacto sin restringir ruta ni línea de negocio — salen 7 filas en `tarifa-plaza` con ese mismo valor, la mayoría coincidencias de otras rutas sin relación; de esas, una sola corresponde a la ruta real (`ZTC_BUCARAMANGA→ZTC_BOGOTA`), bajo `lineanegocio="6"` (Devolución Contenedor, la categoría excluida a propósito del resto de esta comparación por ser reposicionamiento de contenedor vacío). Pero `lineas_solicitud` tiene `RetornaContenedor=False` para estas líneas — el contexto no cuadra con esa tarifa. **Confirmado por el usuario: el valor se capturó manualmente desde un Excel de referencia, no lo generó el motor en vivo** — es captura manual (como `Fuente=Manual`, H-002), con un valor de mercado real pero no comparable por el algoritmo. **Resultado final: 32/32 líneas Tarifario explicadas** (28 por el algoritmo, 4 por captura manual confirmada).
Ejemplo:       Notebook 05, Pasos 1b-1e, 1h e 1i (SAP) y 1g (SICE-TAC).
Hipótesis:     No hay margen intencional ni tarifa mal puesta en ningún lado — es lo esperable si cada motor sigue su propia regla de selección (SAP: la más alta; SICE-TAC: la más eficiente), más un destino más específico de lo que capta nuestro catálogo de rutas en 2 casos, y captura manual en los otros 4. La pregunta al negocio es si esas políticas de selección son las que quieren, no si hay un error.
Verificación:  🔎 Reproducción de la lógica real de ambos motores (`PropuestaTarifaAdapter.CandidatoSapAsync` para SAP, `SeleccionarSice` para SICE-TAC) comparando contra el precio cobrado, no contra un promedio — SAP con `tarifa-plaza` (incluyendo la zona `ZTC_NORTE_BOGOTA`), SICE-TAC con el extracto real de agosto 2026. Las 4 líneas de Bucaramanga→Bogotá, confirmación directa del usuario (captura manual desde Excel).
Severidad:     Baja (ambos motores funcionan como fueron diseñados; no es una falla; las 4 líneas manuales son de fase piloto, no de producción)
Impacto:       Ya no es "¿por qué cotiza distinto al mercado?" sino "¿quieren que SAP siga ofreciendo el precio más alto y SICE-TAC el más eficiente?" — decisión de producto, no error de datos. El bug de captura del camión sigue siendo un problema real aparte, para desarrollo (L-13). La existencia de 2 tablas de precio paralelas para el área de Bogotá es relevante para cualquier análisis futuro que use solo el nombre de ciudad como destino, sin la localidad específica (ver H-006).
Recomendación: Llevar al negocio la pregunta de política de precios de ambos motores. Reportar a desarrollo el bug de captura del camión en Tarifario (L-13). Extender la verificación de SICE-TAC a los otros 26 períodos históricos disponibles para resolver L-07 a escala completa.
```

## H-006

```
ID:            H-006 🔎
Título:        Con la tabla real de zonas (no una aproximación), 21 de 24 ciudades quedan bien ubicadas — el método anterior se equivocaba, con aparente seguridad, en 3 de ellas
Dimensión:     Cobertura geográfica
Evidencia:
  1. Primero se probó una aproximación (buscar el nombre más parecido). De 24 ciudades del catálogo propio, 22 (91,7%) encontraron una zona con un puntaje de similitud alto. Leticia y Puerto Nariño no encontraron nada confiable — se emparejaban con lugares que no tenían nada que ver.
  2. Ya se consiguieron las tablas reales que usa SAP para ubicar una ciudad en su zona. Con ellas se repitió el ejercicio de forma exacta, no aproximada: 21 de 24 ciudades (87,5%) quedan bien ubicadas, pero solo 18 de esas 24 coinciden con lo que había dado el método anterior.
  3. 3 ciudades (Mosquera, Soacha y Rionegro) las estaba ubicando mal la aproximación, aunque con un puntaje de similitud alto que parecía confiable. El método real las ubica correctamente como parte del área metropolitana de Bogotá (Mosquera, Soacha) o de Medellín (Rionegro), no como una zona propia aislada.
  4. Leticia y Puerto Nariño se confirman sin zona en SAP, esta vez con certeza (antes solo se sospechaba). "Bogotá D.C." (una forma puntual de escribir el nombre) no se pudo ubicar porque no aparece así en el catálogo oficial — no afecta nada, porque las otras formas de escribir Bogotá sí se ubican bien.
  5. **Límite nuevo, encontrado después (ver H-005, notebook 05 Paso 1h): ni este método ni la aproximación pueden resolver un destino más específico que "la ciudad" cuando esa ciudad tiene más de una zona SAP.** `tarifa-plaza` tiene 2 tablas de precio paralelas para el área de Bogotá — `ZTC_BOGOTA` (la ciudad y satélites al occidente/sur) y `ZTC_NORTE_BOGOTA` (satélites al norte: Chía, Zipaquirá, Cajicá, etc.), con precios sistemáticamente distintos entre ambas. El valor exacto de 2 cotizaciones de prueba (`Buenaventura→Bogotá`, `Cali→Bogotá`) aparece bajo la zona Norte — como la ciudad de Bogotá está mapeada oficialmente a la zona genérica, la explicación más probable es que el destino real de esas 2 solicitudes fuera un municipio satélite del norte, simplificado a "Bogotá" en el catálogo de rutas (no confirmado con certeza). Ni la aproximación ni el catálogo real de este hallazgo pueden distinguir esto, porque ambos resuelven por el nombre de ciudad que reciben, no por una localidad más específica.
Ejemplo:       Notebook 06, Paso 3 (aproximación) y Paso 3b (tabla real) — comparación de las 24 ciudades por ambos métodos.
Hipótesis:     No aplica — esto se calculó directo con la tabla real, ya no es una aproximación.
Verificación:  🔎 Se repitió, con las tablas reales, el mismo mecanismo que usa el programa para ubicar una ciudad en su zona de SAP (H-004), notebook 06 Paso 3b.
Severidad:     Baja — la cobertura sigue siendo buena. Lo que baja es la confianza en el método anterior: puede fallar, y con aparente seguridad, en ciudades satélite de una zona metropolitana.
Impacto:       Ya no es una aproximación, es el mecanismo real. Ninguna de las 3 correcciones afecta a las 32 cotizaciones de H-005 (ese resultado no cambia), pero sí importa para cualquier análisis futuro que use este tipo de ciudades satélite.
Recomendación: Usar las tablas reales para ubicar ciudades en su zona, no la aproximación por similitud de texto — dejar esa aproximación solo como respaldo cuando las tablas reales no den respuesta.
```

## H-007

```
ID:            H-007
Título:        Fuente=Tarifario reconfirmado 100% (32/32); Fuente=Sicetac no verificable con este extracto
Dimensión:     Consistencia
Evidencia:     Reproducción con código propio del hallazgo del set final: 32/32 líneas Fuente=Tarifario encuentran su ValorFlete exacto en tarifa-plaza.VALOR. Las 13 líneas Fuente=Sicetac dan 0/13 — pero los 7 valores únicos buscados coinciden en cantidad exacta con "6 verificables + 1 malformada" del reporte original del set final.
Ejemplo:       Notebook 03, Paso 2.
Hipótesis:     N/A.
Verificación:  Búsqueda de valor exacto en piso-sicetac (151 filas) vs. la tabla completa de SICE-TAC (~2 millones de filas, usada en la verificación original del set final).
Severidad:     Media
Impacto:       No es que el motor falle — es que nuestra muestra de 151 filas no fue diseñada para incluir las rutas de estas 13 solicitudes. Confirmado también con una consulta 🔎 directa contra la tabla viva de SICE-TAC (ver nota en notebook 03): de los 6 valores de `Fuente=Sicetac` verificables, solo 1 aparece hoy — consistente con que el período ya avanzó de agosto (`202608`) a septiembre (`202609`), no con que el motor fallara en su momento. El hallazgo original (6/6 contra la foto de agosto) se mantiene válido, solo no reproducible aquí. **Actualización (H-005, notebook 05 Paso 1g):** conseguido el extracto real completo de SICE-TAC de agosto 2026, sí se pudo reproducir el mecanismo real para las 12 líneas Sicetac válidas — 12/12 coinciden exacto. Confirma que el motor nunca falló; la limitación era 100% de la muestra de 151 filas, ahora superada para este período específico.
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
Título:        historico-sap (tblSAPTarifasLog) es una auditoría de ediciones — y así mismo la usa Nexo, como tarifa histórica dentro de su motor de precios
Dimensión:     Granularidad
Evidencia:     Solo 8.135 de 23.020 tarifas vigentes (35%) tienen alguna versión en el histórico; de esas, la mediana es 2 versiones. El 65% de las tarifas activas nunca aparece en el histórico — patrón inconsistente con "cada vez que se aplicó la tarifa" (se esperaría que una tarifa activa se "aplicara" muchas veces). **Confirmado por el usuario:** es auditoría de ediciones, no historial de uso — y Nexo la usa precisamente así, como su fuente de "tarifa histórica" dentro del motor de precios (coincide con el código: `PropuestaTarifaAdapter` consulta esta tabla como candidato "historico_sap", junto a SICE-TAC y plaza, ver H-005).
Ejemplo:       Notebook 02, Paso 4.
Hipótesis:     N/A — confirmado.
Verificación:  Confirmado por el usuario. Consistente con el código real (`PropuestaTarifaAdapter.CandidatoSapAsync`, ver H-005), que trata esta tabla como una fuente más de tarifa, no como registro de aplicaciones.
Severidad:     Baja (ya resuelto)
Impacto:       Sí se puede usar tblSAPTarifasLog como "tarifa histórica" para el futuro modelo — es exactamente el uso que le da el propio Nexo, no un mal uso de un log de auditoría.
Recomendación: Documentar esta semántica en diccionario-y-linaje.md; ninguna acción adicional pendiente.
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
Evidencia:     5 de 18 cotizaciones (todas en estado Borrador, del 24-25 de agosto) apuntan a un SolicitudId que no existe en solicitudes.csv. 3 filas de tarifas_historicas apuntan a la misma cotización faltante (también del 25 de agosto). Estas fechas no coinciden con la corrida (única) del DataSeeder ya conocida de otras sesiones (~30 registros del 2026-08-21, excluidos del set final) — el seeder corrió una sola vez, así que no explica por sí solo estos huérfanos de fechas posteriores.
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

## H-017

```
ID:            H-017 🔎
Título:        TIPOCARROSERIA (tarifa-plaza/historico-sap) era un código sin traducción disponible — ya resuelto con el catálogo real (100% de cobertura)
Dimensión:     Columna faltante / Cobertura
Evidencia:     A diferencia de LINEANEGOCIO (trae LineaNegocioDescripcion) y TIPOVEHICULO (trae VehiculoDescripcion), TIPOCARROSERIA no tenía ninguna columna de descripción emparejada en los 4 extractos originales — era un código puro (ej. "9", "2", "4") sin catálogo de referencia. **Actualización (notebook 06, Paso 3c):** se consiguió `tblSAPTiposCarroseria_202609142052.csv` — los 6 códigos no nulos en `tarifa-plaza` (9, 2, 4, 1, 16, 11) y los 5 en `historico-sap` traducen el 100%, sin ningún código "SIN TRADUCCIÓN": `9`=Planchón (7.280 filas, el más común), `2`=Estacas (6.897), `4`=Furgón (4.884), `1`=Camabaja, `16`=Volco, `11`=Portacontenedores.
Ejemplo:       tarifa-plaza.TIPOCARROSERIA = "9" → PLANCHON, "2" → ESTACAS, "4" → FURGON (antes sin descripción disponible en ningún extracto ni en el diccionario oficial).
Hipótesis:     N/A — resuelto directamente con el catálogo real, ya no es una hipótesis.
Verificación:  🔎 `tblSAPTiposCarroseria_202609142052.csv` cruzado contra los valores reales de `tarifa-plaza`/`historico-sap`, notebook 06 Paso 3c.
Severidad:     Baja (ya resuelto)
Impacto:       `TIPOCARROSERIA` ya puede usarse como variable categórica con significado de negocio claro (ej. en el futuro modelo) — antes se hubiera tratado como código numérico sin interpretación.
Recomendación: Ninguna acción adicional — incluir `tblSAPTiposCarroseria` como catálogo de referencia en cualquier extracción o documentación futura que use `TIPOCARROSERIA`.
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
| 7 | historico-sap tratado como "auditoría de ediciones", usada por Nexo como tarifa histórica | Evidencia indirecta (65% de tarifas activas sin ninguna versión en el log) + confirmado directamente por el usuario | Alta — confirmado, ya no depende de evidencia indirecta |
| 8 | H-005: para las 8/32 líneas Tarifario sin ningún candidato SAP dentro de su rango de peso, usar como fallback la mediana solo-ruta (Paso 1b) en vez de descartarlas | Bucaramanga↔Bogotá (21.000 kg) y Bogotá↔Cúcuta (15.000 kg) no tienen ninguna tarifa configurada en tarifa-plaza cuyo rango de peso las cubra exactamente; descartarlas sesgaría la mediana final hacia las rutas que sí tienen cobertura fina | Alta — documentado explícitamente por línea en el notebook (columna `metodo`), no es un descarte silencioso |
| 9 | H-005 (Paso 1e): no perseguir el 100% de reproducción exacta del algoritmo real (quedó en 20/32) ni usar `historico-sap` como fuente adicional pese a que el motor real también lo consulta | Sumar `historico-sap` (deduplicado por `idTarifa` a su última versión) empeoró el resultado (12/32 en vez de 20/32) — probable porque no hay forma de reconstruir su estado exacto a la fecha de cada cotización sin una foto histórica; las 12 líneas restantes se explican por limitaciones ya conocidas (L-02, L-13), no por una causa nueva sin investigar | Alta — la comparación con y sin histórico quedó documentada en el notebook (Paso 1e), no es una decisión oculta |
