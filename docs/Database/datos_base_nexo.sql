-- =============================================================================
-- Datos base de NEXO — filas sin las que una base nueva no funciona
-- =============================================================================
-- Qué es: las filas que el modelo del backend declara con HasData en clsNexoDbContext. Como este proyecto NO usa
-- migraciones de EF Core, HasData nunca se ejecuta solo: en una base nueva hay que insertarlas con este script.
--   * tbl_usuarios: el usuario "Sistema (placeholder)" (Id ...0001). Es el autor de lo que hace el sistema por sí solo.
--   * tbl_mapeo_rol_portal: los 9 mapeos de rol del portal -> rol de NEXO. Sin ellos, quien entra desde el portal se
--     autentica pero queda sin rol (403).
--   * tbl_parametros_score: la fila única (Id 1) con los pesos y constantes del score de prioridad.
-- Los valores son los de la base de pruebas al 2026-10-27, iguales a los del HasData.
--
-- ORDEN: después de ESQUEMA_NEXO.sql y antes de cargar los catálogos (ver docs/Database/seeder/README.md).
-- Es idempotente: inserta solo lo que falta y no modifica filas que ya existan (por ejemplo, parámetros del score que un
-- Admin ya haya ajustado). Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
-- =============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_usuarios] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
    INSERT INTO [db_NEXO].[tbl_usuarios] ([Id], [Nombre], [Email], [Rol], [Activo], [CreatedAt], [UpdatedAt], [IdentidadPortalId])
    VALUES ('00000000-0000-0000-0000-000000000001', N'Sistema (placeholder)', N'system@boterosoto.com.co', N'Admin', 1,
            '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00', NULL);

-- Los 3 primeros traducen el rol por su nombre (IdRolPortal NULL); los demás, por el ID real del rol en el portal
-- (IDROLES de tblSAPRoles, dbSolucionesTI). El rol del portal 49 conserva el nombre "CoordinadorFletesNEXO" y se traduce a
-- AdminBiblioteca (#307).
MERGE [db_NEXO].[tbl_mapeo_rol_portal] AS t
USING (VALUES
    ('00000000-0000-0000-0001-000000000001', NULL, N'TarifasComercial',        N'Comercial',           '2026-01-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000002', NULL, N'TarifasAdministrador',    N'AdminTarifas',        '2026-01-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000003', NULL, N'Administracion',          N'Admin',               '2026-01-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000004', 48,   N'ComercialNEXO',           N'Comercial',           '2026-09-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000005', 49,   N'CoordinadorFletesNEXO',   N'AdminBiblioteca',     '2026-09-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000006', 50,   N'AdminTarifasNEXO',        N'AdminTarifas',        '2026-09-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000007', 51,   N'ServiciosLogisticosNEXO', N'ServiciosLogisticos', '2026-09-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000008', 52,   N'LiderProcesoNEXO',        N'LiderProceso',        '2026-09-01T00:00:00+00:00'),
    ('00000000-0000-0000-0001-000000000009', 53,   N'AdminNEXO',               N'Admin',               '2026-09-01T00:00:00+00:00')
) AS s ([Id], [IdRolPortal], [DescripcionRolPortal], [Rol], [CreatedAt])
ON t.[Id] = s.[Id]
WHEN NOT MATCHED BY TARGET
    THEN INSERT ([Id], [IdRolPortal], [DescripcionRolPortal], [Rol], [CreatedAt])
         VALUES (s.[Id], s.[IdRolPortal], s.[DescripcionRolPortal], s.[Rol], s.[CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_parametros_score] WHERE [Id] = 1)
BEGIN
    SET IDENTITY_INSERT [db_NEXO].[tbl_parametros_score] ON;
    INSERT INTO [db_NEXO].[tbl_parametros_score] (
        [Id], [PesoUrgencia], [PesoValor], [PesoProbabilidad], [PesoTipo], [HorizonteHoras], [ValorReferencia],
        [PuntajeLicitacion], [PuntajeSpot], [ValorPorDefectoSinValor], [ProbabilidadPorDefecto], [CreatedAt], [UpdatedAt])
    VALUES (1, 0.4, 0.3, 0.2, 0.1, 240, 100000000.00, 100, 50, 30, 50,
            '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');
    SET IDENTITY_INSERT [db_NEXO].[tbl_parametros_score] OFF;
END;

-- Verificación (solo lectura): debe devolver 1 usuario de sistema, 9 mapeos de rol y 1 fila de parámetros.
SELECT
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_usuarios] WHERE [Id] = '00000000-0000-0000-0000-000000000001') AS usuario_sistema,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_mapeo_rol_portal]) AS mapeos_rol,
    (SELECT COUNT(*) FROM [db_NEXO].[tbl_parametros_score]) AS parametros_score;
