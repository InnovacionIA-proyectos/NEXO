# PR #303 (#302) — Pruebas de roles de POST y GET /api/cubicacion (incluye AdminTarifas)

## Qué incluye

Resuelve el issue #302 (hallazgo de QA sobre la PR #301): cobertura directa de
los roles de `POST`/`GET /api/cubicacion`.

- `clsCubicacionRolesTests` (nuevo): los 4 roles permitidos (`Comercial`,
  `CoordinadorFletes`, `AdminTarifas`, `Admin`) pasan la autorización (se leen
  como `400` porque el cuerpo/consulta está vacío, antes de llamar al
  Cubicador); `ServiciosLogisticos`/`LiderProceso` → `403`; sin sesión →
  `401`. Son pruebas solo de autorización, no llaman al Cubicador.
- Commit aparte, sin relación con el hallazgo: fix de la limpieza de
  `clsSicetacPeriodoYTramosVaciosTests.Limpiar` (ahora de mejor esfuerzo,
  atrapa `SqlException` del `DELETE`) — responde a un fallo real de CI del
  2026-10-15 (timeout de 30s por bloqueo en la base compartida, con las
  aserciones de la prueba ya pasadas antes del timeout).

## Qué se verificó

**Rama ya al día con `QA`** (ramificada después del merge de la PR #301, sin
necesidad de merge).

**Código revisado línea por línea:** `clsCubicacionRolesTests` (usa
`fixture.Client(rol)`/`fixture.Anon()` ya existentes, cuerpo/consulta vacíos
para no tocar el Cubicador) y el `try/catch(SqlException)` en `Limpiar()` del
otro archivo (limpieza de mejor esfuerzo, no oculta un fallo real de
aserción — el `try` envuelve solo el `DELETE`, no el cuerpo de la prueba).

**Build**: compilación correcta, 0 errores (1 warning preexistente ajeno a
esta PR).

**Suite completa contra la base real de pruebas:**
- `Nexo.Domain.Tests`: **183/183**.
- `Nexo.Application.Tests`: **393/393**.
- `Nexo.Api.Tests`: **381/389** (8 omitidas: 2 `clsMailSenderRealTests` + 6 E2E
  de Cubicador sin secretos/stack local, igual patrón que siempre) — **0
  errores**. Las 11 pruebas nuevas de `clsCubicacionRolesTests` en verde,
  coincide con lo declarado por la PR.

**Verificación independiente de la afirmación de la PR** ("quitando
AdminTarifas del `[Authorize]` fallan los casos de ese rol"): no se tomó la
palabra del autor — se repitió la mutación de forma independiente:
- Quité `AdminTarifas` del `[Authorize(Roles = ...)]` de
  `clsCubicacionController` (ambos endpoints) temporalmente.
- Corrí `clsCubicacionRolesTests` sola: **2 fallos, exactamente los 2 casos
  de `AdminTarifas`** (`403 Forbidden` en vez de `400 BadRequest`), los otros
  9 casos siguieron en verde.
- Revertí el cambio (`git checkout --`) y confirmé `git status` limpio.

Esto confirma que la prueba nueva sí detecta el problema que motivó el issue
#302, tal como afirma la PR.

## Resultado

Sin hallazgos. El issue #302 queda resuelto — se cierra como verificado.
