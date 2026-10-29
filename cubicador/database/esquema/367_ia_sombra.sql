-- =============================================================================
-- dbo_cubicador.iaSombra — diferencias entre el motor y el modelo de lenguaje en modo sombra
-- =============================================================================
-- Qué es: una tabla NUEVA y ADITIVA. No toca ninguna tabla existente: el servicio actual sigue funcionando sin ella
-- mientras el modo sombra esté apagado (IA_SOMBRA_HABILITADA distinto de true). Con el modo sombra encendido hace falta
-- que exista; si no, cada comparación falla al registrarse (se ve en el log) pero la API responde igual.
--
-- Una fila por recomendación comparada, o por intento que no se pudo comparar (Estado trae el código del problema:
-- IA_TIEMPO_AGOTADO, IA_LIMITE_PROVEEDOR, IA_RESPUESTA_INVALIDA…). Solo se agrega, nunca se actualiza.
-- NO guarda lo que se envía al modelo (medidas y pesos de la carga): IdRecomendacion une contra la recomendación del motor.
--
-- Columnas:
--   Estado           : OK, o el código por el que no se pudo comparar.
--   FlotaMotor/FlotaIa: arreglo JSON de "vehículo/carrocería" ordenado, p. ej. ["2S2/FURGON","C2/FURGON"].
--   Coincide*        : acuerdo con el motor en el resultado, en la flota exacta y en los vehículos sin mirar la carrocería.
--   IaRespeta*       : verificación recalculada con el catálogo (peso, volumen y unidades), no lo que el modelo dijo.
--   Tokens*          : los del proveedor; TokensEntrada incluye los de caché. Con ellos se calcula el costo y el tope diario.
--
-- Orden: aplicar ESTE script antes de encender el modo sombra. Idempotente (se puede correr dos veces).
-- Reemplazar dbo_cubicador por el valor de CUBICADOR_DB_ESQUEMA si es otro.
--
-- Deshacer (solo si hiciera falta): DROP TABLE [dbo_cubicador].[iaSombra];
-- =============================================================================

IF OBJECT_ID(N'[dbo_cubicador].[iaSombra]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo_cubicador].[iaSombra] (
        [Id] nvarchar(36) NOT NULL,
        [IdRecomendacion] nvarchar(36) NOT NULL,
        [IdLineaSolicitud] nvarchar(100) NOT NULL,
        [OrigenSistema] nvarchar(20) NOT NULL,
        [Estado] nvarchar(40) NOT NULL,
        [Modelo] nvarchar(100) NULL,
        [Esfuerzo] nvarchar(10) NULL,
        [ResultadoMotor] nvarchar(40) NOT NULL,
        [ResultadoIa] nvarchar(40) NULL,
        [MotivoMotor] nvarchar(40) NULL,
        [MotivoIa] nvarchar(40) NULL,
        [CantidadMotor] int NOT NULL,
        [CantidadIa] int NULL,
        [FlotaMotor] nvarchar(max) NOT NULL,
        [FlotaIa] nvarchar(max) NULL,
        [CoincideResultado] bit NULL,
        [CoincideFlota] bit NULL,
        [CoincideConfiguracion] bit NULL,
        [IaRespetaPeso] bit NULL,
        [IaRespetaVolumen] bit NULL,
        [IaConservaUnidades] bit NULL,
        [Violaciones] nvarchar(max) NULL,
        [Explicacion] nvarchar(500) NULL,
        [TokensEntrada] int NOT NULL CONSTRAINT [DF_iaSombra_TokensEntrada] DEFAULT (0),
        [TokensSalida] int NOT NULL CONSTRAINT [DF_iaSombra_TokensSalida] DEFAULT (0),
        [LatenciaIaMs] int NULL,
        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_iaSombra_CreatedAt] DEFAULT (sysdatetimeoffset()),
        CONSTRAINT [PK_iaSombra] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_iaSombra_OrigenSistema] CHECK ([OrigenSistema] IN (N'NEXO', N'COTIFLY')),
        CONSTRAINT [CK_iaSombra_FlotaMotor] CHECK (ISJSON([FlotaMotor]) = 1),
        CONSTRAINT [CK_iaSombra_FlotaIa] CHECK ([FlotaIa] IS NULL OR ISJSON([FlotaIa]) = 1),
        CONSTRAINT [CK_iaSombra_Violaciones] CHECK ([Violaciones] IS NULL OR ISJSON([Violaciones]) = 1)
    );
END;

-- Buscar lo comparado de una recomendación, y sumar los tokens del día para el tope.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_iaSombra_IdRecomendacion' AND object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]'))
    CREATE INDEX [IX_iaSombra_IdRecomendacion] ON [dbo_cubicador].[iaSombra] ([IdRecomendacion]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_iaSombra_CreatedAt' AND object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]'))
    CREATE INDEX [IX_iaSombra_CreatedAt] ON [dbo_cubicador].[iaSombra] ([CreatedAt]) INCLUDE ([TokensEntrada], [TokensSalida]);

-- Verificación (solo lectura): debe devolver 1 tabla, 27 columnas, 2 índices propios y 4 restricciones CHECK.
SELECT
    (SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]')) AS tablas,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]')) AS columnas,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]') AND name LIKE N'IX_iaSombra_%') AS indices,
    (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo_cubicador].[iaSombra]')) AS restricciones_check;
