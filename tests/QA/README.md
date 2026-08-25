# Registro de verificación QA — PR #48, #49 y #51

Este archivo documenta la verificación manual (revisión de código + ejecución real
de pruebas) hecha antes de aceptar los PR hacia `QA` listados abajo. No reemplaza
el CI: es la evidencia de que, además del CI, alguien corrió todo localmente y
revisó el código línea por línea antes de aprobar el merge.

Ver [`run-qa-checks.ps1`](./run-qa-checks.ps1) para reproducir estos mismos pasos
contra cualquier rama futura.

## Alcance verificado

| PR | HU | Rama | Qué agrega |
|---|---|---|---|
| [#48](https://github.com/InnovacionIA-proyectos/NEXO/pull/48) | S2-HU-07.2 | `25-s2-hu-072-...` | Motor de cubicación portado a Python (servicio `cubicador/` contenerizado) |
| [#49](https://github.com/InnovacionIA-proyectos/NEXO/pull/49) | S2-HU-07.3 | `26-s2-hu-073-...` | Golden tests de paridad (26/26) contra el motor C# real, superset de #48 |
| [#51](https://github.com/InnovacionIA-proyectos/NEXO/pull/51) | — | `fix/using-icurrentuserport-solicitudes` | Fix de 10 `using` faltantes que impedían compilar `QA` |

**Orden de merge recomendado:** #51 → #48 → #49 (49 depende del contenido de 48).

## Entorno usado

- .NET SDK 10.0.400 (`dotnet test`)
- Python 3.14.7 (venv local por rama, `pip install -r requirements.txt`)
- Docker Desktop (stack real de `cubicador/docker-compose.yml`: Postgres + RabbitMQ + api + worker)

## Resultados

### Baseline de `QA` (antes de #51)
```
dotnet test CabinaFletes.Clean.sln
```
- `CabinaFletes.Domain.Tests`: 40/40 ✅
- `CabinaFletes.Application.Tests`: **no compila** (CS0246 `ICurrentUserPort`, 4 archivos)
- `CabinaFletes.Api.Tests`: **no compila** (CS0308/CS0103/CS1061 en cascada, 6 archivos)

### Después del fix de #51
- `CabinaFletes.Domain.Tests`: 40/40 ✅
- `CabinaFletes.Application.Tests`: 41/43 (2 fallas preexistentes, no causadas por #51 — ver "Bugs encontrados")
- `CabinaFletes.Api.Tests`: compila, pero no ejecutable en este entorno (requiere la BD SQL Server corporativa de pruebas — issue #3/#4). Falla local por `SqlException` de red, no por código.

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

## Bugs encontrados y NO corregidos aquí (requieren decisión de negocio, no un `using`)

1. **`ReglasSoloSicetac.CrearReglaSoloSicetacCommandTests.Regla_duplicada_lanza_ConflictException`**
   No se lanza `ConflictException` al crear una regla duplicada — la validación de duplicados no está aplicándose.
2. **`Cotizaciones.MarcarResultadoCommandTests.Nueva_ganada_no_duplica_si_ya_existen_tarifas_para_la_cotizacion`**
   `ITarifasHistoricasPort.Agregar()` se invoca 1 vez cuando el test espera que no se invoque — duplica una tarifa histórica que ya existía.

Recomendación: abrir issues aparte para estos 2 (no son de compilación, son de reglas de negocio) y no bloquear #48/#49/#51 por ellos ya que son preexistentes y no relacionados.

## Hallazgos de proceso (fuera del código de estos 3 PR)

- **Gate de CI corregido:** `cubicador-ci.yml` solo disparaba contra `main`; se corrigió en las ramas de #48/#49 para incluir `QA` (ver comentarios en ambos PR). Antes del fix, el CI nunca había corrido en ninguno de los dos.
- **Branch protection no disponible:** `GET /repos/.../branches/QA/protection` devuelve 403 ("Upgrade to GitHub Pro or make this repository public"). En el plan actual no se puede exigir 1 revisor + CI verde a nivel de plataforma; el cumplimiento depende hoy de disciplina de equipo, no de una regla técnica.
