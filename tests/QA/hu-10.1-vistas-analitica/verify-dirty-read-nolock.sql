-- Verificación QA — riesgo real de NOLOCK en las vistas `analitica.*` (HU-10.1, PR #82).
--
-- Demuestra empíricamente (no solo por lectura del código) que WITH (NOLOCK) permite
-- lecturas SUCIAS: una fila insertada dentro de una transacción sin COMMIT, que luego
-- se revierte con ROLLBACK y por lo tanto NUNCA existió de verdad, es visible desde
-- una vista `analitica.*` mientras la transacción sigue abierta.
--
-- Compara contra una lectura normal (SIN NOLOCK) de la misma tabla base, que gracias a
-- READ_COMMITTED_SNAPSHOT (ya activo en esta base, confirmado con
-- `SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name='Nexo'`)
-- NO ve la fila sucia y tampoco se bloquea esperando el COMMIT/ROLLBACK.
--
-- Cómo correrlo (dos sesiones en paralelo):
--
-- 1) Sesión A (escritor, deja la fila sin confirmar 10s y luego la revierte):
--    docker cp verify-dirty-read-nolock.sql nexo-db-1:/tmp/
--    docker exec -d nexo-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa \
--      -P '<password>' -d Nexo -i /tmp/verify-dirty-read-nolock.sql
--
-- 2) Sesión B (lector, mientras la sesión A sigue abierta, correr varias veces seguidas):
--    docker exec nexo-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa \
--      -P '<password>' -d Nexo -Q "
--        SELECT 'analitica(NOLOCK)=' + CAST(COUNT(*) AS VARCHAR) FROM analitica.solicitudes
--          WHERE Id='DEAD0001-0000-0000-0000-000000000001';
--        SELECT 'dbo(sin NOLOCK)=' + CAST(COUNT(*) AS VARCHAR) FROM dbo.solicitudes
--          WHERE Id='DEAD0001-0000-0000-0000-000000000001';"
--
-- Resultado observado (2026-09-01, contra un stack nexo-db-1 recién levantado):
--   Durante los 10s sin COMMIT -> analitica(NOLOCK)=1, dbo(sin NOLOCK)=0
--   Después del ROLLBACK       -> analitica(NOLOCK)=0, dbo(sin NOLOCK)=0
-- Es decir: la vista analitica.solicitudes mostró una fila que nunca llegó a existir
-- de verdad. Reemplazar los 2 IDs de abajo (ClienteId/CreadoPor) por IDs reales de tu
-- propio ambiente antes de correr.
BEGIN TRANSACTION;
INSERT INTO dbo.solicitudes (Id, ClienteId, Tipo, CreadoPor, Estado, Deadline, CreatedAt, UpdatedAt)
VALUES (
    'DEAD0001-0000-0000-0000-000000000001',
    'C2A72B89-A391-4DBF-A51D-0125D2B9A1A1', -- ClienteId real de tu ambiente
    'Spot',
    '76C446F0-3CDA-4BC3-869E-972E08EB9521', -- CreadoPor (UsuarioId) real de tu ambiente
    'Pendiente',
    DATEADD(day, 10, SYSDATETIMEOFFSET()),
    SYSDATETIMEOFFSET(),
    SYSDATETIMEOFFSET()
);
PRINT 'INSERTADO SIN COMMIT, esperando 10s antes de ROLLBACK...';
WAITFOR DELAY '00:00:10';
ROLLBACK TRANSACTION;
PRINT 'ROLLBACK HECHO - la fila nunca debio existir de verdad';
