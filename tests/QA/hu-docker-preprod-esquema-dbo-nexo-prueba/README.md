# Verificación QA — docker-compose test preproducción (schema `dbo_NEXO_prueba`)

Historial completo de esta HU (sin issue/HU numerada propia, es infraestructura):

1. **PR #90** ("Actualizacion de docker compose test preproduccion", ivasco-dev) —
   verificado y **no mergeado**: rompía 3 pruebas (`OutboxConsistenciaTests`) y las
   13 vistas `analitica.*` (HU-10.1). Ver hallazgos originales más abajo. PR
   cerrado por indicación de Kevin, corrección dividida en 2 issues.
2. **Issues [#92](https://github.com/InnovacionIA-proyectos/NEXO/issues/92)**
   (Outbox) y **[#93](https://github.com/InnovacionIA-proyectos/NEXO/issues/93)**
   (vistas `analitica.*`) — abiertas con el detalle exacto de cada hallazgo,
   asignadas a ivasco-dev.
3. **PR #94** (fix de #92) y **PR #95** (fix de #92 + #93, superset de #94) — al
   recrear la rama sobre el `QA` ya actualizado (con HU-10.2/10.3 mergeadas), los 2
   primeros commits de ambos PR **borraron por accidente toda la carpeta
   `analitica/`** (el entregable de datos, no código) y mis 2 carpetas de
   evidencia QA de esa ronda. Reportado a Kevin, quien decidió: cerrar PR #94
   (queda redundante frente a #95) y esperar la corrección en #95.
4. **PR #95, versión final** — commit adicional `de83f6b "Recuperación de la
   carpeta 'analitica'"` la restaura completa (confirmado byte a byte contra
   `QA`). **Este es el PR verificado y mergeado.**

## Qué trae el PR #95 (contenido real, más allá de las carpetas recuperadas)

- `docker-compose.yml`: deshabilita el servicio `db` local, conecta la API a una
  BD de pruebas/preproducción real vía `ConnectionStrings__CabinaFletes`.
- Migración `MoverEsquemaDboNexoPrueba`: mueve las 27 tablas de NEXO de `dbo` a
  `dbo_NEXO_prueba` (BD compartida con otra app que ya usa `dbo`).
- Migración `AgregarRolesPortalNexo` + fix en `IdentidadPortalDbContext.cs`:
  identidad real del portal y mapeo de los 6 roles.
- **Migración nueva `RecrearVistasAnaliticaEnDboNexoPrueba`** (fix de #93): recrea
  las 13 vistas `analitica.*` apuntando a `dbo_NEXO_prueba.<tabla>` en vez de
  `dbo.<tabla>` — mismas columnas, mismo `WITH (NOLOCK)`, solo cambia el esquema
  del `FROM`/`JOIN`. `Down()` es simétrico (vuelve a dejarlas apuntando a `dbo`,
  el estado roto de antes, a propósito — no hay forma de "arreglar" un rollback
  de un fix).
- **`VistasAnaliticaTests.cs` nuevo** (fix de #93): 13 pruebas `[Theory]`, una por
  vista, que confirman que cada una resuelve contra las tablas reales — no existía
  ninguna prueba que tocara `analitica.*` antes, por eso la ruptura original pasó
  desapercibida para el CI.
- **`OutboxConsistenciaTests.cs`/`OutboxCargaTests.cs`** (fix de #92): el
  `DELETE FROM OutboxMessage` crudo se reemplaza por
  `db.Set<OutboxMessage>().ExecuteDeleteAsync()` — resuelve el esquema desde el
  modelo de EF Core, no lo hardcodea (no se puede volver a romper si el esquema
  cambia otra vez).

## Regresión completa — confirma los 2 fixes

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | 121/121 |
| `Api.Tests` | **116/118** (+2 saltadas por diseño) — antes de este fix: 100/105 (3 fallidas) |

Build `--no-cache`: 0 errores. Las 13 pruebas nuevas de `VistasAnaliticaTests`
están incluidas y en verde (105+13=118 total, 103+13=116 pasadas).

## Verificación en vivo — reproduce exactamente el escenario que rompió antes

Con la app real levantada (SQL Server real, migraciones aplicadas desde cero
incluida la del movimiento de esquema), confirmado que la tabla base vive en
`dbo_NEXO_prueba` y las 13 vistas `analitica.*` resuelven sin error:

```
dbo_NEXO_prueba.solicitudes

clientes -> OK (10 filas)
rutas -> OK (12 filas)
configuraciones_vehiculo -> OK (10 filas)
servicios_conexos -> OK (10 filas)
usuarios -> OK (11 filas)
solicitudes -> OK (17 filas)
lineas_solicitud -> OK (40 filas)
cotizaciones -> OK (10 filas)
cotizacion_lineas -> OK (12 filas)
cotizacion_conexos -> OK (8 filas)
tarifas_historicas -> OK (12 filas)
audit_log -> OK (12 filas)
inconsistencias -> OK (15 filas)
```

`/health` sano antes y después. Mismo escenario que en la verificación original
del PR #90 (donde las 13 daban "Invalid object name"), ahora las 13 resuelven.

## Verificación de la recuperación de `analitica/`

```
git diff $(git merge-base pr95 origin/QA)..pr95 --stat -- analitica/
```
→ 0 diferencias: el árbol de `analitica/` en la versión final del PR #95 es
idéntico byte a byte al de `QA` (HU-10.1/10.2/10.3 intactas). Las 2 carpetas de
evidencia QA de esa ronda (`tests/QA/hu-10.2-...`, `tests/QA/hu-10.3-...`), que sí
seguían borradas en la versión del PR verificada, se restauraron como parte de
este mismo merge.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores; app real levanta y `/health` sano |
| Regresión | ✅ 366 pruebas (353 + 13 nuevas), sin filtros, cero fallos — antes 3 fallidas |
| Integración | ✅ Las 13 vistas `analitica.*` resuelven contra la app real con el esquema movido, confirmado con datos reales |
| Integridad del repositorio | ✅ `analitica/` (entregable de HU-10.1/10.2/10.3) recuperado íntegro, verificado byte a byte |

## Conclusión

Los 2 hallazgos bloqueantes de la verificación original del PR #90 (#92 Outbox,
#93 vistas rotas) están corregidos y verificados de forma independiente, con
cobertura de prueba nueva para que una regresión así no vuelva a pasar
desapercibida. El accidente de borrado de `analitica/` detectado en la primera
versión de este PR también se corrigió y se verificó la recuperación completa.
PR #94 se cerró por quedar redundante frente a este. Sin objeciones para
mergear.

---

## Hallazgos originales (PR #90, no mergeado) — para referencia

Ver el historial de comentarios de los PR #90/#94/#95 en GitHub para el detalle
completo de cómo se encontraron los 2 bugs originales (SQL crudo sin calificar
esquema, vistas `analitica.*` creadas con referencia hardcodeada a `dbo`).
