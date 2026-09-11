# QA — S3-HU-06.2: guarda del contrato de nombres entre C# y Python (`ContratoMessageTypeTests`)

**PR verificado:** #128. **Autor:** bquinterobtst.
**Fecha:** 2026-09-10. **Verificado por:** Kevin Cano (QA).

La rama ya estaba al día con `QA` (merge-base = tip actual) — no hizo falta merge.

## Resultado de las suites automatizadas

| Suite | Resultado |
|---|---|
| `dotnet build` (Release) | ✅ 0 errores |
| Domain.Tests | ✅ 127/127 |
| Application.Tests | ✅ 132/132 |
| **Api.Tests (unitarias, vía `docker-compose.ci.yml`)** | ❌ **154/159 — fallan los 5 tests nuevos de `ContratoMessageTypeTests`** |
| Api.Tests (integración) | ✅ 16/16 (no incluye la clase nueva) |
| Cubicador `pytest` | ✅ 84/84 |

## Bug encontrado: los 5 tests nuevos siempre fallan en el arnés de CI de este repo

`ContratoMessageTypeTests` lee directamente del disco los archivos fuente de Cubicador
(`cubicador/infrastructure/mensajeria/rabbitmq.py`, `cubicador/worker/main.py`) para
compararlos por reflexión/regex contra el lado C#. Eso funciona en un checkout local
completo (`dotnet test` directo: **5/5 pasan**, confirmado), pero **`build/Dockerfile.ci`
solo copia `src/`, `tests/` y `docs/` — nunca `cubicador/`**. Dentro del contenedor que
usa `docker-compose.ci.yml run pruebas` (el mismo arnés que usamos para todo el flujo de
QA, y el candidato obvio para un futuro pipeline de .NET — que es justo lo que este PR
señala que falta), los 5 tests fallan con `FileNotFoundException`
("No existe cubicador/infrastructure/mensajeria/rabbitmq.py").

La verificación del propio PR ("Api.Tests 157, 0 fallos") se corrió fuera de Docker, con
el repo completo en disco — por eso no se notó.

**No se trata de un test mal escrito**: la lógica del contrato es correcta (confirmado
que los 5 pasan localmente). Es un problema de portabilidad al entorno de CI real de este
repo, exactamente el tipo de cosa que esta HU busca prevenir para el propio contrato de
mensajería.

## Decisión pendiente

El resto de las suites pasan limpio y sin regresiones. Este PR es el primero de esta
ronda donde el problema no es un hallazgo de verificación en vivo (que no bloquea la
suite), sino una **falla real en la suite automatizada del propio harness de CI** —
queda a criterio de Kevin si esto bloquea el merge o se documenta para que el autor
ajuste la ruta de los tests (por ejemplo, copiando `cubicador/` en `Dockerfile.ci`, o
haciendo que los tests salten con un mensaje claro si la carpeta no existe en vez de
fallar en rojo).
