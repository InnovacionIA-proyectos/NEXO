# PR #301 (#300) — AdminTarifas cubica, SICE-TAC solo del mes actual y tramos vacíos con 1h+1h

## Qué cambia

- `AdminTarifas` agregado a los roles autorizados de `POST`/`GET /api/cubicacion`.
- SICE-TAC: el piso solo se usa si el período más reciente cargado en DWSice es
  el del **mes y año actuales** (sin mes de gracia, hora de Colombia vía
  `clsPeriodoSicetac`). Si no lo es, no hay piso, se informa el motivo
  (justificación + `motivoPendiente` + aviso) y la línea queda en consulta
  completa; `aplicar-propuestas` ya no aplica en bloque líneas con
  `MotivoPendiente`.
- Horas por tramo (`clsResolucionHorasLinea`): solo una línea que ES
  devolución de contenedor arranca con 1h+1h por defecto; "retorna
  contenedor" en un viaje cargado YA NO cambia las horas de la línea (RN-03) —
  el tramo vacío de retorno se calcula aparte (piso SICE-TAC de viaje vacío,
  1h+1h, mismo vehículo), reemplazando la vieja tarifa de plaza "línea 6
  exclusiva". El levante estima con 2h.
- Sin cambios de esquema.

## Qué se verificó

**Rama (`DEPLOY`) estaba desactualizada respecto a `QA`** (el merge-base era
anterior al merge de la PR #299): mergeé `origin/QA` localmente sin conflictos
antes de verificar (commit local, no pusheado — ver nota abajo).

**Código revisado línea por línea:**
- `clsPeriodoSicetac` (función pura: período AAAAMM en hora de Colombia
  UTC-5 fijo, sin horario de verano; `MotivoNoVigente` null si no hay matriz
  cargada en absoluto, solo informa cuando SÍ hay una pero quedó vieja).
- `clsResolucionHorasLinea.Resolver`: el 1h+1h de default ahora depende de
  `LineaNegocio == DevolucionContenedor`, no de la bandera `RetornaContenedor`
  (que antes mezclaba ambos casos).
- `clsPropuestaTarifaAdapter`: `RetornoContenedorAsync` (piso SICE-TAC viaje
  vacío destino→ciudad devolución, 1h+1h), `PeriodoSiceAsync` (clave de caché
  incluye el período actual para no arrastrar el valor del mes anterior;
  `Periodo <= actual` para que un período futuro cargado no tape al vigente),
  y el ensamblado de `justificacion`/`motivoPendiente` cuando no hay piso por
  período.
- `clsAplicarPropuestasCommand`: nuevo guard `if (p.MotivoPendiente is not
  null) continue` antes de aplicar en bloque — correcto, una línea sin piso
  contra qué comparar no debe auto-aplicarse.
- Rol `AdminTarifas` agregado al `[Authorize]` de `clsCubicacionController`
  (cambio de una línea, mismo patrón que otros endpoints ya verificados en
  PRs anteriores de esta sesión — sin test dedicado nuevo para esta
  combinación de rol/endpoint puntual, hallazgo no bloqueante, ver abajo).

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta, 0
errores (1 warning preexistente ajeno a esta PR).

**Suite completa contra la base real de pruebas (incluye las 6 pruebas nuevas
contra DWSice real de `clsSicetacPeriodoYTramosVaciosTests`):**
- `Nexo.Domain.Tests`: **183/183**.
- `Nexo.Application.Tests`: **393/393**.
- `Nexo.Api.Tests`: primera corrida sin Cubicador → **370/378** (8 omitidas:
  2 `clsMailSenderRealTests` + 6 E2E de Cubicador, igual a lo declarado por la
  PR). Armé el Cubicador real localmente de nuevo (código actual de `QA`,
  esta PR no lo toca) → **376/378, solo 2 omitidas** (las de MailSender,
  preexistentes) — **0 errores**, las 6 E2E del Cubicador corrieron de verdad
  y pasaron.

Coincide exactamente con lo declarado por la PR (Domain 183, Application 393,
Api 370/8 omitidas sin Cubicador/secretos).

## Hallazgo no bloqueante

El rol `AdminTarifas` agregado a `POST`/`GET /api/cubicacion` no tiene una
prueba automatizada dedicada que verifique explícitamente que ese rol puede
usar el endpoint (ni que los roles que no deberían, no puedan). Es un cambio
de una línea siguiendo el mismo patrón `[Authorize(Roles = "...")]` ya usado y
probado en otros controladores de este proyecto, así que el riesgo es bajo,
pero queda sin cobertura directa.

## Limpieza

- `docker compose down -v` (contenedores y volumen del Cubicador local
  eliminados).
- `cubicador/.env` temporal borrado.

## Nota operativa — rama `DEPLOY`

Esta PR tiene como rama base/head `DEPLOY` (igual que la PR #264). El merge
de `QA` para dejar la rama al día con la que se verificó se hizo **solo en
local**, sin push, para no disparar el pipeline real de despliegue sin
autorización explícita.

## Resultado

Sin hallazgos bloqueantes. Un hallazgo menor (falta de test dedicado para el
nuevo rol en `/api/cubicacion`), no bloqueante.
