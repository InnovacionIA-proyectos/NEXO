-- =============================================================================
-- tbl_ia_uso — registro de cada llamada a un modelo de lenguaje (Claude)
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código actual sigue funcionando sin ella.
-- Sin esta tabla solo fallará el código que registre el uso de la IA (cuando se despliegue).
--
-- Una fila por llamada, escrita por el backend DESPUÉS de cada llamada (con éxito o con error). Sirve para tres cosas:
--   1. Costo: gasto por función y por mes (tokens y costo estimado al momento de la llamada).
--   2. Topes: cuánto lleva consumido cada usuario hoy.
--   3. Auditoría y evaluación: quién usó qué función, con qué modelo, cuánto tardó y cómo terminó.
--
-- NO guarda el contenido enviado ni la respuesta del modelo (ni el Excel, ni el chat): solo cifras y el resultado. Es una
-- regla de la decisión de seguridad de la IA, no un descuido.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código que registre el uso. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_ia_uso];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_ia_uso] (
        [Id] uniqueidentifier NOT NULL,
        -- Función de NEXO que llamó al modelo: ImportacionExcel, AsistenteCotizacion o ApoyoCubicador.
        [Funcion] nvarchar(40) NOT NULL,
        -- Quién la pidió. NULL cuando la llamada no la dispara una persona (por ejemplo un proceso interno).
        [UsuarioId] uniqueidentifier NULL,
        -- Identificador del modelo tal como lo devuelve el proveedor (por ejemplo claude-sonnet-5-5).
        [Modelo] nvarchar(80) NOT NULL,
        [TokensEntrada] int NOT NULL CONSTRAINT [DF_ia_uso_TokensEntrada] DEFAULT (0),
        [TokensSalida] int NOT NULL CONSTRAINT [DF_ia_uso_TokensSalida] DEFAULT (0),
        -- Tokens leídos del caché de prompt y tokens escritos en él (se facturan distinto que los de entrada normales).
        [TokensCacheLectura] int NOT NULL CONSTRAINT [DF_ia_uso_TokensCacheLectura] DEFAULT (0),
        [TokensCacheEscritura] int NOT NULL CONSTRAINT [DF_ia_uso_TokensCacheEscritura] DEFAULT (0),
        -- Costo estimado en dólares con el precio vigente cuando se hizo la llamada. Se guarda (y no se recalcula) para que un
        -- cambio de precios no reescriba el gasto histórico. NULL si no se pudo estimar.
        [CostoEstimadoUsd] decimal(12, 6) NULL,
        -- Tiempo total de la llamada, en milisegundos.
        [LatenciaMs] int NOT NULL CONSTRAINT [DF_ia_uso_LatenciaMs] DEFAULT (0),
        -- 'ok' o el código con el que terminó (IA_LIMITE_PROVEEDOR, IA_TIEMPO_AGOTADO, IA_RECHAZADA…).
        [Resultado] nvarchar(40) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ia_uso_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_ia_uso] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ia_uso_cifras_no_negativas] CHECK (
            [TokensEntrada] >= 0 AND [TokensSalida] >= 0 AND [TokensCacheLectura] >= 0 AND [TokensCacheEscritura] >= 0
            AND [LatenciaMs] >= 0 AND ([CostoEstimadoUsd] IS NULL OR [CostoEstimadoUsd] >= 0)),
        CONSTRAINT [FK_ia_uso_usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id])
    );
END
GO

-- Tope por usuario y por día: suma de lo consumido por una persona desde cierta fecha.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ia_uso_UsuarioId_CreatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]'))
    CREATE INDEX [IX_ia_uso_UsuarioId_CreatedAt] ON [db_NEXO].[tbl_ia_uso] ([UsuarioId], [CreatedAt])
        INCLUDE ([Funcion], [TokensEntrada], [TokensSalida], [CostoEstimadoUsd]);
GO

-- Gasto por función y por mes.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ia_uso_Funcion_CreatedAt' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]'))
    CREATE INDEX [IX_ia_uso_Funcion_CreatedAt] ON [db_NEXO].[tbl_ia_uso] ([Funcion], [CreatedAt])
        INCLUDE ([Modelo], [TokensEntrada], [TokensSalida], [CostoEstimadoUsd], [Resultado]);
GO

-- Verificación (solo lectura): debe devolver 1 tabla, 12 columnas, 2 índices propios y 1 llave foránea.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]') AND name LIKE N'IX_ia_uso_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_ia_uso]')) AS llaves_foraneas;
