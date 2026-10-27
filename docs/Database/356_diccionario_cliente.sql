-- =============================================================================
-- tbl_diccionario_cliente — términos y conceptos que usa cada cliente en sus archivos
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código actual sigue funcionando sin ella.
-- Sin esta tabla solo fallan los endpoints /api/diccionario-cliente.
--
-- Para qué sirve: cada cliente manda sus Excel con sus propias palabras. Esta tabla guarda cómo se traduce cada palabra suya a
-- lo que entiende NEXO, para que la importación (y la IA que propone el mapeo de columnas) no tenga que adivinarlo cada vez.
-- Tres tipos de término (columna Tipo):
--   columna : un encabezado del Excel del cliente  -> el campo de NEXO al que corresponde.
--             Ej.: "CIUDAD ORIGEN" -> Concepto = origen.
--   unidad  : una unidad que usa el cliente         -> la unidad canónica (kg o t).
--             Ej.: "TON" -> Concepto = t.
--   valor   : un valor de una columna categórica     -> el valor de NEXO; Contexto dice de qué campo es.
--             Ej.: "TURBO (4,5 TON)" -> Contexto = tipoVehiculo, Concepto = 2_7.
-- ClienteId NULL significa "aplica a todos los clientes" (por ejemplo "KG" -> peso).
--
-- RELACIÓN CON tbl_alias_elementos. Son complementarias, no se duplican:
--   * tbl_alias_elementos es el diccionario GLOBAL de sinónimos de vehículos y clases de contenedor (T.MULA, TB, 40HC) que
--     usa la captura para dejar el código oficial. Un alias es único en toda la tabla.
--   * tbl_diccionario_cliente guarda lo que es propio de UN cliente (y las columnas y unidades de sus archivos). Un término de
--     valor de un cliente puede apuntar a un valor que el catálogo oficial no tiene (el Turbo de BASF es 2_7, que no está en
--     tipos-vehiculo), por eso Concepto no se valida contra el catálogo.
--
-- ORDEN: aplicar ESTE script antes de desplegar el código nuevo. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_diccionario_cliente];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_diccionario_cliente] (
        [Id] uniqueidentifier NOT NULL,
        -- NULL = el término aplica a todos los clientes.
        [ClienteId] uniqueidentifier NULL,
        -- El término tal como lo escribe el cliente (limpio de espacios) y su forma normalizada (sin tildes ni mayúsculas, con
        -- "_" y "-" como espacio): es la que se compara y la que no se puede repetir.
        [Termino] nvarchar(200) NOT NULL,
        [TerminoNormalizado] nvarchar(200) NOT NULL,
        [Tipo] nvarchar(20) NOT NULL,
        -- columna: campo de NEXO · unidad: kg o t · valor: valor de NEXO.
        [Concepto] nvarchar(100) NOT NULL,
        -- Solo para Tipo = 'valor': el campo al que pertenece el valor (tipoVehiculo, carroceria, claseContenedor…).
        [Contexto] nvarchar(50) NULL,
        [Ejemplo] nvarchar(500) NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_diccionario_cliente_Activo] DEFAULT (1),
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_diccionario_cliente_CreatedAt] DEFAULT (sysdatetimeoffset()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_diccionario_cliente] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_diccionario_cliente_Tipo] CHECK ([Tipo] IN (N'columna', N'unidad', N'valor')),
        CONSTRAINT [CK_diccionario_cliente_Contexto] CHECK (
            ([Tipo] = N'valor' AND [Contexto] IS NOT NULL) OR ([Tipo] <> N'valor' AND [Contexto] IS NULL)),
        CONSTRAINT [FK_diccionario_cliente_clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [db_NEXO].[tbl_clientes] ([Id])
    );
END;

-- Un término no se repite para el mismo cliente, tipo y contexto (SQL Server trata los NULL como iguales en un índice único,
-- así que tampoco se repite un término general).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_diccionario_cliente_Unico' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]'))
    CREATE UNIQUE INDEX [IX_diccionario_cliente_Unico]
        ON [db_NEXO].[tbl_diccionario_cliente] ([ClienteId], [TerminoNormalizado], [Tipo], [Contexto]);

-- Los términos de un cliente.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_diccionario_cliente_ClienteId_Tipo' AND object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]'))
    CREATE INDEX [IX_diccionario_cliente_ClienteId_Tipo] ON [db_NEXO].[tbl_diccionario_cliente] ([ClienteId], [Tipo]);

-- Verificación (solo lectura): debe devolver 1 tabla, 11 columnas, 2 índices propios, 1 llave foránea y 2 restricciones CHECK.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]') AND name LIKE N'IX_diccionario_cliente_%') AS indices,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]')) AS llaves_foraneas,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[db_NEXO].[tbl_diccionario_cliente]')) AS restricciones_check;
