-- =============================================================================
-- tbl_mapeo_vehiculo_tarifario — código de cada tipo de vehículo en SICE-TAC y en Plaza/SAP
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA, con su carga inicial. No toca ninguna tabla existente, así que el código actual sigue
-- funcionando sin ella.
--
-- Para qué sirve: SICE-TAC y Plaza/SAP nombran el mismo vehículo con códigos distintos. Hasta ahora NEXO usaba el mismo
-- código para consultar las dos fuentes, y cuando no encontraba filas tomaba la ruta de cualquier vehículo y se quedaba con la
-- más barata, sin avisar. Esta tabla dice, por cada tipo de vehículo, con qué código se busca en cada fuente.
--   TipoVehiculo       : código del vehículo en NEXO (el que queda después de resolver los alias). Llave.
--   CodigoSicetac      : valor de CONFIGURACION en DWSice.dbo.tblRutasSicetac.
--   NombreSicetac      : cómo describe SICE-TAC esa configuración (incluye el rango de peso bruto vehicular).
--   CodigoPlazaSap     : código del vehículo en Plaza/SAP. Es el mismo para tblSAPTarifas y para tbl_tarifas_plaza.
--                        Se guarda en texto; tblSAPTarifas filtra por el ID del catálogo de tipos de vehículo de SAP.
--                        NULL = el vehículo no tiene tarifa de plaza (solo existe en SICE-TAC).
--   NombreTarifasPlaza : cómo se le dice en Plaza/SAP (SENCILLO, TURBO, Minimula…).
--   Verificacion       : de dónde salió el código y cómo se confirmó.
--
-- Decisiones confirmadas al construir la carga (2026-10-27):
--   * Sencillo (C2): en Plaza/SAP el código es 2, no C2. Hay unas 5.000 filas de tarifa con 2 y solo 14, todas vencidas,
--     con C2. En SICE-TAC también es 2.
--   * Turbo: en Plaza/SAP 2L1, 2L2 y 2L3 se tarifan como 2L2 (TURBO). En SICE-TAC cada uno conserva su código. El 2_7 que
--     apareció en el análisis del Excel de un cliente (2026-10-26) fue una excepción puntual, confirmada con el Administrador
--     de Tarifas; no es la regla.
--
-- RELACIÓN CON OTRAS TABLAS:
--   * tbl_alias_elementos traduce el texto libre (Turbo, TB, SLLO…) al TipoVehiculo de NEXO. Esta tabla parte de ese
--     TipoVehiculo y da el código de cada fuente. Primero se resuelve el alias y después se consulta aquí.
--   * tbl_diccionario_cliente guarda términos propios de un cliente. Esta tabla es global: aplica a todos los clientes.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código que consulte la tabla. Es idempotente (se puede correr dos veces):
-- crea la tabla si no existe y la carga con MERGE (inserta lo que falta y actualiza lo que cambió, sin duplicar).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
-- Fuente de la carga: docs/Database/seeder/mapeo_vehiculos.csv.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_mapeo_vehiculo_tarifario];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_mapeo_vehiculo_tarifario]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_mapeo_vehiculo_tarifario] (
        [Id] uniqueidentifier NOT NULL,
        [TipoVehiculo] nvarchar(20) NOT NULL,
        [CodigoSicetac] nvarchar(20) NOT NULL,
        [NombreSicetac] nvarchar(200) NULL,
        [CodigoPlazaSap] nvarchar(20) NULL,
        [NombreTarifasPlaza] nvarchar(100) NULL,
        [Verificacion] nvarchar(500) NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_mapeo_vehiculo_tarifario_Activo] DEFAULT (1),
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_mapeo_vehiculo_tarifario_CreatedAt] DEFAULT (sysdatetimeoffset()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_mapeo_vehiculo_tarifario] PRIMARY KEY ([Id])
    );
END;

-- Un solo mapeo por tipo de vehículo; también es la búsqueda que hace el backend.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_mapeo_vehiculo_tarifario_TipoVehiculo' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_mapeo_vehiculo_tarifario]'))
    CREATE UNIQUE INDEX [IX_mapeo_vehiculo_tarifario_TipoVehiculo]
        ON [db_NEXO].[tbl_mapeo_vehiculo_tarifario] ([TipoVehiculo]);

-- Carga inicial (15 vehículos).
MERGE [db_NEXO].[tbl_mapeo_vehiculo_tarifario] AS t
USING (VALUES
    (N'C2',  N'2',   N'Camión dos ejes - PBV mas de 10500 Kg',                       N'2',   N'SENCILLO',
        N'Plaza/SAP usa 2 (unas 5.000 filas de tarifa); C2 solo aparece en 14 filas vencidas. SICE-TAC: 2.'),
    (N'2_5', N'2_5', N'Propuesta camion dos ejes - Livianos PBV 5001-7000 Kg',       NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.'),
    (N'2_7', N'2_7', N'Propuesta camion dos ejes - Livianos PBV 7001-9000 Kg',       NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza. No es el código del Turbo: el caso del análisis 2026-10-26 fue una excepción puntual (Administrador de Tarifas).'),
    (N'2L1', N'2L1', N'Camión dos ejes - Livianos PBV 9001-10500 Kg',                N'2L2', N'TURBO',
        N'En Plaza/SAP se tarifa como Turbo (2L2).'),
    (N'2L2', N'2L2', N'Camión dos ejes - Livianos PBV 8001-9000 Kg',                 N'2L2', N'TURBO',
        N'Turbo. Mismo código en SICE-TAC y en Plaza/SAP.'),
    (N'2L3', N'2L3', N'Camión dos ejes - Livianos PBV 7500-8000 Kg',                 N'2L2', N'TURBO',
        N'En Plaza/SAP se tarifa como Turbo (2L2).'),
    (N'2S2', N'2S2', N'Tractocamión dos ejes con semiremolque de dos ejes',          N'2S2', N'Minimula',
        N'Mismo código en SICE-TAC y en Plaza/SAP.'),
    (N'2S3', N'2S3', N'Tractocamión dos ejes con semiremolque de tres ejes',         N'2S3', N'minimula poderosa',
        N'Mismo código en SICE-TAC y en Plaza/SAP.'),
    (N'C3',  N'3',   N'camión tres ejes',                                            N'C3',  N'Dobletroque',
        N'SICE-TAC usa 3; Plaza/SAP usa C3.'),
    (N'3S2', N'3S2', N'Tractocamión tres ejes con semiremolque de dos ejes',         NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.'),
    (N'3S3', N'3S3', N'Tractocamión tres ejes con semiremolque de tres ejes',        N'3S3', N'Tractomula',
        N'Mismo código en SICE-TAC y en Plaza/SAP.'),
    (N'CA_', N'CA_', N'Propuesta de camioneta PBV  3500-5000 kg',                    NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.'),
    (N'V2',  N'V2',  N'Volqueta dos ejes',                                           NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.'),
    (N'V3',  N'V3',  N'Volqueta tres ejes',                                          NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.'),
    (N'V4',  N'V4',  N'Volqueta cuatro ejes',                                        NULL,   NULL,
        N'Solo existe en SICE-TAC; sin tarifa de plaza.')
) AS s ([TipoVehiculo], [CodigoSicetac], [NombreSicetac], [CodigoPlazaSap], [NombreTarifasPlaza], [Verificacion])
ON t.[TipoVehiculo] = s.[TipoVehiculo]
WHEN MATCHED AND EXISTS (
        SELECT s.[CodigoSicetac], s.[NombreSicetac], s.[CodigoPlazaSap], s.[NombreTarifasPlaza], s.[Verificacion]
        EXCEPT
        SELECT t.[CodigoSicetac], t.[NombreSicetac], t.[CodigoPlazaSap], t.[NombreTarifasPlaza], t.[Verificacion])
    THEN UPDATE SET
        [CodigoSicetac] = s.[CodigoSicetac],
        [NombreSicetac] = s.[NombreSicetac],
        [CodigoPlazaSap] = s.[CodigoPlazaSap],
        [NombreTarifasPlaza] = s.[NombreTarifasPlaza],
        [Verificacion] = s.[Verificacion],
        [UpdatedAt] = sysdatetimeoffset()
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([Id], [TipoVehiculo], [CodigoSicetac], [NombreSicetac], [CodigoPlazaSap], [NombreTarifasPlaza], [Verificacion])
         VALUES (NEWID(), s.[TipoVehiculo], s.[CodigoSicetac], s.[NombreSicetac], s.[CodigoPlazaSap], s.[NombreTarifasPlaza], s.[Verificacion]);

-- Verificación (solo lectura): debe devolver 1 tabla, 10 columnas, 1 índice propio, 15 filas y 8 con código de plaza.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_mapeo_vehiculo_tarifario]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_mapeo_vehiculo_tarifario]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_mapeo_vehiculo_tarifario]') AND name LIKE N'IX_mapeo_vehiculo_tarifario_%') AS indices,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_mapeo_vehiculo_tarifario]) AS filas,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_mapeo_vehiculo_tarifario] WHERE [CodigoPlazaSap] IS NOT NULL) AS filas_con_plaza;
