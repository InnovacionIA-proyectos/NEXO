-- =============================================================================
-- Cambios que propone el asistente de cotización (y que la persona confirma)
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código actual sigue funcionando sin ella.
-- Requiere las tablas del script 372_asistente_conversaciones.sql (conversaciones y mensajes del asistente).
--
-- El asistente nunca escribe en NEXO: cuando propone un cambio (agregar o editar un flete, agregar conexos, rechazar una línea o cambiar el
-- vehículo) deja una fila aquí, con parámetros fijos, y la persona la confirma o la cancela. Cada fila es también la auditoría de la
-- escritura: quién, cuándo, qué y cómo terminó (ok, o el código con el que una regla del dominio la rechazó).
--
-- ORDEN: aplicar ESTE script (después del 372) antes de desplegar el código del asistente. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_asistente_acciones];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_asistente_acciones] (
        [Id] uniqueidentifier NOT NULL,
        [ConversacionId] uniqueidentifier NOT NULL,
        -- La respuesta del asistente en la que se propuso.
        [MensajeId] uniqueidentifier NOT NULL,
        -- Quién puede confirmarla: la persona de la conversación y nadie más.
        [UsuarioId] uniqueidentifier NOT NULL,
        -- agregar_flete, editar_flete, agregar_conexos, rechazar_linea o cambiar_vehiculo.
        [Tipo] nvarchar(40) NOT NULL,
        -- Los parámetros del cambio como JSON, tal como los validó el servidor: lo que se confirma es exactamente esto.
        [Parametros] nvarchar(max) NOT NULL,
        -- Lo que se le muestra a la persona para que decida.
        [Resumen] nvarchar(500) NOT NULL,
        -- pendiente, ejecutando, ejecutada, fallida (una regla del dominio la rechazó), cancelada o vencida.
        [Estado] nvarchar(20) NOT NULL CONSTRAINT [DF_asistente_acciones_Estado] DEFAULT (N'pendiente'),
        [VenceAt] datetimeoffset NOT NULL,
        [ResueltaAt] datetimeoffset NULL,
        -- 'ok' o el código del rechazo (FLETE_SOLO_SICETAC, COTIZACION_NO_EDITABLE, FLETE_BAJO_PISO_SICETAC…).
        [ResultadoCodigo] nvarchar(60) NULL,
        [ResultadoMensaje] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_acciones_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_asistente_acciones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_asistente_acciones_tipo] CHECK ([Tipo] IN (N'agregar_flete', N'editar_flete', N'agregar_conexos', N'rechazar_linea', N'cambiar_vehiculo')),
        CONSTRAINT [CK_asistente_acciones_estado] CHECK ([Estado] IN (N'pendiente', N'ejecutando', N'ejecutada', N'fallida', N'cancelada', N'vencida')),
        CONSTRAINT [CK_asistente_acciones_parametros_json] CHECK (ISJSON([Parametros]) = 1),
        CONSTRAINT [FK_asistente_acciones_conversaciones_ConversacionId] FOREIGN KEY ([ConversacionId]) REFERENCES [db_NEXO].[tbl_asistente_conversaciones] ([Id]),
        CONSTRAINT [FK_asistente_acciones_mensajes_MensajeId] FOREIGN KEY ([MensajeId]) REFERENCES [db_NEXO].[tbl_asistente_mensajes] ([Id]),
        CONSTRAINT [FK_asistente_acciones_usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id])
    );
END
GO

-- Las acciones de una conversación, en el orden en que se propusieron.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_acciones_ConversacionId_CreatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]'))
    CREATE INDEX [IX_asistente_acciones_ConversacionId_CreatedAt] ON [db_NEXO].[tbl_asistente_acciones] ([ConversacionId], [CreatedAt]);
GO

-- Las acciones de una respuesta (para limitar cuántas puede proponer).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_acciones_MensajeId' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]'))
    CREATE INDEX [IX_asistente_acciones_MensajeId] ON [db_NEXO].[tbl_asistente_acciones] ([MensajeId]);
GO

-- Verificación (solo lectura). Resultado esperado: tablas=1, columnas=13, indices=2, llaves_foraneas=3, restricciones_check=3.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE name LIKE N'IX_asistente_acciones_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE N'FK_asistente_acciones_%') AS llaves_foraneas,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_acciones]')) AS restricciones_check;
GO
