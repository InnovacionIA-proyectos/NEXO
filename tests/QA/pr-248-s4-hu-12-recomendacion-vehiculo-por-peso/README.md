# PR #248 — S4-HU-12: recomendación de vehículo por peso, con líneas de flota reales (#215, #216, #217 — solo Cubicador)

## Qué se verificó

Cubre, solo del lado de `cubicador/` (el resto queda documentado como
contrato pendiente para NEXO/frontend, ver `cubicador/CONTRATO.md` §9-§10):

- **#215 — Catálogo único de vehículos**: se eliminó la constante duplicada
  del motor rápido; `vehiculos_cubicacion` (tabla) queda como única fuente,
  identificada por `codigo` en vez de `nombre` (el nombre lo puede editar
  el usuario, `codigo` no).
- **#216 — Recomendación por peso**: `techo(peso / capacidad)` con reparto
  equilibrado (heurística LPT), reemplaza el reparto voraz anterior
  (primero al tope, último con lo que sobra).
- **#217 — Evento de flota**: `crear_desde_linea` ahora devuelve/publica
  TODOS los camiones de la flota (antes solo reportaba el primero).

Se revisó el diff real (`5fbb754` y `c851fdd`; el commit `f19f944` es
puramente normalización de EOL, confirmado con
`git diff --ignore-cr-at-eol` sin diferencias de contenido) y se leyó la
justificación de los 3 campos `null` intencionales en el catálogo (puerta y
ejes del contenedor refrigerado, clase de contenedor del High Cube) — son
datos que el catálogo viejo nunca tuvo, no un descuido; la validación los
distingue explícitamente de un cero.

## Conflicto de merge con `QA`

Único archivo en conflicto real: `cubicador/application/plan_cargue/desde_linea.py`
— `QA` todavía tenía la implementación vieja (cascada, retorna un solo
plan); esta rama la reemplaza por completo (reparto por peso, retorna la
flota completa). Se resolvió tomando la versión de esta rama en su
totalidad, tal como indicó el autor en la descripción de la PR. Se confirmó
que el guard `PRESET_VEHICULO_NO_ENCONTRADO` que trajo `QA` (commit
`3550ec0`) ya está cubierto por esta rama con una implementación equivalente
(busca por `codigo` en vez de por `nombre`) — su test de regresión
(`test_desde_linea_preset_no_encontrado`) se corrió después de resolver y
sigue en verde, sin haber sido tocado.

Se confirmó además que, tras el merge, el diff contra `origin/QA` fuera de
`cubicador/` es exactamente cero — el alcance real de la PR es 100% el
microservicio Python, sin tocar NEXO backend ni frontend.

## Cómo correrlo

```bash
cd cubicador
python -m venv .venv-qa && source .venv-qa/bin/activate  # o .venv-qa/Scripts/activate en Windows
pip install -r requirements.txt
pytest -v
```

Mismo comando que usa `.github/workflows/_cubicador-pruebas.yml` (SQLite en
memoria, sin necesidad de BD real ni VPN).

## Resultado real verificado (2026-09-29)

- `pytest -v` completo: **164 passed, 0 failed**.
- Confirmados individualmente en verde los tests de las 3 issues:
  `tests/application/test_catalogo_unico_vehiculos.py` (20 tests, #215),
  `tests/domain/test_recomendacion_por_peso.py` (10 tests, #216),
  `tests/application/test_flota_desde_linea.py` (8 tests, #217).
- Confirmado en verde el test de regresión heredado de `QA`:
  `test_desde_linea_preset_no_encontrado`.
