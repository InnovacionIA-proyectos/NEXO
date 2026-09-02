-- Verificación QA — PR #90 ("Actualizacion de docker compose test preproduccion").
--
-- El PR agrega la migración MoverEsquemaDboNexoPrueba, que mueve las 27 tablas de
-- NEXO de `dbo` a un esquema nuevo `dbo_NEXO_prueba` (para compartir la BD de
-- pruebas real con otra app que ya usa `dbo`). Las 13 vistas `analitica.*`
-- (HU-10.1, PR #82, YA MERGEADO A QA) fueron creadas con SQL crudo que referencia
-- `dbo.<tabla>` literalmente (ver migración VistasAnaliticaSoloLectura.cs) — no usan
-- el esquema por defecto de EF Core, así que NO se mueven con las tablas.
--
-- Este script confirma que las 13 vistas quedan rotas después de aplicar la
-- migración de este PR.
--
-- Cómo correrlo: levantar la app de esta rama con migraciones aplicadas
-- (Database__MigrateOnStartup=true) contra cualquier SQL Server, luego:
--   docker exec -i <contenedor-db> /opt/mssql-tools18/bin/sqlcmd -C -S localhost \
--     -U sa -P '<password>' -d CabinaFletes -i verify-analitica-tras-mover-esquema.sql

PRINT '--- dónde quedó la tabla solicitudes tras la migración ---';
SELECT s.name AS esquema, t.name AS tabla
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name = 'solicitudes';

PRINT '--- probando las 13 vistas analitica.* (todas deberían fallar) ---';
GO
SELECT COUNT(*) FROM analitica.clientes;
GO
SELECT COUNT(*) FROM analitica.rutas;
GO
SELECT COUNT(*) FROM analitica.configuraciones_vehiculo;
GO
SELECT COUNT(*) FROM analitica.servicios_conexos;
GO
SELECT COUNT(*) FROM analitica.usuarios;
GO
SELECT COUNT(*) FROM analitica.solicitudes;
GO
SELECT COUNT(*) FROM analitica.lineas_solicitud;
GO
SELECT COUNT(*) FROM analitica.cotizaciones;
GO
SELECT COUNT(*) FROM analitica.cotizacion_lineas;
GO
SELECT COUNT(*) FROM analitica.cotizacion_conexos;
GO
SELECT COUNT(*) FROM analitica.tarifas_historicas;
GO
SELECT COUNT(*) FROM analitica.audit_log;
GO
SELECT COUNT(*) FROM analitica.inconsistencias;
GO
