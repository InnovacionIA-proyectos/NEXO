# Decisión — paginación en los 8 endpoints de listado candidatos

**Estado:** aceptada · **Fecha:** 2026-09-18 · **Issue:** #156 · **Relacionada:** `administracion_embarqa_nexo#122` (frontend, agrega los controles de paginación en las pantallas que lo necesiten según esta decisión)

## Qué se pidió

Frontend (`administracion_embarqa_nexo#122`) identificó 8 pantallas de listado
que cargan todo el resultado en un solo request, sin ningún parámetro de
paginación en el contrato. Frontend no tiene visibilidad del volumen real de
datos ni de su tasa de crecimiento — se pidió a este lado revisar los 8 con
datos reales y paginar solo los que de verdad lo justifiquen, siguiendo el
patrón ya validado de `GET /api/servicios-conexos` (`page`+`pageSize`,
respuesta `{ items, total }`).

## Metodología

Medición en vivo contra el ambiente de pruebas (`COUNT(*)` real por endpoint,
no estimaciones). No se midió contra la base de producción real por no tener
acceso directo desde esta sesión — para los 5 endpoints que consultan tablas
propias de NEXO (`solicitudes`, `tarifas_historicas`, `reglas_solo_sicetac`,
`planes_cargue`, `vehiculos_cubicacion`) esto es una limitación real: esa base
de pruebas se resiembra/limpia periódicamente, así que sus números NO son
representativos del crecimiento real en producción — se documenta igual la
decisión tomada con la información disponible, dejando explícito el supuesto
para quien la revise después. Para los 3 endpoints que consultan bases
corporativas EXTERNAS (Data Warehouse SICE-TAC, datacenter SAP), en cambio, la
medición SÍ es representativa: esas bases son compartidas y reales, no se
resiembran por ciclo de pruebas de NEXO.

## Resultado medido

| Endpoint | Tabla | Total medido | Filas visibles hoy (antes de este fix) | Naturaleza |
|---|---|---:|---:|---|
| `GET /biblioteca-sicetac` | `DWSice.tblRutasSicetac` (externa) | **2.148.120** | 50 | DW corporativo real, filtrado por periodo |
| `GET /biblioteca-sap-plaza` | `dbsolucionesTI.tblSAPTarifas` (externa) | **22.793** | 50 | Datacenter corporativo real |
| `GET /solicitudes` | `solicitudes` (NEXO) | 190 | 190 (todo) | Transaccional — número de pruebas, no representativo del crecimiento real |
| `GET /planes-cargue` | `planes_cargue` (NEXO) | 37 | 37 (todo) | Transaccional — ídem |
| `GET /biblioteca-sap-historico` | `dbsolucionesTI.tblSAPTarifasLog` (externa) | 0 | 0 | Externa, pero vacía en el momento de la medición |
| `GET /reglas-solo-sicetac` | `reglas_solo_sicetac` (NEXO) | 0 | 0 | Catálogo de alta manual (Admin) |
| `GET /tarifas-historicas` | `tarifas_historicas` (NEXO) | 10 | 10 (todo) | Transaccional — número de pruebas, no representativo |
| `GET /vehiculos` | `vehiculos_cubicacion` (NEXO) | 3 | 3 (todo) | Catálogo de alta manual (Admin, rol restringido) |

## Decisión

### Se paginan (2): SICE-TAC y plaza SAP

Con datos reales de bases externas (no sujetas al reseteo del ambiente de
pruebas), la truncación **ya está pasando hoy**, no es un riesgo a futuro: en
SICE-TAC, **2.148.070 filas** (el total menos las 50 visibles) no las puede
ver ningún usuario, sin ningún aviso de que existen — el campo `total` ya
viaja en el JSON de respuesta (`MatrizSicetacResultDto.Total`,
`SapHistoricoResultDto.Total`), pero hasta este fix no había forma de pedir la
página siguiente. Paginación del lado del cliente no es viable para 2.1M
filas — tiene que ser `page`/`Skip` real en el backend.

Implementado: parámetro `page` (1-based, opcional, default 1) agregado junto
al `top` (pageSize) que ya existía en ambos endpoints, con `Skip((page-1)*top)`
antes del `Take(top)` ya existente. Cambio aditivo — omitir `page` preserva el
comportamiento actual exacto. Ver `CONTRATO.md` §"Biblioteca SICE-TAC"/
"Biblioteca plaza SAP" para el contrato completo.

### NO se paginan por ahora (6)

- **`reglas-solo-sicetac` y `vehiculos`**: catálogos de alta MANUAL por un
  Admin (0 y 3 filas medidas). No tienen ningún mecanismo de crecimiento
  ligado a una operación de negocio recurrente — nadie da de alta cientos de
  reglas o vehículos a mano. Bajo cualquier lectura razonable de "volumen y
  crecimiento reales", no lo justifican.
- **`solicitudes`, `planes-cargue`, `tarifas-historicas`**: transaccionales
  por naturaleza (crecen solos con cada operación de negocio), pero el único
  número disponible (190 / 37 / 10) viene de una base de pruebas que se
  resiembra periódicamente — no sirve para proyectar el crecimiento real. Se
  deja sin paginar POR AHORA, con la salvedad explícita de que esta decisión
  se apoya en un dato incompleto (falta el número de producción real) y debe
  revisarse si algún día se puede medir contra producción, o si el equipo de
  operación reporta que el listado empieza a sentirse lento/pesado.
- **`biblioteca-sap-historico`**: externa (no sujeta a reseteo de pruebas),
  pero midió 0 filas en el momento de la medición — sin evidencia de volumen
  que lo justifique hoy. Comparte el mismo adapter (`SapHistoricoAdapter`,
  método interno `BuscarCoreAsync`) que `biblioteca-sap-plaza`, así que si
  más adelante se confirma volumen real en el log histórico, agregar `page`
  ahí es un cambio pequeño (el `Skip` ya vive en el método compartido; solo
  falta exponer el parámetro en `BuscarAsync`/`ISapHistoricoPort` y en el
  controller, siguiendo exactamente el mismo patrón que se usó para plaza).

### Motivo para no volver a revisar esto sin evidencia nueva

Para los 6 no paginados, la condición para reabrir esta decisión es
específica y verificable, no un "quizás algún día":

1. `reglas-solo-sicetac` / `vehiculos`: solo si cambia la forma en que se
   cargan (ej. una importación masiva en lote) — hoy es alta 1 a 1 por un
   Admin.
2. `solicitudes` / `planes-cargue` / `tarifas-historicas`: cuando se pueda
   medir el conteo real contra producción (ver consultas de conteo usadas
   para esta medición, reproducibles contra cualquier ambiente), o si aparece
   un reporte concreto de lentitud.
3. `biblioteca-sap-historico`: si una medición nueva contra el datacenter
   corporativo real muestra un volumen material (a diferencia de SICE-TAC/
   plaza, que ya lo mostraron).
