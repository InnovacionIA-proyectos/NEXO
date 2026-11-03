# Registro de verificación QA — sesión del 2026-08-25

Este archivo documenta la verificación manual (revisión de código + ejecución real
de pruebas) hecha antes de aceptar los PR hacia `QA` listados abajo. No reemplaza
el CI: es la evidencia de que, además del CI, alguien corrió todo localmente y
revisó el código línea por línea antes de aprobar el merge.

Ver [`run-qa-checks.ps1`](./run-qa-checks.ps1) para reproducir estos mismos pasos
contra cualquier rama futura.

## Alcance verificado

| PR | HU / issue | Rama | Qué hace | Estado |
|---|---|---|---|---|
| [#48](https://github.com/InnovacionIA-proyectos/NEXO/pull/48) | S2-HU-07.2 | `25-s2-hu-072-...` | Motor de cubicación portado a Python (servicio `cubicador/` contenerizado) | ✅ Mergeado (`43613a5`) |
| [#49](https://github.com/InnovacionIA-proyectos/NEXO/pull/49) | S2-HU-07.3 | `26-s2-hu-073-...` | Golden tests de paridad (26/26) contra el motor C# real, superset de #48 | ✅ Mergeado (`fb045c9`, conflicto de doc resuelto en `94a1b5e`) |
| [#51](https://github.com/InnovacionIA-proyectos/NEXO/pull/51) | issue #1/#4 | `fix/using-icurrentuserport-solicitudes` | Fix de 10 `using` faltantes que impedían compilar `QA` | ✅ Mergeado (`fe35ea0`) |
| [#52](https://github.com/InnovacionIA-proyectos/NEXO/pull/52) | — | `docs/qa-verification-report` | Esta misma carpeta `tests/QA/` | ✅ Mergeado (`933cfb6`) |
| [#53](https://github.com/InnovacionIA-proyectos/NEXO/pull/53) | — | `fix/duplicados-reglas-sicetac-y-tarifas-historicas` | 2 bugs de lógica de negocio (ver sección de abajo) | ✅ Mergeado (`0e6eb8b`) |
| [#54](https://github.com/InnovacionIA-proyectos/NEXO/pull/54) | issue #6 | `test/verificar-gate-issue-6` | PR de prueba deliberadamente roto, para verificar el gate | ❌ Cerrado **sin merge** a propósito (`93530c9`) — ver sección "Verificación del gate" |

**Orden de merge aplicado:** #51 → #52 → #48 → #49 → #53. #54 nunca se mergeó (ese era el punto de la prueba).

## Entorno usado

- .NET SDK 10.0.400 (`dotnet test`)
- Python 3.14.7 (venv local por rama, `pip install -r requirements.txt`)
- Docker Desktop (stack real de `cubicador/docker-compose.yml`: Postgres + RabbitMQ + api + worker)

## Resultados

### Baseline de `QA` (antes de #51)
```
dotnet test Nexo.Clean.sln
```
- `Nexo.Domain.Tests`: 40/40 ✅
- `Nexo.Application.Tests`: **no compila** (CS0246 `ICurrentUserPort`, 4 archivos)
- `Nexo.Api.Tests`: **no compila** (CS0308/CS0103/CS1061 en cascada, 6 archivos)

### Después del fix de #51
- `Nexo.Domain.Tests`: 40/40 ✅
- `Nexo.Application.Tests`: 41/43 (2 fallas preexistentes, no causadas por #51 — ver "Bugs encontrados")
- `Nexo.Api.Tests`: compila, pero no ejecutable en este entorno (requiere la BD SQL Server corporativa de pruebas — issue #3/#4). Falla local por `SqlException` de red, no por código.

### PR #48 (rama `25-s2-hu-072-...`)
```
cd cubicador
python -m venv .venv && .venv/Scripts/pip install -r requirements.txt
.venv/Scripts/python -m pytest -v
```
- **41/41 pytest ✅** (motor + api + worker, contra SQLite en memoria)

Smoke test contra el stack real:
```
docker compose up -d --build --wait --wait-timeout 180
python scripts/ci_docker_smoke.py
docker compose down -v
```
- Los 4 contenedores (db, rabbitmq, api, worker) llegan a `Healthy`.
- Flujo async completo (crear → optimizar → resultado): **OK**.
- Reintentos + Dead-Letter Queue con mensaje inválido: **OK**.

Revisión de código: sin secretos hardcodeados, consultas SQL vía SQLAlchemy ORM
(sin f-strings ni SQL crudo → sin riesgo de inyección), autenticación por API key
fail-closed (`api/dependencias.py`).

### PR #49 (rama `26-s2-hu-073-...`, superset de #48)
```
cd cubicador
python -m venv .venv && .venv/Scripts/pip install -r requirements.txt
.venv/Scripts/python -m pytest -v
```
- **68/68 pytest ✅** (los 41 anteriores + 27 de paridad: 26 casos dorados + 1 test que verifica que existen exactamente 26 fixtures)
- Verificado por inspección: `herramientas/generador_casos_dorados/` referencia el `.csproj` real de `cabinaFletes` (motor C# original, congelado) — los fixtures NO son valores transcritos a mano, salen de ejecutar el motor real.
- `test_paridad_casos_dorados.py` hace comparación profunda campo a campo (con redondeo a 6 decimales para evitar falsos negativos `decimal` vs `float`), no una comparación superficial.

## Bugs de lógica de negocio — encontrados y corregidos en #53

Encontrados durante la verificación de #48/#49/#51 (no relacionados con esos PR), corregidos por separado en el PR #53 (`0e6eb8b`) una vez entendida la causa raíz de cada uno:

1. **`ReglaSoloSicetacTexto.Normalizar` no quitaba tildes.** "Bogotá" y "BOGOTA" generaban dos reglas "solo SICE-TAC" distintas para la misma ruta en vez de que la segunda fuera rechazada como duplicada. Fix: delegar a `Domain.Common.Texto.Normalizar` (ya usado para esto mismo en otros módulos). El propio nombre del test (`..._sin_tildes`) ya decía cuál era el comportamiento esperado; su aserción tenía un typo que lo contradecía.
2. **Stub incorrecto en `MarcarResultadoCommandTests`.** El test comparaba contra el Guid de búsqueda de la cotización en vez del `Id` real de la entidad (dos valores distintos, `Cotizacion.Id = Guid.NewGuid()` en el constructor). El código de producción ya estaba correcto — el bug era solo del test, que nunca ejercitaba la ruta que decía probar.

Verificado: `Application.Tests` pasó de 41/43 a **43/43**.

## Verificación del gate de CI (issue #6)

Objetivo: demostrar que un cambio que rompe una regla de negocio real queda bloqueado antes de llegar a `QA`, con evidencia del CI (no solo de una máquina local).

- Commit de prueba `93530c9` (rama `test/verificar-gate-issue-6`, [PR #54](https://github.com/InnovacionIA-proyectos/NEXO/pull/54)): invierte la comparación en `excede_peso_total` (`cubicador/domain/motor/validadores.py`) — el motor dejaría de rechazar cargas que exceden la capacidad del vehículo.
- Confirmado localmente antes de subir el commit: **24 tests fallan** (`pytest`).
- CI de GitHub Actions ([run 32868508370](https://github.com/InnovacionIA-proyectos/NEXO/actions/runs/32868508370)):
  - `Pruebas unitarias y de integracion (pytest)`: **failure** ❌
  - `Verificacion contra Docker Compose real`: **skipped** (depende de que pytest pase primero — el cambio malo nunca llegó a esa etapa)
- PR #54 cerrado **sin merge**, rama borrada. Evidencia completa en los comentarios de [PR #54](https://github.com/InnovacionIA-proyectos/NEXO/pull/54) y del [issue #6](https://github.com/InnovacionIA-proyectos/NEXO/issues/6) (cerrado).

## Hallazgos de proceso

- **Gate de CI corregido:** `cubicador-ci.yml` solo disparaba contra `main`; se corrigió en las ramas de #48/#49 para incluir `QA`. Antes del fix, el CI nunca había corrido en ninguno de los dos. Ya verificado en producción con la prueba de arriba (issue #6).
- **El gate solo cubre `cubicador/` (Python).** El trigger de `cubicador-ci.yml` tiene `paths: ["cubicador/**"]` — el backend .NET (`src/`, `tests/Nexo.*`) **no tiene ningún workflow de CI configurado todavía** (ver issue #4). Un PR que rompa una regla de negocio en el backend C# no sería bloqueado automáticamente hoy, solo por revisión manual (así se verificaron #51 y #53, sin CI).
- **Branch protection no disponible:** `GET /repos/.../branches/QA/protection` devuelve 403 ("Upgrade to GitHub Pro or make this repository public"). En el plan actual no se puede exigir 1 revisor + CI verde a nivel de plataforma; el cumplimiento depende hoy de disciplina de equipo, no de una regla técnica.
- **Ramas de compañeros desactualizadas respecto a `QA`.** Al revisar las ramas remotas sin PR abierto (`1-s2-hu-011-...`, `2-s2-hu-012-...`, `7-s2-hu-021-...`, `14-s2-hu-042-...`, `15-s2-hu-043-...`, `50-s2-hu-017-...`), las 6 están **20 commits detrás de `QA`** (partieron de antes de esta sesión). La rama de la HU-01.1 (`1-s2-hu-011-...`) ya trae, de forma independiente, el mismo fix de `using` en `Program.cs` y los 2 controladores de Excel que este PR — verificado que el contenido es idéntico byte a byte, así que no hay conflicto ahí. Sí conviene que quien las tenga asignadas actualice (`merge`/`rebase`) contra `QA` antes de seguir, para no acumular más desfase.
