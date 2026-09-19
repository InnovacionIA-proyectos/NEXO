# QA — traer a `QA` el arreglo del worker que quedó aislado en `Pre-Despliegue-BACKEND`

**Commit original:** `774de8c` (`fix(cubicador): el reporte de un fallo ya no puede atascar el worker`).
**Autor:** Bran / bquintero.
**Fecha:** 2026-09-15. **Verificado por:** Kevin Cano (QA).

## Contexto

Al revisar `Pre-Despliegue-BACKEND` (por un desacuerdo sobre por qué las pruebas
de integración locales daban 69 fallas), encontré que esa rama tenía un commit
real que **nunca llegó a `QA` ni a `main`** — comiteado directo a
`Pre-Despliegue-BACKEND`, sin pasar por PR hacia `QA`.

Es un arreglo genuino, distinto del bug #131 que cerramos en S3-HU-10.1 (PR #141):
- **#131 (ya en QA):** `cuerpo["planId"]` con indexación directa — `KeyError` si
  el mensaje no traía esa clave.
- **Este (nuevo):** cuando `guardar_item` falla contra un plan inexistente
  (`ForeignKeyViolationError` durante el flush), la sesión de SQLAlchemy queda
  **envenenada** — cualquier uso posterior lanza `PendingRollbackError`. El
  manejador de errores llamaba a `marcar_error()` sobre esa misma sesión,
  explotaba antes del `ack()`, y el mensaje quedaba `unacknowledged` para
  siempre — el mismo síntoma del #131, causa distinta.

Confirmado real en el propio ambiente de pre-despliegue el 14-sep (no es
hipotético): 2 mensajes así dejaron el broker con `messages_unacknowledged=2` y
8 mensajes más encolados detrás sin procesar.

No se trabajó parado en `Pre-Despliegue-BACKEND` (esa rama está 35 commits
atrás de `QA` — hubiera sido el mismo problema de rama vieja que ya conocemos,
al revés). Se trajo el commit puntual con `git cherry-pick` sobre una rama
nueva basada en `QA` actual.

## Suites automatizadas

| Suite | Resultado |
|---|---|
| Cubicador `pytest` | ✅ 109/109 (antes 103 — 6 tests nuevos, dedicados a esta clase de fallo) |

Sin cambios en `.NET` (confirmado con `git diff --name-only` contra `src/`/`tests/`).

## Script de esta carpeta

[`verify-fix-sesion-envenenada.mjs`](verify-fix-sesion-envenenada.mjs) — reproduce
el escenario real contra el stack real (Postgres + RabbitMQ + worker): publica 2
mensajes `guardar_item` contra un `planId` inexistente (el disparador exacto de
la sesión envenenada) directo a la cola, y confirma que el worker no queda
atascado.

**Resultado real de la corrida:**
- Ambos mensajes dispararon el `ForeignKeyViolationError` real (confirmado en el
  log del worker).
- Los 2 terminaron correctamente en `cubicacion.dlq` — **0 mensajes
  `unacknowledged`**.
- El worker siguió funcionando después: se creó un plan nuevo sin problema
  inmediatamente después del incidente.

## Decisión

Se aprueba el merge a `QA`.
