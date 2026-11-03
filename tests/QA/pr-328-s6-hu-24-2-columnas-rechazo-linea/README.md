# PR #328 (S6-HU-24.2, cierra #314) — Columnas del rechazo en la línea de solicitud

## Qué cambia

Solo esquema (sin código C#, el consumo queda para la issue NEXO-B1 aparte):
`db_NEXO.tbl_lineas_solicitud` gana `MotivoRechazo` (nvarchar(500) NULL),
`RechazadaPor` (uniqueidentifier NULL) y `RechazadaAt` (datetimeoffset NULL).
Actualizado solo `docs/Database/ESQUEMA_NEXO.sql` (sin migraciones de EF
Core en este proyecto, el script se corre a mano). Incluye además un
documento de propuesta (`2026-10-19_ISSUES-MIGRACION-BD.md`) sobre migrar las
bases de pruebas compartidas a bases propias — solo backlog, no ejecutado.

## Qué se verificó (ver `verificar.ps1`, script real ejecutado)

**No se confió en la afirmación de la PR de que ya se aplicó contra la base
de pruebas real** — se consultó directamente `sys.columns`/`sys.check_constraints`
contra `dbInformesAutomatizados_Pruebas` (10.10.40.85):

1. **Las 3 columnas existen con el tipo y nulabilidad exactos declarados**:
   `MotivoRechazo` nvarchar (max_length=1000 bytes = 500 caracteres),
   `RechazadaPor` uniqueidentifier, `RechazadaAt` datetimeoffset — las tres
   `is_nullable=True`.
2. **`EstadoLinea`**: nvarchar(40 bytes = 20 caracteres), `NOT NULL`, **sin
   ningún `CHECK CONSTRAINT`** en la tabla — confirma que el valor
   `"Rechazada"` (9 caracteres) cabe sin cambios adicionales, tal como
   afirma la PR.

**Rama actualizada con `QA`** (merge local sin conflictos, commit
`bf99728`, no pusheado aparte — se integra al mergear la PR).

**Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación correcta,
0 errores (1 warning preexistente ajeno a esta PR) — confirma que no hay
cambio de código.

**Sanity contra la base real ya alterada**: corrí
`Nexo.Api.Tests` filtrado por `Solicitud` (35 pruebas, las más
relacionadas con la tabla tocada) → **35/35, 0 errores**. Las columnas
nuevas (no mapeadas aún en el modelo EF, a propósito) no rompen ninguna
consulta existente.

## Resultado

Sin hallazgos. Las columnas están correctamente aplicadas en la base real de
pruebas con el tipo/nulabilidad declarados, y no afectan el comportamiento
existente.
