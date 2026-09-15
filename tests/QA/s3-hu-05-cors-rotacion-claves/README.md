# QA — S3-HU-05: cerrar CORS de Cubicador y rotar claves expuestas

**PR verificado:** #125. **Autor:** ivasco-dev.
**Fecha:** 2026-09-09. **Verificado por:** Kevin Cano (QA).

La rama estaba desactualizada respecto a `QA` (creada antes de que se mergearan
#122/#123/#124) — se hizo un merge real de `QA` sobre la rama antes de testear.
El merge no tuvo conflictos.

Esta rama no toca ningún archivo `.NET` (`src/`/`tests/`), así que las 3 suites de
xUnit no se volvieron a correr (ya estaban en verde en `QA` tras la ronda anterior).

## Script de esta carpeta

[`verify-cors-cerrado.mjs`](verify-cors-cerrado.mjs) — confirma la ausencia de
encabezados `Access-Control-*`, el rechazo del preflight `OPTIONS`, y que la
autenticación sigue fail-closed. Corrido contra el stack real durante esta
verificación.

## Resultado de las suites

| Suite | Resultado |
|---|---|
| Cubicador `pytest` | ✅ 82/82 |
| `ci_docker_smoke.py` (Postgres+RabbitMQ+api+worker reales) | ✅ pasa |

## Verificación en vivo del objetivo específico de esta HU

- **CORS realmente cerrado:** `GET /api/planes-cargue/vehiculos` con encabezado
  `Origin: http://localhost:5173` responde 200 pero **sin ningún encabezado
  `Access-Control-*`** — un navegador no podría leer la respuesta cross-origin.
  Un preflight `OPTIONS` con `Access-Control-Request-Method` no obtiene respuesta
  de CORS (405, el método no está expuesto). Confirmado que ya no queda ninguna
  variable `CUBICADOR_CORS_ORIGENES` en código ni en `.env.example`.
- **Autenticación sigue fail-closed:** sin `Authorization` o con un Bearer inválido,
  la API responde 401 en ambos casos — la rotación de `CUBICADOR_API_KEY` /
  `NEXO_CALLBACK_API_KEY` no debilitó el mecanismo de validación.
- `docs/CHECKLIST_DESPLIEGUE.md` (nuevo) deja documentado, sin ejecutar, que falta
  aplicar la clave nueva en el ambiente real — correctamente fuera del alcance de
  este repo/PR.

## Bugs encontrados

Ninguno. No se repite ningún hallazgo de la ronda anterior (#122/#123/#124) porque
esta rama no toca esos archivos (`worker/main.py`, `plan_cargue_repo.py`).

## Decisión

PR cumple su criterio de cierre. Se aprueba el merge.
