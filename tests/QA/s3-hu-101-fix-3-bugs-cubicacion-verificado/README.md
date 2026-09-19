# QA — S3-HU-10.1: verificación independiente del cierre de los 3 bugs de Cubicación

**PR verificado:** #141 (cierra issues #131, #132, #133). **Autor:** bquinterobtst.
**Fecha:** 2026-09-10. **Verificado por:** Kevin Cano (QA).

El autor ya dejó su propia evidencia en `tests/QA/s3-hu-10-tres-bugs-cubicacion/README.md`.
Este README es la verificación **independiente** de QA, repitiendo exactamente los
mismos 3 escenarios que originalmente encontraron los bugs (documentados en
`tests/QA/s3-hu-01-02-03-061-cubicacion-por-colas/README.md`), sin partir de la
evidencia del autor.

Rama ya estaba al día con `QA` (merge-base = tip). Sin cambios en `.NET`.

## Scripts de esta carpeta

Los mismos 3 escenarios de la carpeta original, re-ejecutados contra el código ya
corregido (por eso reportan "OK: bug corregido" en vez de "BUG PRESENTE"):

| Script | Verifica el cierre de |
|---|---|
| [`verify-fix-worker-atascado.mjs`](verify-fix-worker-atascado.mjs) | Bug 1 (issue #131) |
| [`verify-fix-sku-nulo.mjs`](verify-fix-sku-nulo.mjs) | Bug 2 (issue #132) |
| [`verify-fix-eliminar-plan-optimizado.mjs`](verify-fix-eliminar-plan-optimizado.mjs) | Bug 3 (issue #133) |

## Suites automatizadas

| Suite | Resultado |
|---|---|
| Cubicador `pytest` | ✅ 103/103 (antes 84 — 19 tests nuevos) |

Revisé el diff de los 3 arreglos (`worker/main.py`, `api/dtos/plan_cargue.py`,
`plan_cargue_repo.py`) línea por línea antes de probar — los 3 usan `.get()` en vez de
indexación directa donde corresponde, y el borrado en cascada respeta el mismo orden
(colocaciones → ítems/plan) que ya usaba `eliminar_item()`.

## Verificación en vivo — repetí los 3 escenarios originales

**Bug #131 (worker atascado):** corrí `ci_docker_smoke.py` **3 veces seguidas** sin
purgar la cola entre corridas (el criterio de cierre del PR). Las 3 pasaron limpio, y
tras las 3 la cola `cubicador.optimizar` quedó en **0 mensajes unacknowledged**
(antes de este fix, la segunda corrida ya dejaba el worker atascado).

**Bug #132 (SKU nulo):** mandé un ítem con `"sku": null` explícito contra la API real
de Cubicador — respuesta `200`, `sku` normalizado a `""`, el plan se quedó en
`"borrador"` (antes: 3 reintentos, DLQ, plan en `"error"`).

**Bug #133 (borrado con FK huérfana):** creé un plan, agregué un ítem, lo optimicé
(2 colocaciones reales creadas), y lo borré — `204` (antes: `ForeignKeyViolationError`
tras agotar reintentos). Confirmé **0 filas huérfanas** en `colocaciones_cargue` e
`items_plan_cargue` para ese plan después del borrado.

## Bugs encontrados

Ninguno. Los 3 quedaron cerrados tal como los describe el PR, verificado de forma
independiente contra el stack real (Postgres + RabbitMQ + api + worker), no solo
contra la evidencia o los tests del propio autor.

## Decisión

Se aprueba el merge.
