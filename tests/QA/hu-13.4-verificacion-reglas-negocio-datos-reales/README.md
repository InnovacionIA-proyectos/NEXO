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

Acceso a datos reales: no hay conexión SQL/API directa disponible en el
entorno de desarrollo de esta sesión (mismo bloqueo documentado como issue #3
en `tests/QA/README.md`). Las consultas de la carpeta [`consultas/`](consultas/)
se prepararon para correrse a mano contra el ambiente real (DBeaver, 3 bases:
`CabinaFletes` la app, `dbSolucionesTI` tarifas SAP, `DWSice` piso SICE-TAC) y
los resultados se documentan en las tablas de abajo.

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

**Orden acordado (a ciegas, para que la comparación no esté sesgada):**
1. Correr [`consultas/07-pliego-licitacion-esperado.sql`](consultas/07-pliego-licitacion-esperado.sql)
   completo (resuelve zona SAP por ciudad, código SAP por vehículo, y trae la
   tarifa de plaza + piso SICE-TAC vigentes reales para las 15 líneas — los
   códigos de Línea de Negocio/Clase de Viaje ya vienen traducidos en el
   archivo, no hace falta resolverlos).
2. Con esos resultados, completo la columna "esperado" de la tabla de abajo
   (MAX entre plaza y piso, por línea).
3. **Recién ahí** subes `docs/ejemplo_licitacion.xlsx` a la app real y me
   pasas lo que salió por línea (Fuente, ValorFlete).
4. Comparamos fila por fila.

Esta prueba también cubre de paso: la Regla 3 (paso 5 de la consulta revisa
si alguna de las 15 rutas ya tiene "solo SICE-TAC" activo) y da 3 casos reales
de devolución de contenedor vacío (líneas 3, 4, 14) más un caso de
extradimensionado (línea 13) y uno de carga consolidada (línea 5) — variedad
que no tenía la muestra genérica de `01-regla-del-mayor-muestra-lineas.sql`.

## Tabla de la muestra (caso — esperado — obtenido)

> **Pendiente de llenar** con los resultados reales de correr `consultas/*.sql`.
> Formato por fila: id de línea/cotización real, qué dice la regla que
> debería pasar, y qué se obtuvo realmente al correr la consulta.

### Regla 1 — regla del mayor
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

> **Pendiente.** Se verifica sobre el universo real accesible (no solo la
> muestra de la tabla de la regla 1/2), cruzando cada línea costeada contra
> su piso SICE-TAC vigente a la fecha real. Universo total: ver el `COUNT(*)`
> de `01-regla-del-mayor-muestra-lineas.sql`.

## Desviaciones encontradas

> **Pendiente** — se llena solo si aparece algo real al correr las consultas.
> Si no aparece ninguna, esta sección queda igual que en HU-04.7/09.2:
> "Ninguna", con la explicación del criterio usado. Si aparece alguna, se
> documenta con: id de la línea/cotización, propuesta obtenida, regla
> esperada, y el título/cuerpo del issue a abrir contra el milestone de esta
> HU (no hay `gh` CLI en este entorno — el issue queda redactado, no creado
> automáticamente).

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
