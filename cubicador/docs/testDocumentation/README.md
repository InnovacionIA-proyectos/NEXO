# Documentación de tests

Todo lo relacionado con verificación del microservicio: qué se prueba, cómo,
y qué falta. Tres documentos, uno por nivel de prueba:

| Documento | Nivel | Qué cubre |
|---|---|---|
| [`REGLAS_DE_NEGOCIO.md`](./REGLAS_DE_NEGOCIO.md) | Unitario (dominio puro) | Las 31 reglas geométricas/de negocio del motor (`domain/motor/*`) — 26 de ellas son los casos dorados de `cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs` (mismo nombre/orden), las otras 5 cubren apilamiento multi-soporte. 33 pruebas en `tests/domain/` (una regla tiene 3 casos parametrizados). |
| **Paridad cross-motor** (sin doc propio aún — ver sección de abajo) | Paridad automatizada (hotspot H5) | `tests/paridad/test_paridad_casos_dorados.py`: los mismos 26 casos, pero comparados contra la salida REAL del motor C# (grabada por `herramientas/generador_casos_dorados/`), no contra un valor transcrito a mano. 27 pruebas (26 casos + 1 guarda de conteo). |
| [`PRUEBAS_DE_INTEGRACION.md`](./PRUEBAS_DE_INTEGRACION.md) | Integración + infraestructura real | Las 4 pruebas de `tests/api/test_smoke.py` (flujo completo vía HTTP contra SQLite), las 2 de `tests/worker/test_ejecutar_solicitud.py` (regresión del desempaque de mensajes RabbitMQ), las 2 de `tests/worker/test_mover_a_dlq.py` (la alerta al equipo cuando un mensaje cae a `cubicacion.dlq`) y el registro de la verificación manual/CI contra Docker Compose real (Postgres propio + RabbitMQ compartido de NEXO, ver `docs/DECISIONES_TECNICAS.md` #9): qué se probó, qué bug apareció y cómo se corrigió, y cómo repetirlo. |

Aparte, `tests/domain/test_optimizadores_registro.py` (2 pruebas) verifica
que un motor alternativo se puede registrar y usar sin tocar `motor.py`.

## Resumen de cobertura actual

```
pytest tests/ -v
```

→ **70 pruebas automatizadas, todas verdes**: 33 de reglas de negocio + 27
de paridad cross-motor + 4 de integración HTTP/SQLite + 2 de regresión del
worker + 2 del registro de algoritmos + 2 de la alerta al `cubicacion.dlq`.

Además, verificado contra **Docker Compose real** (Postgres + api + worker
propios, RabbitMQ compartido de NEXO): el smoke test de CI (`.github/workflows/cubicador-ci.yml` +
`scripts/ci_docker_smoke.py`) automatiza parte de esto en cada push/PR; el
resto (vigilante de timeout, puente contra un NEXO real) sigue siendo
verificación manual — ver `PRUEBAS_DE_INTEGRACION.md` sección 2.

## Relación con los 26 "casos dorados" de `MotorCubicacion3DTests.cs`

Hay dos niveles distintos que no hay que confundir:

1. **`tests/domain/test_motor.py`** — los 26 casos portados **1:1** (mismo
   nombre traducido a snake_case, mismo orden, mismo escenario) como tests
   independientes del motor Python. El "esperado" aquí se escribió leyendo
   los asserts del archivo C# — es fidelidad por lectura cuidadosa, no por
   ejecución del motor original.
2. **`tests/paridad/test_paridad_casos_dorados.py`** — los mismos 26
   escenarios, pero el "esperado" es el JSON grabado por
   `herramientas/generador_casos_dorados/` **corriendo el motor C# real**
   (compilado desde `cabinaFletes`, no reescrito). Esta es la evidencia de
   paridad formal que exige el criterio de liberación por hotspot H5 —
   "verde solo con 26/26", con reporte de
   entrada/esperado/obtenido en cada falla (ver
   `docs/DECISIONES_TECNICAS.md` #3, ya actualizada — la decisión de NO
   construir esto se revirtió cuando ese criterio lo exigió explícitamente).

Los 26/26 coincidieron exacto en la primera corrida contra el motor C# real
— confirma que el nivel 1 (fidelidad por lectura) ya estaba bien hecho, y
ahora hay evidencia automatizada que lo respalda en cada build.
