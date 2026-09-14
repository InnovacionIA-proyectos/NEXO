# QA — S3-HU-11: CI/CD por servicio (API .NET, Cubicador-API, Cubicador-Worker, RabbitMQ)

**PR verificado:** #147. **Autor:** bquinterobtst.
**Fecha:** 2026-09-14. **Verificado por:** Kevin Cano (QA).

Merge real de `QA` sobre la rama (estaba unos commits atrás — traía el EDA de PR #140
y mi commit de scripts retroactivos). Sin conflictos, sin archivos perdidos
(confirmado con `git diff --diff-filter=D`).

Durante la revisión llegó un commit nuevo a la rama (`POST /api/cubicacion`, issue #78 —
reemplaza la llamada HTTP directa del frontend a Cubicador). Se volvió a mergear `QA`,
reconstruir y re-correr todo antes de seguir.

## Script de esta carpeta

[`verify-suite-desde-cero.sh`](verify-suite-desde-cero.sh) — reproduce el hallazgo de
abajo desde una base de datos realmente vacía (no reciclando un contenedor con
esquema de una corrida anterior, que enmascara el problema).

## Resultado de las suites automatizadas

| Suite | Resultado |
|---|---|
| `dotnet build` (Release) | ✅ 0 errores |
| Domain.Tests | ✅ 127/127 |
| Application.Tests | ✅ 139/139 (antes 132 — 7 tests nuevos) |
| Api.Tests (`pruebas`, sin filtro) | ✅ **160/162** (2 skip) — confirma que `ContratoMessageTypeTests` (PR #128) ya pasa en Docker, `Dockerfile.ci` ahora copia `cubicador/` |
| Api.Tests (`pruebas-integracion`) | ✅ 16/16 |
| Cubicador `pytest` | ✅ 103/103 |
| `scripts/verificar_rutas_ci.py` | ✅ 22/22 expectativas cumplidas |

Estos resultados de arriba son con el contenedor `db-pruebas` **reciclado** entre
corridas (ya tenía el esquema de una prueba anterior). Repetido desde cero, el
resultado es otro — ver el hallazgo.

## Hallazgo real: `docker-compose.ci.yml` queda roto contra una base realmente vacía

`ApiFixture.cs` y `OutboxConsistenciaTests.cs` ya no llaman `Migrate()`/
`EnsureDeleted()` por defecto — correcto para la base corporativa real (objetivo
de esta HU), que no debe recibir un `EnsureDeleted` accidental. Pero
`docker-compose.ci.yml` (el arnés local, con un SQL Server **efímero** que nace
vacío en cada corrida) nunca se actualizó para pasarle `TEST_DB_MIGRATE_ON_STARTUP=true`.

Probado con `docker compose -f docker-compose.ci.yml down -v` antes de cada corrida
(para garantizar una base realmente vacía, no reciclada):

| Corrida | Resultado |
|---|---|
| Sin el flag | **69 de 162** pruebas de `Api.Tests` fallan con `SqlException: Cannot open database "CabinaFletes_Test"` |
| Con `TEST_DB_MIGRATE_ON_STARTUP=true` | Baja a **1 sola falla**: `OutboxConsistenciaTests` revienta con `Database 'CabinaFletes_Test' already exists` — condición de carrera real entre esa clase y `ApiFixture`, que migran la misma base nueva al mismo tiempo (xUnit corre clases de test en paralelo por defecto) |

Detalle completo y la corrección sugerida para dejarlo resuelto del todo: comentario
en el PR.

## Verificación en vivo del endpoint nuevo (`POST /api/cubicacion`, issue #78)

- **Sin `Cubicador3D:OutboundBaseUrl`/`OutboundApiKey` configurados:** responde `503`
  con `CUBICACION_RAPIDA_NO_CONFIGURADA` y un mensaje accionable — degrada, no revienta.
- **Control de rol:** un usuario `ServiciosLogisticos` (no Comercial/CoordinadorFletes/Admin)
  recibe `403 SIN_PERMISO_CABINA_FLETES`.

## Decisión

No bloquea — el resto de las suites (incluida la corrida real del autor contra la
base corporativa vía el nuevo pipeline `ci-cd-api.yml`, que no tiene este problema
porque esa base YA está migrada) pasa limpio. Se aprueba el merge; el arreglo de
`docker-compose.ci.yml` queda documentado en el PR para una corrección posterior.
