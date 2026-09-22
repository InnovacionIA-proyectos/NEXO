# S2-HU-13.4 (issue #43) — Verificación de reglas de negocio con datos reales

Rama: `43-s2-hu-134-verificación-de-reglas-de-negocio-con-datos-reales`.

## Objetivo

Primera puesta en operación real de CabinaFletes: el desarrollo anterior fue
un PoC que nunca estuvo en producción (se archiva como referencia, sin
coexistencia ni vuelta atrás que gestionar). Esta HU verifica, por muestreo
contra **datos reales** ya cargados en el ambiente desplegado (matriz
SICE-TAC vigente + histórico de tarifas SAP + operación real de CabinaFletes),
que las 7 reglas de negocio del motor de costeo se cumplen de verdad en la
plataforma — no solo contra datos sintéticos, como sí se hizo en
[`hu-04.7-pliego-dataset-validacion-muestral`](../hu-04.7-pliego-dataset-validacion-muestral/README.md)
y [`hu-09.2-tarifas-plaza-provisionales`](../hu-09.2-tarifas-plaza-provisionales/README.md).

Continuación directa de la recomendación §7 de
`analitica/04-comparativa-y-propuesta-modelo/recomendacion_captura_datos.md`
(Habilitador B8): esa regla (`ReglaDeCosteo`, `MAX(piso SICE-TAC, tarifa
SAP)`) solo se había probado contra cotizaciones de prueba internas, nunca
contra operación real.

## Las 7 reglas y dónde viven en el código

| # | Regla | Código real |
|---|---|---|
| 1 | Regla del mayor | [`ReglaDeCosteo.Decidir`](../../../src/CabinaFletes.Domain/Costeo/Reglas/ReglaDeCosteo.cs#L49-L83) |
| 2 | Piso inviolable | Consecuencia de la regla 1 (mismo archivo) |
| 3 | Solo SICE-TAC donde aplica | `PropuestaTarifaAdapter.ProponerLineaAsync` (líneas 163-172) vía `IClavesSoloSicetacPort` |
| 4 | Tarifas vencidas marcadas y no propuestas | `PropuestaTarifaAdapter.CandidatoSapAsync` + `TarifarioSapAdapter.Seleccionar` |
| 5 | "Ganada" → archiva tarifa | `MarcarResultadoCommandHandler.Handle` |
| 6 | Ajuste auditado con autoría real | `AjustarVigenciaPlazaCommandHandler.Handle` |
| 7 | Total recalculado al cambiar conexos | `AgregarConexosCotizacionCommand` → `CotizacionCalculos.Recalcular` |

## Metodología

Acceso a datos reales: `sqlcmd` (Windows Auth, `-C` para el certificado
interno) permite conexión directa desde este entorno a **3 bases**, todas en
el servidor `tbsdatacenter`: **`dbSolucionesTI`** (tarifa SAP — usada para
casi todas las consultas de plaza de esta sesión), y **`dbInformesAutomatizados_Pruebas`**,
schema `dbo_NEXO_prueba` (la base propia de la app CabinaFletes — usada para
verificar Reglas 3, 5, 6 y 7 directamente). **`DWSice`** (piso SICE-TAC) vive
en otro servidor al que no se encontró acceso directo; esas consultas las
corrió el usuario por su conexión `consultascubos` en DBeaver. Las consultas
de la carpeta [`consultas/`](consultas/) documentan los 2 primeros caminos.

**Lecciones de esta sesión, para no repetir el tanteo:** `TIPOVEHICULO` en
`tblSAPTarifas` guarda el **ID numérico** del catálogo (`tblSAPTiposVehiculoTarifario.ID`),
no el texto de `CODIGO` — y viene vacío para líneas de negocio Puerto↔Interior
(tarifican solo por rango de peso, hallazgo ya conocido de EDA-2). Bogotá y
Medellín se codifican como zona de grupo (`ZTC_BOGOTA`/`ZTC_NORTE_BOGOTA`,
`ZTC_VABURRA`), no como zona individual (`ZC_BOGOTA` no existe en la tabla).
En SICE-TAC, filtrar siempre por `NOMBRETIPOCARGA = 'General'` (o
`'Contenedor vacío'` para líneas de devolución) — sin ese filtro la consulta
trae todos los tipos de carga mezclados.

Profundidad: cobertura pareja entre las 7 reglas (3-5 casos reales cada una),
ninguna en cero — ver la sección de Hotspot H3 más abajo sobre por qué no se
persiguió el 100% de profundidad en ninguna regla.

## Prueba adicional: pliego real subido en vivo a la app desplegada

Insumo: [`docs/ejemplo_licitacion.xlsx`](../../../docs/ejemplo_licitacion.xlsx)
(15 líneas reales, pliego "LIC-2026-0091"). En vez de reconstruir a mano qué
haría el motor, se sube este Excel a la app real y se compara lo que
realmente propuso contra lo que calculamos nosotros de antemano — más fuerte
que la reconstrucción manual porque ejercita el código real de punta a punta
(`ExcelSolicitudesAdapter` → `PropuestaTarifaAdapter` → `ReglaDeCosteo`).

**Orden seguido (a ciegas, para que la comparación no estuviera sesgada):**
1. Correr [`consultas/01-pliego-licitacion-esperado.sql`](consultas/01-pliego-licitacion-esperado.sql)
   — documento único y final (consolida lo que antes eran varios archivos de
   diagnóstico 07b-07h, ya eliminados): Parte A (tarifa de plaza SAP, 15
   líneas), Parte B (piso SICE-TAC, 15 líneas), Parte C (regla "solo
   SICE-TAC" — ya corrida contra `dbInformesAutomatizados_Pruebas.dbo_NEXO_prueba.reglas_solo_sicetac`:
   existen 3 reglas configuradas, ninguna coincide con las 15 líneas del
   pliego). Cada consulta trae su resultado real ya documentado como
   comentario.
2. Con esos resultados se completó el "esperado" — ver
   [`esperado-final-pliego.md`](../esperado-final-pliego.md) para la tabla
   final lista para comparar (Fuente + Valor por línea).
3. **Ahora:** subir `docs/ejemplo_licitacion.xlsx` a la app real y pasar lo
   que salga por línea (Fuente, ValorFlete).
4. Comparar fila por fila contra `esperado-final-pliego.md`.

Esta prueba también cubre de paso: la Regla 3 (Parte C revisa si alguna de
las 15 rutas ya tiene "solo SICE-TAC" activo — ninguna coincide, ver sección
de Reglas más abajo) y da 3 casos reales de devolución de contenedor vacío
(líneas 3, 4, 14) más un caso de extradimensionado (línea 13) y uno de carga
consolidada (línea 5) — buena variedad de casos borde en un solo pliego.

### Resultado del "esperado" — las 15 líneas

**Nota:** el primer cálculo manual de esta sección (dos vueltas, con
`CONFIGURACION` del vehículo y luego con código `2`) resultó equivocado en
el mecanismo — se descartó por completo, no quedan sus números aquí para
evitar confusión. El detalle de qué se asumió mal y por qué está en
[`esperado-final-pliego.md`](../esperado-final-pliego.md) sección 3
("Recomendaciones para leer/verificar este código").

**Lo confirmado y vigente:** se subió el Excel a la app real, se comparó
contra el PDF real de cotización obtenido
(`cotizacion-agencia-de-aduanas-jf-asociados-s-a-s-nivel-1-7667da46.pdf`), y
se confirmó (código real + valor exacto verificado en la base, 12/12
coincidencias) que el piso SICE-TAC real usado por la app **no depende del
vehículo/tipología de la línea** — siempre resuelve `CONFIGURACION='CA'`,
`NOMBRETIPOCARGA='General'`, `VIAESTANDAR='SI'` en `tblRutasSicetac`, vía
`TarifarioSapAdapter.SicetacOficialAsync`. También se confirmó que 5 rutas
comunes (Bucaramanga→Bogotá, Bogotá→Villavicencio, entre otras) sí tuvieron
tarifa de plaza SAP en el pasado — coherente con el 95,8% de vigencias ya
vencidas medido en HU-09.2 — pero para 3 de esas 5 (Cúcuta→Bogotá,
Manizales→Medellín, Neiva→Bogotá) la vencida que se encontró en un primer
chequeo era de otra combinación Línea de Negocio+Clase de Viaje, no la que
usa el dominio — con el combo correcto, esas 3 en realidad **nunca tuvieron
plaza** para su combinación exacta (ver el detalle línea por línea en
`esperado-final-pliego.md`, que es la fuente autoritativa de estos números).

**Ver [`../esperado-final-pliego.md`](../esperado-final-pliego.md) para la
tabla final correcta (caso — obtenido — comparación) y las 2 desviaciones
reales encontradas:**
1. **Posible violación de la regla del mayor en 4 líneas (1, 2, 3, 11):**
   existe tarifa de plaza SAP vigente hoy, mayor al piso SICE-TAC usado
   (hasta 4x mayor en la línea 1), pero la app propuso el valor más bajo
   (Sicetac) en vez del más alto (Tarifario). No confirmado si es un bug o
   si el buscador de plaza tampoco encuentra esas filas por la misma razón
   que ignora el vehículo en SICE-TAC — recomendado como issue para
   desarrollo.
2. **3 líneas con piso SICE-TAC disponible (8, 13, 15) no aparecieron en el
   PDF de cotización**, ni con valor ni marcadas visiblemente como
   pendientes — recomendado como issue para confirmar si quedaron en otro
   estado no visible en el PDF, o si es un fallo real.

## Tabla de la muestra (caso — esperado — obtenido)

### Regla 1 — regla del mayor
| caso (línea del pliego) | piso SICE-TAC | tarifa plaza | esperado (MAX) | obtenido (ValorFlete real) | ¿coincide? |
|---|---|---|---|---|---|
| 1: Buenaventura→Bogotá | $1.556.503 | $6.000.000 / $6.400.000 (vigente) | Tarifario, $6,0-6,4M | Sicetac, $1.556.503 | **No** — ver Desviación 1 |
| 4: Bogotá→Buenaventura (dev.) | $1.613.856 | sin plaza | Sicetac, $1.613.856 | Sicetac, $1.613.856 | Sí |

Ver [`esperado-final-pliego.md`](../esperado-final-pliego.md) sección 2 para
las 15 líneas completas.

### Regla 2 — piso inviolable
| caso | piso SICE-TAC vigente | ValorFlete real | ¿ValorFlete >= piso? |
|---|---|---|---|
| 5: Cali→Cartagena | $3.118.351 | $3.118.351 | Sí (igual al piso) |
| 10: Pereira→Buenaventura | $917.268 | $917.268 | Sí (igual al piso) |

Cumplido en las 12 líneas cotizadas del pliego — ninguna por debajo del piso.

### Regla 3 — solo SICE-TAC
**Sin caso disponible en el pliego actual.** `reglas_solo_sicetac` (base de
la app) tiene 3 reglas configuradas (Medellín→Bogotá 3S3; Cali→Medellín 3S3;
Barranquilla→Pereira 2S3) — ninguna coincide con las 15 líneas ya subidas.
Pendiente un caso de prueba dedicado con una de estas 3 rutas.

### Regla 4 — tarifas vencidas
| caso | tarifa SAP vencida (valor, vigencia) | ValorFlete real | ¿usó la vencida? |
|---|---|---|---|
| 9: Bogotá→Villavicencio | $1.300.000, venció 2026-08-31 | Sicetac, $867.043 | No |
| 6: Bucaramanga→Bogotá | $2.000.000, venció 2026-09-10 (6 días antes) | Sicetac, $1.271.020 | No |

### Regla 5 — Ganada archiva tarifa
| CotizacionId | NumLineas | NumTarifasArchivadas | ¿coincide 1:1? |
|---|---|---|---|
| 6113EF1E-A0EE-4916-9CAA-01AE2D74A860 | 1 | 1 | Sí |
| 16F96485-03D7-4689-845A-062CB8724D0A | 1 | 1 | Sí |

Confirmado en **las 54 cotizaciones reales marcadas "Ganada"** que existen
en la base — las 54 con 1:1 exacto, ninguna desincronizada.

### Regla 6 — ajuste auditado
| AjusteId | AjustadoPor | Motivo | ¿autoría real presente? |
|---|---|---|---|
| A8B757DE-C7EF-40EF-AEAC-E7D3A6DE2AE6 | Brayan Quintero (bquintero@boterosoto.com.co, rol AdminTarifas) | "prueba QA" | Sí — con entrada correspondiente en `audit_log` (acción "ajustar") |

Es el único ajuste real que existe en la base (1 fila).

### Regla 7 — recálculo de conexos
| CotizacionId | Suma líneas | Suma conexos | Total real | ¿se recalculó bien? |
|---|---|---|---|---|
| C1A463AF-FC53-49D2-8934-18122E493309 | $1.850.000 | $2.000 | $1.852.000 | Sí, exacto |
| 2E2CCA35-20F9-43AD-9BC4-25CB4A19DDB3 | $21.003.583 | $85.000 | $21.088.583 | Sí, exacto |
| 3C4C29D9-367D-4F87-AD5C-9BE796965831 | $1.476.620 | $18.000 | $1.494.620 | Sí, exacto |

## Invariante de piso (criterio de cierre: cero propuestas bajo el piso vigente)

**Cumplido en las 12 líneas del pliego que sí quedaron cotizadas:** en todas,
`Fuente=Sicetac` y `ValorFlete` es exactamente igual al piso SICE-TAC
(`CONFIGURACION='CA'`, `NOMBRETIPOCARGA='General'`) — por construcción, no
hay ninguna propuesta por debajo del piso. No se identificó ningún caso de
`Fuente=Tarifario` en la muestra real para verificar el lado "plaza nunca
por debajo del piso" del invariante — ver la Desviación 1 abajo, que es
justo la pregunta inversa (plaza disponible y mayor, pero no se usó).

## Desviaciones encontradas

**2 desviaciones reales, encontradas al comparar el pliego de prueba
(`docs/ejemplo_licitacion.xlsx`) subido a la app real contra el cálculo
esperado — ver el detalle completo en
[`esperado-final-pliego.md`](../esperado-final-pliego.md):**

1. **Candidata a violación de la regla del mayor, 4 líneas (1, 2, 3, 11):**
   existe tarifa de plaza SAP vigente (no vencida) con valor mayor al piso
   SICE-TAC que sí se usó — hasta 4x mayor en la línea 1
   (Buenaventura→Bogotá: plaza $6.000.000 vigente vs $1.556.503 propuesto).
   La app propuso el valor MÁS BAJO en vez del más alto. No se confirmó la
   causa raíz (podría ser que el buscador de plaza real tampoco encuentre
   esas filas, por una razón similar a por qué ignora el vehículo de la
   línea al buscar el piso SICE-TAC) — **recomendado abrir un issue** contra
   el milestone de esta HU para que desarrollo lo confirme. Sin `gh` CLI en
   este entorno, no se creó automáticamente.
2. **3 líneas con piso SICE-TAC disponible (8, 13, 15) no aparecieron en el
   PDF de cotización**, ni con valor ni marcadas visiblemente como
   pendientes de costeo — **recomendado abrir un issue** para confirmar si
   quedaron en otro estado no visible en el documento, o si es un fallo
   real que las descarta sin explicación.

## Hallazgo aparte (no bloquea esta HU)

No existe ningún comando en el código para editar vehículo/ruta/peso de una
línea de solicitud ya creada — "conexos" (regla 7) es un concepto específico
de servicios logísticos adicionales, no de esos atributos. Si el negocio
esperaba que cambiar vehículo/ruta/peso disparara un recálculo, esa ruta no
existe hoy en el código.

## Alcance real vs plan aprobado (Hotspot H3)

2 semanas ejecutadas vs 8-12 semanas estimadas en el plan original aprobado.
Con ese tiempo se priorizó cobertura pareja entre las 7 reglas en vez de
profundidad exhaustiva en unas pocas. Resultado real:

- **Reglas 2, 4, 5, 6, 7:** verificadas con evidencia real, cero
  desviaciones encontradas.
- **Regla 1:** verificada parcialmente — confirmado que el piso SICE-TAC se
  calcula y usa bien cuando es la única fuente (8/12 líneas del pliego); 1
  desviación real candidata cuando compite contra una plaza vigente (4/12
  líneas) — ver Desviaciones.
- **Regla 3:** no se pudo verificar — las 3 reglas "solo SICE-TAC"
  configuradas en el sistema no coinciden con ninguna línea del pliego de
  prueba usado. Requeriría un caso de prueba dedicado (una línea nueva con
  una de esas 3 rutas), no ejecutado por tiempo.
- No se llegó a: profundizar más de 3-5 casos por regla (ej. probar la
  Regla 5/7 con más de 3 cotizaciones, aunque el universo real permitía
  hacerlo — 54 y 35 respectivamente).

## Insumo Gherkin

[`docs/costeo/escenarios-reglas-negocio.feature`](../../../docs/costeo/escenarios-reglas-negocio.feature) —
7 escenarios, uno por regla, documentación viva (no hay runner de Gherkin
instalado en este proyecto, igual que `docs/contrato-integracion/escenarios.feature`).

## Fuera de alcance (declarado)

- No se montó ningún test de integración **automatizado** (xUnit u otro
  arnés que corra en CI) contra la BD corporativa real — toda la
  verificación de esta HU se hizo con consultas SQL manuales/dirigidas, no
  con una suite repetible. Sí se logró conectividad directa real a
  `dbSolucionesTI` y a la base propia de la app
  (`dbInformesAutomatizados_Pruebas`) — el bloqueo original de la issue #3
  quedó superado para esas 2 bases; `DWSice` sigue sin acceso directo desde
  este entorno.
- No se persiguió el 100% de profundidad en ninguna regla individual (ver
  Hotspot H3 arriba).
- La Regla 3 quedó sin verificar (ninguna línea del pliego coincide con las
  3 rutas "solo SICE-TAC" configuradas).
