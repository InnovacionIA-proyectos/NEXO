# PR #292 (S5-HU-19) — Motor de decisión de tarifa (cierra #281-#284)

## Qué cambia

- **#281**: plaza e histórico ahora se resuelven en orden SAP oficial → SAP
  tolerante (por zona) → tabla propia de NEXO, ganando la primera **vigente**
  (antes la propia tapaba a SAP sin competir). Corrige además un bug real: una
  plaza **vencida** en "oficial" dejaba de buscar en las otras dos fuentes —
  una vigente en SAP tolerante o en la propia nunca se encontraba si oficial
  traía algo vencido primero.
- **#282**: `clsReglaDeCosteo.DecidirFlujo` (nuevo, puro) con 3 pasos
  (`ModoDecision`: `SoloSicetac`/`PlazaVigenteMayor`/`ConsultaCompleta`);
  auto-costeo ya no guarda precio cuando el modo es `ConsultaCompleta`.
- **#283**: validaciones server-side (flete bajo el piso SICE-TAC, histórico
  de la propuesta, entrega bloqueada con líneas pendientes).
- **#284**: solo documentación (investigación de una desviación), sin cambio
  de motor.

## Qué se verificó

**Merge real contra `QA`**: sin conflictos.

**Código revisado línea por línea:** `DecidirFlujo` (los 3 pasos, con el
orden exacto que describe la PR) y el reordenamiento de fuentes en
`clsPropuestaTarifaAdapter` (SAP oficial → tolerante → propia, el histórico
se salta cuando `PlazaVigenteMayor` ya va a ganar).

**Suite completa contra la base real de pruebas (2026-10-13):**
- `Nexo.Domain.Tests`: 157/157 — incluye `clsDecidirFlujoTests` con los 10
  casos exactos que describe la PR (los 3 pasos × todas las combinaciones de
  presencia/ausencia de piso, plaza vigente/vencida, histórico).
- `Nexo.Application.Tests`: 277/277.
- `Nexo.Api.Tests`: primera corrida sin Cubicador configurado → 7 fallos: 6
  eran tests E2E contra el Cubicador real (`ServiceUnavailable`, mi entorno
  no lo tenía levantado — mismo patrón que en la PR #291) y **1 fallo real a
  investigar**, ver abajo.

**Hallazgo investigado — NO es un bug de esta PR:**
`clsPropuestaPlazaPropiaTests.Linea_con_una_tarifa_SAP_vigente_para_la_zona_le_gana_a_la_tarifa_de_plaza_propia`
esperaba `3.000.000` (una tarifa SAP tolerante que el propio test documenta
como "vigente hasta verificado contra tblSAPTarifas") y obtuvo `3.300.000`
(el valor de la tabla propia). Consulté `dbo.tblSAPTarifas` directo en la
base real de pruebas: la fila que el test espera (`ZC_BUENAVENTURA→ZTC_PEREIRA`,
vehículo SAP `2`, valor `3.000.000`) tiene **`VIGENCIA = 2026-08-31 23:59:59`**
— ya venció (hoy es 2026-10-13). Confirmé con una consulta `WHERE VIGENCIA >
SYSDATETIME()` que no existe ninguna fila vigente para ese vehículo/zona hoy.
**El código está haciendo exactamente lo correcto** (cae a la tabla propia
cuando SAP no tiene nada vigente) — el test quedó frágil por tener
hardcodeada una fecha de vigencia real externa que ya pasó. La PR declara
"237 OK/2 omitidas/0 fallos" porque la autora lo corrió antes de que esa fila
venciera (el corte es a fin de mes, ~6 semanas antes de hoy).

**Segunda corrida, con el Cubicador real armado localmente** (mismo
procedimiento que en PR #291: rebuild del stack desde el código ya en `QA`,
`preparar_catalogo_local.py`, `aplicar_sql.py` para las tablas propias del
motor 3D):
- `Nexo.Api.Tests`: **236/239 superadas, 2 omitidas (preexistentes), 1 con
  error** (el hallazgo de arriba, ya diagnosticado) — los 6 E2E del
  Cubicador ahora pasan. Total 236+2+1=239, coincide con el total que
  reporta la PR.

## Resultado

Sin hallazgos bloqueantes de código. El único fallo real es una fragilidad
de test ya diagnosticada (dependencia de una fecha de vigencia SAP que
expiró por calendario) — no afecta la lógica de negocio, que funciona
correctamente. Vale la pena que el equipo sepa que ese test específico va a
necesitar refrescar su dato de referencia (u obtener el valor de la fila
vigente en tiempo de ejecución en vez de hardcodearlo) para no quedar
permanentemente rojo.
