-- =============================================================================
-- #304 — rechazo del Cubicador por línea (tbl_rechazos_cubicacion_linea)
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente, así que el código anterior sigue funcionando
-- sin ella. Sin esta tabla el código NUEVO falla solo en dos puntos: el consumer del rechazo y
-- GET /api/planes-cargue/por-linea/{lineaId}/estado.
--
-- ORDEN: aplicar ESTE script ANTES de desplegar NEXO. Es idempotente (se puede correr dos veces).
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [db_NEXO].[tbl_rechazos_cubicacion_linea];
-- =============================================================================

IF OBJECT_ID(N'[db_NEXO].[tbl_rechazos_cubicacion_linea]', N'U') IS NULL
BEGIN
    CREATE TABLE [db_NEXO].[tbl_rechazos_cubicacion_linea] (
        [LineaSolicitudId] uniqueidentifier NOT NULL,
        [Codigo] nvarchar(60) NOT NULL,
        [Mensaje] nvarchar(1000) NOT NULL,
        [RechazadoAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_rechazos_cubicacion_linea] PRIMARY KEY ([LineaSolicitudId]),
        CONSTRAINT [FK_rechazos_cubicacion_linea_lineas_solicitud_LineaSolicitudId]
            FOREIGN KEY ([LineaSolicitudId]) REFERENCES [db_NEXO].[tbl_lineas_solicitud] ([Id]) ON DELETE CASCADE
    );
END
