-- =============================================================================
-- S5-HU-17.2 — Corrección de dos cambios que no correspondían
-- =============================================================================
-- `S5-HU-17.2_ajustes_esquema.sql` se aplicó sin tener en cuenta dos decisiones
-- que el equipo de Datos ya había tomado y dejado escritas en el issue #267:
--
--   1. Los 10 tipos de mercancía con `Cubicable = 0` NO se siembran.
--      `tiposMercancia` se queda con las 31 filas reales de la matriz. Si
--      alguna vez hace falta rechazar graneles o cemento con motivo, entra
--      por CRUD cuando se necesite.
--
--   2. Se evitan las columnas NULL. Si hay un valor por defecto razonable se
--      usa; no se deja un tercer estado "no declarado". Las 6 columnas que se
--      agregaron a `recomendacionVehiculo` quedaron nullable.
--
-- Este script deshace ambas cosas. Es seguro: las filas a borrar son
-- identificables por `CreadoPor` y las tablas de recomendación están vacías.
-- Idempotente.
-- =============================================================================


-- 1 --------------------------------------------------------------------------
-- Quitar los 10 tipos excluidos. Se identifican por CreadoPor, no por código,
-- para no tocar nada que el equipo haya sembrado con esos mismos nombres.
DELETE FROM dbo_cubicador.tiposMercancia
 WHERE CreadoPor = 'S5-HU-17.2' AND Cubicable = 0;
GO


-- 2 --------------------------------------------------------------------------
-- Las 6 columnas pasan a NOT NULL. Sin default a propósito: el motor escribe
-- cada fila completa de una sola vez, así que un default nunca se usaría en la
-- práctica y solo serviría para que una fila incompleta pase inadvertida. Sin
-- él, el INSERT falla y el error aparece donde está la causa.
--
-- Seguro de aplicar: `recomendacionVehiculo` está vacía, así que no hay filas
-- existentes que quedarían en falta.
--
-- `IdRecomendacion` necesita que se suelte antes su índice: SQL Server no deja
-- cambiar la nulabilidad de una columna que un índice referencia (error 5074).
IF EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = 'IX_recomendacionVehiculo_IdRecomendacion'
              AND object_id = OBJECT_ID('dbo_cubicador.recomendacionVehiculo'))
    DROP INDEX IX_recomendacionVehiculo_IdRecomendacion
        ON dbo_cubicador.recomendacionVehiculo;
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN IdRecomendacion UNIQUEIDENTIFIER NOT NULL;
GO
CREATE INDEX IX_recomendacionVehiculo_IdRecomendacion
    ON dbo_cubicador.recomendacionVehiculo (IdRecomendacion);
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN PorcentajeLlenadoVolumen DECIMAL(5,2) NOT NULL;
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN PorcentajeLlenadoPeso DECIMAL(5,2) NOT NULL;
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN PesoTotalKg DECIMAL(18,2) NOT NULL;
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN VolumenTotalM3 DECIMAL(18,3) NOT NULL;
GO
ALTER TABLE dbo_cubicador.recomendacionVehiculo
    ALTER COLUMN NumeroVehiculo INT NOT NULL;
GO


-- Verificación -----------------------------------------------------------------
SELECT 'tipos de mercancia' AS que, COUNT(*) AS valor FROM dbo_cubicador.tiposMercancia
UNION ALL
SELECT 'de esos, no cubicables', COUNT(*) FROM dbo_cubicador.tiposMercancia WHERE Cubicable = 0
UNION ALL
SELECT 'columnas nuevas que siguen nullable',
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo_cubicador.recomendacionVehiculo')
           AND is_nullable = 1
           AND name IN ('IdRecomendacion','PorcentajeLlenadoVolumen','PorcentajeLlenadoPeso',
                        'PesoTotalKg','VolumenTotalM3','NumeroVehiculo'));
-- Esperado: 31 tipos, 0 no cubicables, 0 columnas nullable.


-- 3 --------------------------------------------------------------------------
-- Las 3 columnas opcionales de `recomendaciones` también pasan a NOT NULL con
-- default, siguiendo el mismo principio y el patrón que el esquema ya usa en
-- `ControlAmbientalRequerido NOT NULL DEFAULT (N'NINGUNO')`.
--
-- Una corrida sin motivo, sin advertencias y sin extradimensión es el caso
-- normal: con estos valores queda representado sin un tercer estado.
IF EXISTS (SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID('dbo_cubicador.recomendaciones')
              AND name = 'MotivoSinSeleccion' AND is_nullable = 1)
BEGIN
    UPDATE dbo_cubicador.recomendaciones SET MotivoSinSeleccion = N'NINGUNO'
     WHERE MotivoSinSeleccion IS NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ALTER COLUMN MotivoSinSeleccion NVARCHAR(500) COLLATE Modern_Spanish_CI_AS NOT NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ADD CONSTRAINT DF_recomendaciones_MotivoSinSeleccion
            DEFAULT (N'NINGUNO') FOR MotivoSinSeleccion;

    UPDATE dbo_cubicador.recomendaciones SET AdvertenciasJson = N'[]'
     WHERE AdvertenciasJson IS NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ALTER COLUMN AdvertenciasJson NVARCHAR(MAX) COLLATE Modern_Spanish_CI_AS NOT NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ADD CONSTRAINT DF_recomendaciones_AdvertenciasJson
            DEFAULT (N'[]') FOR AdvertenciasJson;

    UPDATE dbo_cubicador.recomendaciones SET ExtradimensionJson = N'{}'
     WHERE ExtradimensionJson IS NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ALTER COLUMN ExtradimensionJson NVARCHAR(MAX) COLLATE Modern_Spanish_CI_AS NOT NULL;
    ALTER TABLE dbo_cubicador.recomendaciones
        ADD CONSTRAINT DF_recomendaciones_ExtradimensionJson
            DEFAULT (N'{}') FOR ExtradimensionJson;
END;
GO

SELECT 'columnas nullable en recomendaciones' AS que,
       COUNT(*) AS valor
  FROM sys.columns
 WHERE object_id = OBJECT_ID('dbo_cubicador.recomendaciones') AND is_nullable = 1;
-- Esperado: 0.
