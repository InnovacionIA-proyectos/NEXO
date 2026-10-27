-- =============================================================================
-- tbl_plantillas_mapeo_cliente — el mapeo de columnas que un cliente ya confirmó para sus archivos de Excel
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código actual sigue funcionando sin ella.
-- Sin esta tabla fallan los endpoints /api/captura-solicitudes/plantillas-mapeo y también sugerir-mapeo cuando recibe un clienteId
-- (busca ahí la plantilla del cliente); el resto de la API no se ve afectado. Por eso se aplica ANTES de desplegar.
--
-- Para qué sirve: cuando un cliente manda otro Excel con LOS MISMOS encabezados que uno anterior, NEXO usa el mapeo que el usuario ya
-- confirmó en vez de volver a llamar a la IA. Si un encabezado cambia, se agrega, se quita o se reordena una columna, la firma cambia y
-- el archivo se vuelve a analizar como uno nuevo.
--
-- Columnas:
--   FirmaEncabezados : SHA-256 en hexadecimal (64 caracteres) de los encabezados normalizados (sin tildes ni mayúsculas) y en orden.
--                      Con ClienteId es única: un cliente tiene como máximo una plantilla por conjunto de encabezados.
--   Encabezados      : los encabezados tal como llegaron, arreglo JSON de textos (solo para mostrarlos).
--   Columnas         : el mapeo, arreglo JSON de { indice, campo, transformacion }, una entrada por columna.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código nuevo. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_plantillas_mapeo_cliente];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_plantillas_mapeo_cliente] (
        [Id] uniqueidentifier NOT NULL,
        [ClienteId] uniqueidentifier NOT NULL,
        [Nombre] nvarchar(100) NULL,
        [FirmaEncabezados] nvarchar(64) NOT NULL,
        [Encabezados] nvarchar(max) NOT NULL,
        [Columnas] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_plantillas_mapeo_cliente_CreatedAt] DEFAULT (sysdatetimeoffset()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_plantillas_mapeo_cliente] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_plantillas_mapeo_cliente_Encabezados] CHECK (ISJSON([Encabezados]) = 1),
        CONSTRAINT [CK_plantillas_mapeo_cliente_Columnas] CHECK (ISJSON([Columnas]) = 1),
        CONSTRAINT [FK_plantillas_mapeo_cliente_clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [db_NEXO].[tbl_clientes] ([Id])
    );
END;

-- Un cliente tiene como máximo una plantilla por conjunto de encabezados (guardar otra vez la reemplaza).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_plantillas_mapeo_cliente_Firma' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]'))
    CREATE UNIQUE INDEX [IX_plantillas_mapeo_cliente_Firma]
        ON [db_NEXO].[tbl_plantillas_mapeo_cliente] ([ClienteId], [FirmaEncabezados]);

-- Verificación (solo lectura): debe devolver 1 tabla, 8 columnas, 1 índice propio, 1 llave foránea y 2 restricciones CHECK.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]') AND name LIKE N'IX_plantillas_mapeo_cliente_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]')) AS llaves_foraneas,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_plantillas_mapeo_cliente]')) AS restricciones_check;
