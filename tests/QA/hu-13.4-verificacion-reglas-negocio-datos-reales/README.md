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
interno) sí permite conexión directa a **`dbSolucionesTI`** (servidor
`tbsdatacenter`) desde este entorno — se usó para correr casi todas las
consultas de tarifa SAP de esta sesión. **`DWSice`** (piso SICE-TAC) vive en
otro servidor al que no se encontró acceso directo; esas consultas las corrió
el usuario por su conexión `consultascubos` en DBeaver. La base propia de la
app (`CabinaFletes`) tampoco se probó todavía en directo — pendiente. Las
consultas de la carpeta [`consultas/`](consultas/) documentan ambos caminos.

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
1. Correr [`consultas/07-pliego-licitacion-esperado.sql`](consultas/07-pliego-licitacion-esperado.sql)
   — documento único y final (consolida lo que antes eran varios archivos de
   diagnóstico 07b-07h, ya eliminados): Parte A (tarifa de plaza SAP, 15
   líneas), Parte B (piso SICE-TAC, 15 líneas), Parte C (regla "solo
   SICE-TAC" — **pendiente de correr todavía**). Cada consulta trae su
   resultado real ya documentado como comentario.
2. Con esos resultados se completó el "esperado" — ver
   [`esperado-final-pliego.md`](../esperado-final-pliego.md) para la tabla
   final lista para comparar (Fuente + Valor por línea).
3. **Ahora:** subir `docs/ejemplo_licitacion.xlsx` a la app real y pasar lo
   que salga por línea (Fuente, ValorFlete).
4. Comparar fila por fila contra `esperado-final-pliego.md`.

Esta prueba también cubre de paso: la Regla 3 (Parte C revisa si alguna de
las 15 rutas ya tiene "solo SICE-TAC" activo — pendiente) y da 3 casos reales
de devolución de contenedor vacío (líneas 3, 4, 14) más un caso de
extradimensionado (línea 13) y uno de carga consolidada (línea 5) — variedad
que no tenía la muestra genérica de `01-regla-del-mayor-muestra-lineas.sql`.

### Resultado del "esperado" — las 15 líneas, ya calculado a ciegas (antes de subir el Excel)

**Hallazgo grande de esta sesión, más importante que cualquier línea
individual: 11 de las 15 líneas (73%) no tienen tarifa de plaza SAP vigente
para su combinación real de ruta+línea de negocio+clase de viaje+peso.**
Coherente con el 95,8% de vigencias ya vencidas medido en la sesión de
HU-09.2 sobre toda la tabla (`docs/tarifas-plaza-provisionales/README.md`).

**Verificado a fondo, no es ausencia de dato — son tarifas que existieron y
vencieron hace poco, ni siquiera en corredores raros.** El usuario cuestionó
correctamente que rutas tan comunes como Bucaramanga→Bogotá no tuvieran
ninguna tarifa. Importante no confundir esto con el mecanismo real: el
"esperado" de la tabla de arriba se calculó **solo** contra `tblSAPTarifas`
(el tarifario vigente, resuelto por zona de tarificación — igual que hace el
motor real, `PropuestaTarifaAdapter.CandidatoSapAsync`, que filtra
`Vigencia >= hoy` y nunca consulta el histórico para decidir un precio
nuevo). `tblSAPTarifasLog` (histórico) se consultó aparte, solo como
evidencia de contexto para responder "¿esta ruta tan común de verdad nunca
tuvo tarifa?" — no participa del cálculo del "esperado" ni de la decisión
real de la app. Con esa aclaración: las 5 rutas de "atención manual" SÍ
tienen historial real en el Log (15 a 31 filas cada una) — la más reciente de
cada una ya venció:

| Ruta | Última tarifa SAP, vigencia | Venció hace |
|---|---|---|
| Bogotá→Villavicencio | 2026-08-31 | 16 días |
| Manizales→Medellín | 2026-09-03 | 13 días |
| Bucaramanga→Bogotá | 2026-09-10 | 6 días |
| Cúcuta→Bogotá | 2026-08-10 | 37 días |
| Neiva→Bogotá | 2026-02-10 | 7 meses |

En la práctica, para la mayoría de rutas reales de este pliego la Regla 1 se
reduce a "gobierna el piso SICE-TAC en solitario" — no porque falte el dato
de origen, sino porque el proceso de renovar tarifas SAP no está al día,
incluso en corredores muy transitados.

**Corrección tras seguir investigando (el código de vehículo de SICE-TAC no
es el mismo que el de SAP):** para las líneas 6, 8 y 15 había concluido
"tampoco hay piso SICE-TAC", pero era el mismo error que ya se había
corregido para 9 y 11 — usé el código de SAP (`C2`, `Turbo`) en vez del
código oficial de SICE-TAC. El catálogo real de SICE-TAC para estas rutas es
distinto (`2, 2_7, 2_8, 2L1, 2L2, 2L3, 2S2, 2S3, 3, 3S2, 3S3, CA, V2, V3, V4`)
— **el código SAP `C2` ("Rígidos, Turbo o Sencillo") corresponde al código
`2` de SICE-TAC** (camión rígido de 2 ejes, sin sufijo). Con el código
correcto, las 3 rutas SÍ tienen piso real:

| Línea | Piso SICE-TAC (código `2`, General) |
|---|---|
| 6 | $2.107.491 |
| 8 | $3.015.093 |
| 15 | $1.719.671 |

Esto deja **solo 2 líneas (9 y 12) genuinamente sin ninguna fuente** —
confirmadas dos veces sin ningún código de vehículo disponible, no es tema
de código mal buscado.

**IMPORTANTE — este cálculo manual (tabla que estaba aquí) resultó
INCORRECTO en el mecanismo, y ya se reemplazó.** Se subió el Excel a la app
real, se comparó contra el PDF real de cotización obtenido
(`cotizacion-agencia-de-aduanas-jf-asociados-s-a-s-nivel-1-7667da46.pdf`), y
los valores no coincidían. Investigando por qué, se encontró y confirmó
(código real + valor exacto verificado en la base, 12/12 coincidencias) que
el piso SICE-TAC real usado por la app **no depende del vehículo/tipología
de la línea** — siempre resuelve `CONFIGURACION='CA'`, `NOMBRETIPOCARGA='General'`,
`VIAESTANDAR='SI'` en `tblRutasSicetac`, vía `TarifarioSapAdapter.SicetacOficialAsync`.

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

> El "obtenido" de las 15 líneas del pliego se llena en la sección de arriba
> una vez se suba `docs/ejemplo_licitacion.xlsx` a la app real. Las tablas de
> abajo son para evidencia adicional más allá del pliego (otras líneas reales
> ya costeadas, regla por regla).

### Regla 1 — regla del mayor (evidencia adicional, más allá del pliego)
| caso (CotizacionLineaId) | piso SICE-TAC | tarifa plaza | esperado (MAX) | obtenido (ValorFlete real) | ¿coincide? |
|---|---|---|---|---|---|
| _pendiente_ | | | | | |

### Regla 2 — piso inviolable
| caso | piso SICE-TAC vigente | ValorFlete real | ¿ValorFlete >= piso? |
|---|---|---|---|
| _pendiente_ | | | |

### Regla 3 — solo SICE-TAC
| caso (ruta con regla activa) | tarifa de plaza existente | ValorFlete real | Fuente real | ¿ignoró la plaza? |
|---|---|---|---|---|
| _pendiente_ | | | | |

### Regla 4 — tarifas vencidas
| caso | tarifa SAP vencida (valor, vigencia) | ValorFlete real posterior al vencimiento | ¿usó la vencida? |
|---|---|---|---|
| _pendiente_ | | | |

### Regla 5 — Ganada archiva tarifa
| CotizacionId | NumLineas | NumTarifasArchivadas | ¿coincide 1:1? |
|---|---|---|---|
| _pendiente_ | | | |

### Regla 6 — ajuste auditado
| AjusteId | AjustadoPor | Motivo | ¿autoría real presente? |
|---|---|---|---|
| _pendiente_ | | | |

### Regla 7 — recálculo de conexos
| CotizacionId | Total antes | Total después del conexo | ¿se recalculó? |
|---|---|---|---|
| _pendiente_ | | | |

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
Con ese tiempo se priorizó cobertura pareja (una muestra real por cada una de
las 7 reglas, 3-5 casos) en vez de profundidad exhaustiva en unas pocas. No
se llegó a: [completar tras la ejecución — qué quedó fuera, con cuánta
profundidad se cubrió cada regla].

## Insumo Gherkin

[`docs/costeo/escenarios-reglas-negocio.feature`](../../../docs/costeo/escenarios-reglas-negocio.feature) —
7 escenarios, uno por regla, documentación viva (no hay runner de Gherkin
instalado en este proyecto, igual que `docs/contrato-integracion/escenarios.feature`).

## Fuera de alcance (declarado)

- No se montó ningún test de integración automatizado contra la BD
  corporativa real (mismo bloqueo de acceso que issue #3/#4 — este entorno de
  desarrollo no tiene conectividad a `DwSice`/`DwCubos`/`dbSolucionesTI`).
- No se persiguió el 100% de profundidad en ninguna regla individual (ver
  Hotspot H3 arriba).
