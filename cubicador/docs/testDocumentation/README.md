# Documentación de tests

Todo lo relacionado con verificación del microservicio: qué se prueba, cómo,
y qué falta. Dos documentos, uno por nivel de prueba:

| Documento | Nivel | Qué cubre |
|---|---|---|
| [`REGLAS_DE_NEGOCIO.md`](./REGLAS_DE_NEGOCIO.md) | Unitario (dominio puro) | Las 31 reglas geométricas/de negocio del motor (`domain/motor/*`) — 26 de ellas son los casos dorados de `cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs` (mismo nombre/orden, ver más abajo), las otras 5 cubren apilamiento multi-soporte. 33 pruebas en `tests/domain/` (una regla tiene 3 casos parametrizados). |
| [`PRUEBAS_DE_INTEGRACION.md`](./PRUEBAS_DE_INTEGRACION.md) | Integración + infraestructura real | Las 4 pruebas de `tests/api/test_smoke.py` (flujo completo vía HTTP contra SQLite), las 2 de `tests/worker/test_ejecutar_solicitud.py` (regresión del desempaque de mensajes RabbitMQ) y el registro de la verificación manual/CI contra Docker Compose real (Postgres + RabbitMQ): qué se probó, qué bug apareció y cómo se corrigió, y cómo repetirlo. |

Aparte, `tests/domain/test_optimizadores_registro.py` (2 pruebas) verifica
que un motor alternativo se puede registrar y usar sin tocar `motor.py`.

## Resumen de cobertura actual

```
pytest tests/ -v
```

→ **41 pruebas automatizadas, todas verdes**: 33 de reglas de negocio + 4 de
integración HTTP/SQLite + 2 de regresión del worker + 2 del registro de
algoritmos.

Además, verificado contra **Docker Compose real** (Postgres + RabbitMQ + api
+ worker): el smoke test de CI (`.github/workflows/cubicador-ci.yml` +
`scripts/ci_docker_smoke.py`) automatiza parte de esto en cada push/PR; el
resto (vigilante de timeout, puente contra un NEXO real) sigue siendo
verificación manual — ver `PRUEBAS_DE_INTEGRACION.md` sección 2.

## Relación con los 26 "casos dorados" de `MotorCubicacion3DTests.cs`

Los 26 métodos de prueba de `cabinaFletes/src/CabinaFletes.Tests/
MotorCubicacion3DTests.cs` (el criterio de cierre de S2-HU-072) están
portados **1:1** en `tests/domain/test_motor.py` — mismo nombre (traducido a
snake_case), mismo orden, mismo escenario, misma razón de rechazo esperada.
Confirmado comparando ambos archivos método por método.

Lo que **no** existe es una comparación automatizada en vivo entre el motor
C# y el motor Python corriendo lado a lado con el mismo input (paridad
cross-motor) — esa se descartó explícitamente porque el motor original va a
dejar de existir, y comparar contra una versión que se sabe temporal no da
valor duradero (ver `docs/DECISIONES_TECNICAS.md` #3). Cada caso dorado
tiene su equivalente fiel, verificado de forma independiente — no dos
motores comparados en tiempo de ejecución.
