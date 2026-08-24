# Documentación de tests

Todo lo relacionado con verificación del microservicio: qué se prueba, cómo,
y qué falta. Dos documentos, uno por nivel de prueba:

| Documento | Nivel | Qué cubre |
|---|---|---|
| [`REGLAS_DE_NEGOCIO.md`](./REGLAS_DE_NEGOCIO.md) | Unitario (dominio puro) | Las 31 reglas geométricas/de negocio del motor (`domain/motor/*`), portadas desde los tests xUnit originales de cabinaFletes. 33 pruebas en `tests/domain/` (una regla tiene 3 casos parametrizados). |
| [`PRUEBAS_DE_INTEGRACION.md`](./PRUEBAS_DE_INTEGRACION.md) | Integración + infraestructura real | Las 4 pruebas de `tests/api/test_smoke.py` (flujo completo vía HTTP contra SQLite) y el registro de la verificación manual contra Docker Compose real (Postgres + RabbitMQ): qué se probó, qué bug apareció y cómo se corrigió, y cómo repetirlo. |

## Resumen de cobertura actual

```
pytest tests/ -v
```

→ **37 pruebas automatizadas, todas verdes**: 33 de reglas de negocio + 4 de
integración HTTP/SQLite.

Además, verificado **manualmente** (no automatizado todavía, ver
`PRUEBAS_DE_INTEGRACION.md` sección 2): el mismo flujo contra PostgreSQL real
en Docker, y el flujo asíncrono completo contra RabbitMQ real (worker
consumiendo mensajes, optimizando contra la base de datos, y publicando el
evento de resultado).

## Filosofía de estas pruebas (por qué no hay "26 casos dorados" aquí)

Este servicio **no** compara su salida contra el motor C# original — esa
paridad se descartó explícitamente porque el motor va a cambiar de diseño
más adelante (ver `docs/DECISIONES_TECNICAS.md` #3). Lo que sí se hizo fue
portar las reglas de negocio que el motor original ya tenía verificadas
(las 31 de `REGLAS_DE_NEGOCIO.md`) como pruebas de **este mismo motor**, para
garantizar que el port de C# a Python no introdujo errores de traducción —
no para demostrar que dos motores distintos producen el mismo resultado.
