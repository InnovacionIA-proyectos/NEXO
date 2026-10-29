-- =============================================================================
-- tbl_rangos_peso_contenedor — qué vehículo lleva un contenedor de 20 o 40 pies según su peso
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA, con su carga inicial. No toca ninguna tabla existente, así que el código actual sigue
-- funcionando sin ella (hoy el backend lee los rangos de las tablas de SAP tblRangoPeso y tblContenedorRangoPeso).
--
-- Para qué sirve: en una línea de Contenedor, NEXO escoge el vehículo según el tamaño del contenedor y su peso.
--   Contenedor   : tamaño en pies (20 o 40).
--   TipoVehiculo : código de vehículo de NEXO, el MISMO de tbl_mapeo_vehiculo_tarifario.TipoVehiculo (#377). No es el código
--                  de SICE-TAC ni el de Plaza/SAP: para buscar en esas fuentes se traduce con el mapeo (ej. C2 -> 2). La llave
--                  foránea hacia el mapeo impide guardar un vehículo que no esté mapeado.
--   PesoMaximo   : peso máximo del contenedor (kg) que puede llevar ese vehículo.
--
-- Cómo se escoge el vehículo: entre las filas del tamaño de contenedor, la de MENOR PesoMaximo que sea mayor o igual al peso.
-- En cada tamaño los topes van subiendo sin repetirse, así que no hace falta peso mínimo. Si el peso supera el tope más alto
-- (34.000 kg en 40 pies, 30.000 kg en 20 pies), ningún vehículo aplica.
--
-- Fuente de la carga: docs/Database/seeder/rangos_peso_contenedor.csv, que sale de RANGOS-PESO-CONTENEDORES-NORMALIZADO.xlsx
-- (hoja "Fletes puertos" del documento de negocio). Rangos confirmados por Sara Calderón (2026-10-27). La fila del camión de
-- dos ejes en 40 pies, que no tenía tope de peso en el documento original, se dejó por fuera.
--
-- ORDEN: aplicar DESPUÉS de 377_mapeo_vehiculo_tarifario.sql (la llave foránea apunta a esa tabla) y antes de desplegar el
-- código que consulte esta. Es idempotente (se puede correr dos veces): crea la tabla si no existe y la carga con MERGE.
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_rangos_peso_contenedor];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_rangos_peso_contenedor] (
        [Id] uniqueidentifier NOT NULL,
        [Contenedor] int NOT NULL,
        [TipoVehiculo] nvarchar(20) NOT NULL,
        [PesoMaximo] int NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_rangos_peso_contenedor_Activo] DEFAULT (1),
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_rangos_peso_contenedor_CreatedAt] DEFAULT (sysdatetimeoffset()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_rangos_peso_contenedor] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_rangos_peso_contenedor_Contenedor] CHECK ([Contenedor] IN (20, 40)),
        CONSTRAINT [CK_rangos_peso_contenedor_PesoMaximo] CHECK ([PesoMaximo] > 0),
        CONSTRAINT [FK_rangos_peso_contenedor_mapeo_vehiculo_tarifario_TipoVehiculo] FOREIGN KEY ([TipoVehiculo])
            REFERENCES [db_NEXO].[tbl_mapeo_vehiculo_tarifario] ([TipoVehiculo])
    );
END;

-- Un solo rango por tamaño de contenedor y vehículo; también es la búsqueda que hace el backend.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_rangos_peso_contenedor_Contenedor_TipoVehiculo' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]'))
    CREATE UNIQUE INDEX [IX_rangos_peso_contenedor_Contenedor_TipoVehiculo]
        ON [db_NEXO].[tbl_rangos_peso_contenedor] ([Contenedor], [TipoVehiculo]);

-- Carga inicial (10 rangos).
MERGE [db_NEXO].[tbl_rangos_peso_contenedor] AS t
USING (VALUES
    (40, N'2L3',  8000),
    (40, N'2S2', 18000),
    (40, N'2S3', 23000),
    (40, N'3S2', 30000),
    (40, N'3S3', 34000),
    (20, N'C2',   8000),
    (20, N'2S2', 14000),
    (20, N'2S3', 18000),
    (20, N'3S2', 25000),
    (20, N'3S3', 30000)
) AS s ([Contenedor], [TipoVehiculo], [PesoMaximo])
ON t.[Contenedor] = s.[Contenedor] AND t.[TipoVehiculo] = s.[TipoVehiculo]
WHEN MATCHED AND t.[PesoMaximo] <> s.[PesoMaximo]
    THEN UPDATE SET
        [PesoMaximo] = s.[PesoMaximo],
        [UpdatedAt] = sysdatetimeoffset()
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([Id], [Contenedor], [TipoVehiculo], [PesoMaximo])
         VALUES (NEWID(), s.[Contenedor], s.[TipoVehiculo], s.[PesoMaximo]);

-- Verificación (solo lectura): debe devolver 1 tabla, 7 columnas, 1 índice propio, 1 llave foránea, 2 restricciones CHECK,
-- 10 filas (5 de 40 pies y 5 de 20 pies).
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]') AND name LIKE N'IX_rangos_peso_contenedor_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]')) AS llaves_foraneas,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_rangos_peso_contenedor]')) AS restricciones_check,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_rangos_peso_contenedor]) AS filas,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_rangos_peso_contenedor] WHERE [Contenedor] = 40) AS filas_40,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_rangos_peso_contenedor] WHERE [Contenedor] = 20) AS filas_20;
