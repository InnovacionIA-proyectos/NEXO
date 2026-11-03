# QA — PR #424: Base propia del Cubicador con esquema, seeder y documentación únicos (#327)

## Resumen

Preparación para que el Cubicador deje de compartir esquema con Cotifly
(hoy vive en `dbo_cubicador` dentro de `dbTarifario_Nexo_Pruebas`, que
comparte con `dbo_cotifly` de Cotifly) y pase a tener su **propia base**,
`dbCubicador_Pruebas`, esquema `dbo`.

**Importante: esta PR NO cambia el comportamiento en ejecución todavía.**
El propio commit lo dice explícitamente: *"el esquema por defecto sigue en
`dbo_cubicador` y QA sigue en la base compartida hasta S6-HU-31.3 (#423)"*
— el corte real queda para otra issue/PR futura. Esta PR es, en esencia,
consolidación de documentación + esquema + seeders + ajuste de
docker-compose/CI **locales**, sin tocar la BD compartida de QA ni el
comportamiento de la aplicación contra ella.

- **`cubicador/database/esquema/ESQUEMA_CUBICADOR.sql`**: reúne las 16
  tablas (catálogo, recomendación y motor 3D) en un solo DDL para el
  esquema `dbo`, según el commit "verificado contra la base real de QA"
  por el autor (columnas, valores por defecto, `CHECK`, llaves e índices).
- **Seeders regenerados** (`_generar_seeders.py`): la regla de
  incompatibilidad ahora usa el par normalizado, igual que lo hace la
  aplicación — el catálogo cargado en `dbCubicador_Pruebas` quedó
  idéntico al de QA (según el commit).
- **`cubicador/infrastructure/persistence/db.py` y
  `modelos_catalogo.py`**: solo se actualizan comentarios/docstrings para
  apuntar a la nueva ruta del esquema (`database/esquema/ESQUEMA_CUBICADOR.sql`
  en vez de `docs/Database/ESQUEMA_CUBICADOR.sql`) — **sin cambio de
  comportamiento**, confirmado línea por línea en el diff.
- **`docker-compose.yml`/CI**: el contenedor de SQL Server **local y
  desechable** (desarrollo y smoke test de CI) ahora crea
  `dbCubicador_Pruebas`/`dbo` en vez de `dbTarifario_Nexo_Pruebas`/`dbo_cubicador`
  — aislado, no afecta la BD compartida real de QA.
- Documentación nueva/reescrita: `cubicador/database/README.md`,
  `documentacion/DICCIONARIO-DATOS.md`, `documentacion/ORIGEN-Y-DECISIONES-SEEDER.md`.
- Limpieza de archivos obsoletos: scripts de corrección SQL antiguos y CSVs
  de seeders que ya no aplican.

## Qué se verificó

1. Diff aislado de la PR (3 commits propios: limpieza de archivos
   obsoletos, y el commit principal de consolidación).
2. Confirmado que `db.py` y `modelos_catalogo.py` **solo cambian
   comentarios**, ningún cambio de lógica.
3. Confirmado que el cambio de `docker-compose.yml`/CI es **local y
   aislado** (contenedor desechable), no toca la BD compartida real de
   QA que sigue usando `dbo_cubicador`.
4. `dotnet build -c Release` → 0 errores (esta PR no toca `src/` de
   .NET).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218** (sin cambios,
   como se esperaba).
6. `dotnet test tests/Nexo.Application.Tests` → **1182/1182** (sin
   cambios).
7. `python -m pytest tests -q` dentro de `cubicador/` (con su propio
   `.venv`) → **360/360**, incluido `test_esquema_y_migraciones.py` con
   su docstring actualizado.
8. No se pudo validar la carga del esquema nuevo en un SQL Server
   desechable vía Docker (Docker Desktop no estaba corriendo en esta
   máquina en el momento de la verificación) — no se considera bloqueante
   dado que el autor ya documentó haberlo verificado contra la BD real de
   QA, y que esta PR no cambia el comportamiento de la aplicación en
   ejecución.

## Datos de prueba

Ninguno: esta PR no toca la BD compartida de QA. La base propia
`dbCubicador_Pruebas` mencionada en la documentación es, según el commit,
una base aparte donde el autor ya probó el esquema — fuera del alcance de
esta verificación.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. El corte real
hacia la base propia del Cubicador queda pendiente de una issue futura
(#423), fuera del alcance de esta PR.
