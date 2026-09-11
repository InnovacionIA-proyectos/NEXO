# QA — S3-HU-02.3: eliminar_vehiculo, vehiculo.eliminado y la carrera de crear_plan

**PR verificado:** #127. **Autor:** bquinterobtst.
**Fecha:** 2026-09-10. **Verificado por:** Kevin Cano (QA).

Cierra 3 huecos que quedaron abiertos tras S3-HU-02/03/05: el comando
`eliminar_vehiculo` (faltaba de los 8 originales), el evento `vehiculo.eliminado`
(sin él la proyección de NEXO nunca sacaba un vehículo borrado) y una carrera real
entre `crear_plan` y `optimizar` (la fila espejo nacía en "borrador", dejando pasar
un `POST /optimizar` inmediato que el worker podía procesar antes que `crear_plan`).

La rama ya traía el merge de `QA` incorporado (su propio commit de merge) — no
hizo falta un merge adicional.

## Resultado de las suites automatizadas

| Suite | Resultado |
|---|---|
| `dotnet build` (Release) | ✅ 0 errores, 1 warning preexistente (no relacionado) |
| Domain.Tests | ✅ 127/127 |
| Application.Tests | ✅ 132/132 |
| Api.Tests (unitarias) | ✅ 152/154 (2 skip esperados) |
| Api.Tests (integración) | ✅ 16/16 |
| Cubicador `pytest` | ✅ 84/84 |

Coincide exactamente con lo que declara el PR.

## Verificación en vivo de los 3 arreglos

- **Carrera `crear_plan`/`optimizar`:** confirmé que la fila espejo nace en
  `"procesando"` (antes "borrador"). Intentar reproducir la carrera con llamadas
  HTTP secuenciales normales no la disparó de forma confiable — en un entorno
  local todo-en-Docker, la confirmación de Cubicador llega en pocos milisegundos,
  cerrando la ventana antes de que la segunda llamada llegue. Para verificar el
  guard mismo de forma determinística, forcé el estado a `"procesando"` por SQL
  directo y confirmé que `POST /optimizar` responde correctamente `409
  PLAN_CARGUE_EN_PROCESO` — el guard funciona como se describe.
- **`DELETE /api/vehiculos/{id}`:** creé un vehículo, confirmé que aparecía en
  `GET /api/vehiculos`, lo borré, y confirmé que desapareció de la proyección de
  NEXO (evento `vehiculo.eliminado` funcionando end-to-end).
- **Control de rol:** un usuario `CoordinadorFletes` (no Admin) recibe `403
  SIN_PERMISO_CABINA_FLETES` al intentar borrar.
- **`VEHICULO_EN_USO`:** intenté borrar un vehículo referenciado por un plan
  existente — Cubicador lo rechaza de forma async y el vehículo sigue intacto en
  el catálogo (no se publica `vehiculo.eliminado` cuando el borrado falla).

## Bugs encontrados

Ninguno nuevo. Confirmé que esta rama **no toca** `worker/main.py` (el manejador
de errores del Bug 1), `plan_cargue_repo.py::eliminar()` (Bug 3) ni el DTO de
`sku` (Bug 2) — los 3 bugs documentados en la ronda anterior
(`tests/QA/s3-hu-01-02-03-061-cubicacion-por-colas/README.md`) siguen presentes
sin cambios, ni mejor ni peor.

## Decisión

PR cumple su criterio de cierre. Se aprueba el merge.
