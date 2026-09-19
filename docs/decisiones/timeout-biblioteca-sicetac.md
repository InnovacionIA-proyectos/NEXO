# Diagnóstico — timeout real de GET /api/biblioteca-sicetac

**Estado:** aceptada (mitigación aplicada; causa raíz pendiente del equipo de DW) · **Fecha:** 2026-09-18 · **Issue:** #167 (S4-HU-02)

## Qué se reportó

Sin filtros: 33 segundos para traer solo 3 filas. Filtrando por origen/destino o
incluso solo por periodo: timeout completo (500, "Execution Timeout Expired").
Tabla `DWSice.dbo.tblRutasSicetac`, ~2.14 millones de filas (medido en vivo,
issue #156).

## Causa raíz

**`DwSiceDbContext` no tiene NINGÚN índice declarado** sobre `Periodo`,
`NombreRuta`, `Configuracion` ni `NombreUnidadTransporte` (`HasIndex` no
aparece ni una sola vez en `src/Nexo.Infrastructure/External/
DwSiceDbContext.cs`) — solo existe la clave primaria sobre `Id`. Sin ese
índice:

- El `WHERE Periodo = @p` escanea la tabla completa.
- El `ORDER BY NombreRuta, Configuracion, NombreUnidadTransporte` que precede
  al `Skip/Take` obliga a SQL Server a materializar y ordenar en memoria/tempdb
  TODO lo que matcheó el filtro antes de poder cortar la página — con 2.14M de
  filas de base, esto es exactamente el tipo de plan que agota un timeout de
  30 segundos.
- El filtro de texto libre (`q`) usa `.Contains()` → `LIKE '%term%'`, no
  sargable, agravando cualquier escaneo.
- `PeriodosAsync` (resuelve el periodo por defecto cuando no se pasa
  explícito) hace un `DISTINCT` sobre `Periodo` en las 2.14M filas — plausible
  causa principal de los "33s sin filtros", ya que se ejecuta ANTES del filtro
  de la matriz y solo se cachea 15 minutos.

**Por qué esto NO se arregla con una migración de este repo**:
`DwSiceDbContext` es de **solo lectura** — sin `Migrate()`, sin carpeta de
Migrations propia, con `SaveChanges*` bloqueados explícitamente
(`throw ReadOnly()`). La tabla la mantiene el proceso ETL del Data Warehouse
corporativo, un sistema fuera del alcance de NEXO. Es exactamente el mismo
patrón que el issue #164 (Dokploy): el fix real es una petición al equipo
dueño de la infraestructura, no un cambio de código acá.

## Petición para el equipo de Data Warehouse (SICE-TAC)

Índice compuesto sugerido sobre `dbo.tblRutasSicetac` (ajustar nombres de
columna reales si difieren de los que usa este adapter):

```sql
CREATE NONCLUSTERED INDEX IX_tblRutasSicetac_Periodo_Orden
    ON dbo.tblRutasSicetac (Periodo, NombreRuta, Configuracion, NombreUnidadTransporte)
    INCLUDE (Origen, Destino, NombreTipoCarga, CondicionCarga, ViaEstandar,
             Valor, ValorHora, ValorSiceConHora, HorasRecorrido, HorasPactadas,
             FechaCreacion, FechaModificacion);

-- Ademas, uno liviano solo para el DISTINCT de PeriodosAsync (no necesita el
-- INCLUDE: es una consulta que solo toca la columna Periodo).
CREATE NONCLUSTERED INDEX IX_tblRutasSicetac_Periodo
    ON dbo.tblRutasSicetac (Periodo);
```

Adicional: `README.md` documenta que este DW corre en **SQL Server 2008 R2**
(fuera de la matriz de compatibilidad oficial de
`Microsoft.EntityFrameworkCore.SqlServer 10.0.0`, que empieza en SQL Server
2012) — vale la pena que el equipo de datos confirme si esa versión sigue
siendo correcta, porque si lo es, la sintaxis `OFFSET/FETCH` que genera
`Skip/Take` (introducida en SQL Server 2012) podría fallar por sintaxis además
de por rendimiento.

## Mitigaciones aplicadas desde el código (esto sí se implementó)

Ninguna de estas resuelve la causa raíz — bajan el impacto mientras se
gestiona el índice con el equipo de DW:

1. **`CommandTimeout` explícito de 90s** para `DwSiceDbContext`
   (`src/Nexo.Infrastructure/DependencyInjection.cs`) — el default de
   SqlClient es 30s, que coincide exactamente con el síntoma reportado. Con
   90s, una consulta legítimamente lenta (sin índice) tiene más margen antes
   de devolver 500, sin pretender que quede "rápida".
2. **TTL del caché de periodos subido de 15 minutos a 6 horas**
   (`BibliotecaSicetacAdapter.cs`) — los periodos SICE-TAC cambian con baja
   frecuencia (mensual), así que el costoso `DISTINCT` sobre 2.14M filas se
   paga muchas menos veces al día.

## Verificación pendiente (no ejecutable desde este entorno)

No se pudo medir el tiempo de respuesta real antes/después contra el DW real
desde esta sesión (sin acceso a esa base corporativa). Falta, contra el
ambiente real:

- Medir `GET /api/biblioteca-sicetac` sin filtros y con filtros por
  origen/destino/periodo, ANTES de que el equipo de DW aplique el índice.
- Repetir la medición DESPUÉS de aplicar el índice sugerido, para confirmar
  que resuelve ambos síntomas (lentitud sin filtros, timeout con filtros).
- Confirmar que el `CommandTimeout` de 90s no interfiere negativamente con
  otro consumidor del mismo `DwSiceDbContext` (solo lo usa este módulo hoy).
