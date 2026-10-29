-- =============================================================================
-- Conversaciones del asistente de cotización y auditoría de sus herramientas
-- =============================================================================
-- Qué es: tres tablas NUEVAS y ADITIVAS. No tocan ninguna tabla existente, así que el código actual sigue funcionando sin ellas.
-- Sin estas tablas solo fallará el endpoint de conversación del asistente (cuando se despliegue).
--
--   tbl_asistente_conversaciones   una por conversación; es de quien la inicia (UsuarioId).
--   tbl_asistente_mensajes         los mensajes de cada conversación, en orden (Orden es único dentro de ella).
--   tbl_asistente_herramientas_uso una fila por cada herramienta que el asistente usó: cuál, con qué entrada, qué devolvió y cómo terminó.
--
-- Las conversaciones se guardan en NEXO (no en el proveedor de IA) porque son la prueba de dónde salió cada valor que el asistente
-- mostró. Pueden contener el texto que escribe el usuario; tienen el mismo trato que cualquier dato de NEXO.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código del asistente. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta, en este orden):
--   DROP TABLE [db_NEXO].[tbl_asistente_herramientas_uso];
--   DROP TABLE [db_NEXO].[tbl_asistente_mensajes];
--   DROP TABLE [db_NEXO].[tbl_asistente_conversaciones];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_asistente_conversaciones]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_asistente_conversaciones] (
        [Id] uniqueidentifier NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        -- Las primeras palabras del primer mensaje, para reconocerla en un listado.
        [Titulo] nvarchar(120) NOT NULL,
        -- Mensajes del usuario enviados; es lo que se limita por conversación.
        [Turnos] int NOT NULL CONSTRAINT [DF_asistente_conversaciones_Turnos] DEFAULT (0),
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_conversaciones_CreatedAt] DEFAULT (sysdatetimeoffset()),
        [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_conversaciones_UpdatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_asistente_conversaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_asistente_conversaciones_turnos] CHECK ([Turnos] >= 0),
        CONSTRAINT [FK_asistente_conversaciones_usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id])
    );
END
GO

IF OBJECT_ID(N'[db_NEXO].[tbl_asistente_mensajes]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_asistente_mensajes] (
        [Id] uniqueidentifier NOT NULL,
        [ConversacionId] uniqueidentifier NOT NULL,
        [Orden] int NOT NULL,
        -- 'user' o 'assistant'.
        [Rol] nvarchar(10) NOT NULL,
        [Contenido] nvarchar(max) NOT NULL CONSTRAINT [DF_asistente_mensajes_Contenido] DEFAULT (N''),
        -- 'en_curso' (se está generando), 'completo', 'incompleto' (se cortó) o 'descartado' (traía cifras sin origen y se reemplazó por un aviso).
        [Estado] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_mensajes_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_asistente_mensajes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_asistente_mensajes_rol] CHECK ([Rol] IN (N'user', N'assistant')),
        CONSTRAINT [CK_asistente_mensajes_estado] CHECK ([Estado] IN (N'en_curso', N'completo', N'incompleto', N'descartado')),
        CONSTRAINT [FK_asistente_mensajes_conversaciones_ConversacionId] FOREIGN KEY ([ConversacionId]) REFERENCES [db_NEXO].[tbl_asistente_conversaciones] ([Id])
    );
END
GO

IF OBJECT_ID(N'[db_NEXO].[tbl_asistente_herramientas_uso]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_asistente_herramientas_uso] (
        [Id] uniqueidentifier NOT NULL,
        [ConversacionId] uniqueidentifier NOT NULL,
        -- La respuesta del asistente en cuyo transcurso se usó la herramienta.
        [MensajeId] uniqueidentifier NOT NULL,
        [Herramienta] nvarchar(60) NOT NULL,
        -- La entrada que mandó el modelo y lo que devolvió la herramienta, como JSON. Salida es NULL si falló antes de devolver algo.
        [Entrada] nvarchar(max) NOT NULL,
        [Salida] nvarchar(max) NULL,
        [Encontrado] bit NULL,
        [Fuente] nvarchar(200) NULL,
        -- 'ok' o el código del error (HERRAMIENTA_NO_PERMITIDA, SOLICITUD_AJENA, HERRAMIENTA_ERROR…).
        [Resultado] nvarchar(40) NOT NULL,
        [LatenciaMs] int NOT NULL CONSTRAINT [DF_asistente_herramientas_uso_LatenciaMs] DEFAULT (0),
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_asistente_herramientas_uso_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_asistente_herramientas_uso] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_asistente_herramientas_uso_entrada_json] CHECK (ISJSON([Entrada]) = 1),
        CONSTRAINT [CK_asistente_herramientas_uso_salida_json] CHECK ([Salida] IS NULL OR ISJSON([Salida]) = 1),
        CONSTRAINT [FK_asistente_herramientas_uso_conversaciones_ConversacionId] FOREIGN KEY ([ConversacionId]) REFERENCES [db_NEXO].[tbl_asistente_conversaciones] ([Id]),
        CONSTRAINT [FK_asistente_herramientas_uso_mensajes_MensajeId] FOREIGN KEY ([MensajeId]) REFERENCES [db_NEXO].[tbl_asistente_mensajes] ([Id])
    );
END
GO

-- Las conversaciones de una persona, de la más reciente a la más antigua.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_conversaciones_UsuarioId_UpdatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_conversaciones]'))
    CREATE INDEX [IX_asistente_conversaciones_UsuarioId_UpdatedAt] ON [db_NEXO].[tbl_asistente_conversaciones] ([UsuarioId], [UpdatedAt]);
GO

-- Los mensajes de una conversación en orden; el orden no se repite dentro de ella.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_mensajes_ConversacionId_Orden' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_mensajes]'))
    CREATE UNIQUE INDEX [IX_asistente_mensajes_ConversacionId_Orden] ON [db_NEXO].[tbl_asistente_mensajes] ([ConversacionId], [Orden]);
GO

-- Las herramientas usadas en una conversación.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_herramientas_uso_ConversacionId_CreatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_herramientas_uso]'))
    CREATE INDEX [IX_asistente_herramientas_uso_ConversacionId_CreatedAt] ON [db_NEXO].[tbl_asistente_herramientas_uso] ([ConversacionId], [CreatedAt]);
GO

-- Para ubicar rápido las herramientas de una respuesta (una FK no crea índice sola).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_asistente_herramientas_uso_MensajeId' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_herramientas_uso]'))
    CREATE INDEX [IX_asistente_herramientas_uso_MensajeId] ON [db_NEXO].[tbl_asistente_herramientas_uso] ([MensajeId]);
GO

-- Verificación (solo lectura). Resultado esperado: tablas=3, columnas_conversaciones=6, columnas_mensajes=7,
-- columnas_herramientas=11, indices=4, llaves_foraneas=4.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'db_NEXO') AND name LIKE N'tbl_asistente_%') AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_conversaciones]')) AS columnas_conversaciones,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_mensajes]')) AS columnas_mensajes,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_asistente_herramientas_uso]')) AS columnas_herramientas,
    (SELECT COUNT(*) FROM sys.indexes WHERE name LIKE N'IX_asistente_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE N'FK_asistente_%') AS llaves_foraneas;
GO
