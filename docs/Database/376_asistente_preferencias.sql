-- =============================================================================
-- Reglas y preferencias propias de cada persona para el asistente de cotización
-- =============================================================================
-- Qué es: una tabla NUEVA y un ajuste de una restricción de una tabla del propio asistente. No toca ninguna otra tabla de NEXO, así que el
-- código actual sigue funcionando sin ella.
--
--   tbl_asistente_preferencias   una fila por preferencia ("avísame si plaza e histórico difieren más de 20 %"); es de quien la guarda.
--                                El asistente la recibe al inicio de cada conversación de esa persona. Es una PREFERENCIA: nunca
--                                reemplaza las reglas del sistema (el piso SICE-TAC, que todo precio salga de una consulta, la
--                                confirmación de los cambios).
--   tbl_asistente_acciones       se agrega el tipo 'guardar_preferencia' a la restricción CK_asistente_acciones_tipo, porque guardar
--                                una preferencia se propone y la persona lo confirma, como cualquier otro cambio del asistente.
--
-- Requiere los scripts 372_asistente_conversaciones.sql y 373_asistente_acciones.sql.
-- ORDEN: aplicar ESTE script antes de desplegar el código del asistente. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_asistente_preferencias];
-- y, si se quiere volver la restricción a su forma anterior, borrar antes las acciones de tipo 'guardar_preferencia'.
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_asistente_preferencias]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_asistente_preferencias] (
        [Id] uniqueidentifier NOT NULL,
        -- De quién es: solo esa persona la ve, la usa y la borra.
        [UsuarioId] uniqueidentifier NOT NULL,
        [Texto] nvarchar(300) NOT NULL,
        -- La conversación en la que la dijo (NULL si esa conversación ya no está).
        [ConversacionId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_preferencias_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_asistente_preferencias] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_asistente_preferencias_texto] CHECK (LEN(LTRIM(RTRIM([Texto]))) >= 5),
        CONSTRAINT [FK_asistente_preferencias_usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]),
        CONSTRAINT [FK_asistente_preferencias_conversaciones_ConversacionId] FOREIGN KEY ([ConversacionId]) REFERENCES [db_NEXO].[tbl_asistente_conversaciones] ([Id])
    );
END
GO

-- Las preferencias de una persona, de la más antigua a la más reciente.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_preferencias_UsuarioId_CreatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_preferencias]'))
    CREATE INDEX [IX_asistente_preferencias_UsuarioId_CreatedAt] ON [db_NEXO].[tbl_asistente_preferencias] ([UsuarioId], [CreatedAt]);
GO

-- El tipo 'guardar_preferencia' en las acciones del asistente (solo si la restricción aún no lo incluye).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_asistente_acciones_tipo' AND parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]') AND definition NOT LIKE N'%guardar_preferencia%')
BEGIN
    ALTER TABLE [db_NEXO].[tbl_asistente_acciones] DROP CONSTRAINT [CK_asistente_acciones_tipo];
    ALTER TABLE [db_NEXO].[tbl_asistente_acciones] ADD CONSTRAINT [CK_asistente_acciones_tipo]
        CHECK ([Tipo] IN (N'agregar_flete', N'editar_flete', N'agregar_conexos', N'rechazar_linea', N'cambiar_vehiculo', N'guardar_preferencia'));
END
GO

-- Verificación (solo lectura). Resultado esperado: tablas=1, columnas=5, indices=1, llaves_foraneas=2, tipo_incluye_preferencia=1.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_preferencias]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_preferencias]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE name LIKE N'IX_asistente_preferencias_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE N'FK_asistente_preferencias_%') AS llaves_foraneas,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_asistente_acciones_tipo' AND definition LIKE N'%guardar_preferencia%') AS tipo_incluye_preferencia;
GO
