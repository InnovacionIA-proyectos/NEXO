-- QA — S2-HU-11.1 (PR #140): verifica que la migracion corregida
-- (20260910231344_QuitarNolockVistasAnaliticaDboNexoPrueba) deja las 13 vistas
-- `analitica.*` apuntando al esquema correcto (dbo_NEXO_prueba) y sin WITH (NOLOCK).
--
-- Uso: aplicar TODAS las migraciones de la rama (incluida esta) contra una base
-- limpia con:
--   dotnet ef database update --project src/CabinaFletes.Infrastructure \
--     --startup-project src/CabinaFletes.Api --context CabinaFletesDbContext \
--     --connection "<connection-string>"
-- y despues correr este script contra esa misma base.
--
-- La version original de esta migracion (commit cd17068, antes de la correccion)
-- fallaba al aplicarse con "Invalid object name 'dbo.clientes'" porque todavia
-- apuntaba al esquema `dbo` de antes del rename a `dbo_NEXO_prueba` (PR #90/#93/#95).

-- 1) Las 13 vistas deben existir y ser consultables sin error.
SELECT
    s.name AS esquema,
    v.name AS vista
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
WHERE s.name = 'analitica'
ORDER BY v.name;
-- Esperado: 13 filas (clientes, rutas, configuraciones_vehiculo, servicios_conexos,
-- usuarios, solicitudes, lineas_solicitud, cotizaciones, cotizacion_lineas,
-- cotizacion_conexos, tarifas_historicas, audit_log, inconsistencias).

-- 2) Ninguna debe referenciar el esquema viejo `dbo.` ni traer WITH (NOLOCK).
SELECT
    v.name AS vista,
    CASE WHEN OBJECT_DEFINITION(v.object_id) LIKE '%FROM dbo.%' THEN 'FALLA: aun apunta a dbo.*' ELSE 'OK' END AS chequeo_esquema,
    CASE WHEN OBJECT_DEFINITION(v.object_id) LIKE '%NOLOCK%' THEN 'FALLA: todavia tiene NOLOCK' ELSE 'OK' END AS chequeo_nolock
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
WHERE s.name = 'analitica'
ORDER BY v.name;
-- Esperado: las 13 filas en "OK" para ambas columnas.

-- 3) Una consulta real de humo contra la vista mas simple, para confirmar que no
--    solo el texto de la vista esta bien sino que tambien resuelve en runtime.
SELECT COUNT(*) AS filas_clientes FROM analitica.clientes;
-- Esperado: no lanza error (el conteo en si puede ser 0 si la base esta vacia).
