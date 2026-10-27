-- =============================================================================
-- tbl_plantillas_mapeo_cliente — mapeo de columnas ya confirmado para un formato de Excel de un cliente
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código actual sigue funcionando sin ella.
--
-- Para qué sirve: cuando un comercial confirma cómo se lee un Excel de un cliente (qué columna es origen, destino, peso,
-- unidad…), ese mapeo se guarda aquí. Si el mismo cliente vuelve a mandar un Excel con los mismos encabezados, el backend
-- aplica la plantilla guardada y no llama a la IA. Si los encabezados cambian, se vuelve a sugerir el mapeo.
--   FirmaEncabezados : SHA-256 (64 caracteres hex) de los encabezados normalizados y en orden. Es lo que se busca.
--   Encabezados      : JSON con los encabezados tal como venían, para mostrar y auditar.
--   Columnas         : JSON con el mapeo confirmado de cada columna al campo de NEXO, con la forma { "version": 1, "columnas": [{ indice, campo, transformacion }] };
--                      la versión permite cambiar la forma sin leer mal las plantillas ya guardadas.
--   Nombre           : etiqueta opcional para reconocer la plantilla (ej. "Formato licitación 2026").
-- Una sola plantilla por cliente y firma (índice único).
--
-- RELACIÓN CON tbl_diccionario_cliente. Son complementarias:
--   * tbl_diccionario_cliente es el vocabulario del cliente, término por término. Sirve de pista a la IA cuando llega un
--     formato que no tiene plantilla, y sigue sirviendo aunque el cliente cambie de formato.
--   * tbl_plantillas_mapeo_cliente es la foto completa de UN formato ya confirmado. Si la firma coincide, manda la plantilla
--     y no se consulta el diccionario ni la IA.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código que use las plantillas. Es idempotente (se puede correr dos veces).
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

-- Búsqueda de la plantilla de un cliente por la firma de sus encabezados; también impide duplicarla.
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
