-- =============================================================================
-- Esquema del motor de selección de vehículo del Cubicador — SQL Server 2019
-- =============================================================================
-- Base de datos propia y exclusiva del Cubicador (no comparte base con NEXO
-- ni con Cotifly) — por eso no hace falta un esquema con nombre propio tipo
-- "db_NEXO", todo vive en "dbo" de la base "cubicador".
--
-- Este archivo ES la fuente de la verdad de este esquema nuevo — mismo
-- criterio que `docs/Database/ESQUEMA_NEXO.sql` en NEXO: no hay migraciones
-- (ni EF Core ni Alembic), se corre este script completo, de una sola
-- pasada, contra una base vacía. Cualquier cambio de esquema se hace
-- editando directamente este archivo y aplicándolo a mano.
--
-- Corresponde a `cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`
-- (las 9 tablas, ya cerradas y revisadas). Los datos iniciales viven en
-- `cubicador/database/seeder/*.csv`, cargados por `ejecutar_schema_y_seeders.py`.
-- =============================================================================

IF DB_ID(N'cubicador') IS NULL
BEGIN
    CREATE DATABASE [cubicador];
END
GO

USE [cubicador];
GO

-- --- Tabla 1: vehiculos -----------------------------------------------------
-- Configuraciones de vehículo (Turbo, Sencillo, Dobletroque, Minimula,
-- Tractomula...). CodigoVehiculo es la clave estable para matching contra
-- SICE-TAC/tarifario — debe quedar única (ver Tabla 4 para el caso
-- Tractomula/Tractomula Furgón, que NO duplica este código).
CREATE TABLE [dbo].[vehiculos] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [CodigoVehiculo] nvarchar(30) NOT NULL,
    [NombreVehiculo] nvarchar(120) NOT NULL,
    [LargoPlancha] decimal(9,2) NOT NULL,
    [AnchoPlancha] decimal(9,2) NOT NULL,
    [AltoCarroceria] decimal(9,2) NOT NULL DEFAULT (4.4), -- siempre se valida, para toda carrocería
    [CapacidadMaxima] decimal(18,2) NOT NULL,
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_vehiculos] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_vehiculos_CodigoVehiculo] UNIQUE ([CodigoVehiculo])
);
GO

-- --- Tabla 2: TiposCarroceria ------------------------------------------------
-- Catálogo cerrado de carrocerías (FURGON, ESTACAS, PLATAFORMA, PLANCHON,
-- FURGON_REFRIGERADO, FURGON_CLIMATIZADO, FURGON_VENTILADO, PORTAVIDRIOS,
-- CONTENEDOR_SECO, CONTENEDOR_REFRIGERADO) — ver seeder/tiposCarroceria.csv.
CREATE TABLE [dbo].[TiposCarroceria] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [TipoCarroceria] nvarchar(50) NOT NULL,
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_TiposCarroceria] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_TiposCarroceria_TipoCarroceria] UNIQUE ([TipoCarroceria])
);
GO

-- --- Tabla 5: categoriaMercancia ---------------------------------------------
-- Antes que tiposMercancia porque esta es su FK. Catálogo plano de
-- categorías (GENERAL, ALIMENTOS, FARMACEUTICO, COMBUSTIBLE, etc.).
CREATE TABLE [dbo].[categoriaMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [NombreCategoria] nvarchar(100) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_categoriaMercancia] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_categoriaMercancia_NombreCategoria] UNIQUE ([NombreCategoria])
);
GO

-- --- Tabla 3: tiposMercancia --------------------------------------------------
-- Las 31 mercancías de la Matriz cubicador, con su categoría y carrocerías
-- admitidas. Cubicable y CondicionadaCubicacion son independientes (una
-- mercancía puede ser cubicable Y requerir validación obligatoria de
-- AdminTarifas a la vez).
CREATE TABLE [dbo].[tiposMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [NombreMercancia] nvarchar(200) NOT NULL,
    [IdCategoria] uniqueidentifier NOT NULL,
    [Cubicable] bit NOT NULL DEFAULT (1),
    [CondicionadaCubicacion] bit NOT NULL DEFAULT (0),
    [ComentarioCondicion] nvarchar(500) NULL,
    [IdCarroceriaPrincipal] uniqueidentifier NOT NULL,
    [IdCarroceriaSecundaria] uniqueidentifier NULL,
    [CarroceriaSecundariaCondicionada] bit NOT NULL DEFAULT (0),
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_tiposMercancia] PRIMARY KEY ([Id])
    , CONSTRAINT [CK_tiposMercancia_CarroceriasDistintas]
        CHECK ([IdCarroceriaSecundaria] IS NULL OR [IdCarroceriaSecundaria] <> [IdCarroceriaPrincipal])
    , CONSTRAINT [CK_tiposMercancia_SecundariaCondicionadaRequiereSecundaria]
        CHECK ([IdCarroceriaSecundaria] IS NOT NULL OR [CarroceriaSecundariaCondicionada] = 0)
);
GO

-- --- Tabla 4: relacionVehiculosCarroceria -------------------------------------
-- N:N entre vehículo y carrocería. UsaCapacidadMaximaDelVehiculo=false +
-- CapacidadMaximaEspecifica resuelve el caso Tractomula/Tractomula Furgón
-- 3S3 sin duplicar el código del vehículo.
CREATE TABLE [dbo].[relacionVehiculosCarroceria] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdTipoVehiculo] uniqueidentifier NOT NULL,
    [IdCarroceria] uniqueidentifier NOT NULL,
    [UsaCapacidadMaximaDelVehiculo] bit NOT NULL DEFAULT (1),
    [CapacidadMaximaEspecifica] decimal(18,2) NOT NULL DEFAULT (0), -- solo se lee si UsaCapacidadMaximaDelVehiculo = 0
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_relacionVehiculosCarroceria] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_relacionVehiculosCarroceria_Par] UNIQUE ([IdTipoVehiculo], [IdCarroceria])
);
GO

-- --- Tabla 6: reglasIncompatibilidad ------------------------------------------
-- Pares de categoría incompatibles entre sí. Cada par se guarda UNA sola
-- vez (orden canónico lo decide la aplicación antes de insertar) — hoy
-- vacía a propósito, no se inventaron reglas sin respaldo real.
CREATE TABLE [dbo].[reglasIncompatibilidad] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdCategoria1] uniqueidentifier NOT NULL,
    [IdCategoria2] uniqueidentifier NOT NULL,
    [Motivo] nvarchar(300) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_reglasIncompatibilidad] PRIMARY KEY ([Id])
    , CONSTRAINT [CK_reglasIncompatibilidad_CategoriasDistintas] CHECK ([IdCategoria1] <> [IdCategoria2])
    , CONSTRAINT [UQ_reglasIncompatibilidad_Par] UNIQUE ([IdCategoria1], [IdCategoria2])
);
GO

-- --- Tabla 9: parametrosMotor --------------------------------------------------
-- Configuración del motor (k_max, factor_vol_util, etc.) — en tabla, no
-- hardcodeada en código (propuesta madre §4.5/§8.2).
CREATE TABLE [dbo].[parametrosMotor] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [Clave] nvarchar(50) NOT NULL,
    [Valor] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(300) NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_parametrosMotor] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_parametrosMotor_Clave] UNIQUE ([Clave])
);
GO

-- --- Tabla 7: recomendacionVehiculo --------------------------------------------
-- Un vehículo asignado = una fila. IdTipoVehiculo/IdCarroceria/PorcentajeLlenado
-- quedan nullable: una línea puede terminar en SIN_SELECCION_AUTOMATICA, sin
-- ningún vehículo asignado.
CREATE TABLE [dbo].[recomendacionVehiculo] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdLineaSolicitud] nvarchar(100) NOT NULL,
    [OrigenSistema] nvarchar(20) NOT NULL,
    [IdTipoVehiculo] uniqueidentifier NULL,
    [IdCarroceria] uniqueidentifier NULL,
    [Heterogenea] bit NOT NULL DEFAULT (0),
    [PorcentajeLlenado] decimal(5,2) NULL,
    [Resultado] nvarchar(30) NOT NULL,
    [MotivoSinSeleccion] nvarchar(500) NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_recomendacionVehiculo] PRIMARY KEY ([Id])
    , CONSTRAINT [CK_recomendacionVehiculo_OrigenSistema] CHECK ([OrigenSistema] IN (N'NEXO', N'COTIFLY'))
    , CONSTRAINT [CK_recomendacionVehiculo_Resultado] CHECK ([Resultado] IN (N'ASIGNADO', N'SIN_SELECCION_AUTOMATICA'))
);
GO

-- --- Tabla 8: recomendacionVehiculoMercancia ------------------------------------
-- Una mercancía dentro de un vehículo asignado = una fila. Varias filas con
-- el mismo IdRecomendacionVehiculo = carga heterogénea en ese camión.
CREATE TABLE [dbo].[recomendacionVehiculoMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdRecomendacionVehiculo] uniqueidentifier NOT NULL,
    [IdMercancia] uniqueidentifier NOT NULL,
    [CantidadUnidadesMercancia] int NOT NULL,
    [PesoMercanciaKg] decimal(18,2) NOT NULL,
    [VolumenMercanciaM3] decimal(18,4) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_recomendacionVehiculoMercancia] PRIMARY KEY ([Id])
);
GO

-- --- Llaves foráneas (después de crear las 9 tablas, en bloque) ----------------

ALTER TABLE [dbo].[tiposMercancia] ADD CONSTRAINT [FK_tiposMercancia_categoriaMercancia_IdCategoria]
    FOREIGN KEY ([IdCategoria]) REFERENCES [dbo].[categoriaMercancia] ([Id]);
ALTER TABLE [dbo].[tiposMercancia] ADD CONSTRAINT [FK_tiposMercancia_TiposCarroceria_IdCarroceriaPrincipal]
    FOREIGN KEY ([IdCarroceriaPrincipal]) REFERENCES [dbo].[TiposCarroceria] ([Id]);
ALTER TABLE [dbo].[tiposMercancia] ADD CONSTRAINT [FK_tiposMercancia_TiposCarroceria_IdCarroceriaSecundaria]
    FOREIGN KEY ([IdCarroceriaSecundaria]) REFERENCES [dbo].[TiposCarroceria] ([Id]);

ALTER TABLE [dbo].[relacionVehiculosCarroceria] ADD CONSTRAINT [FK_relacionVehiculosCarroceria_vehiculos_IdTipoVehiculo]
    FOREIGN KEY ([IdTipoVehiculo]) REFERENCES [dbo].[vehiculos] ([Id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[relacionVehiculosCarroceria] ADD CONSTRAINT [FK_relacionVehiculosCarroceria_TiposCarroceria_IdCarroceria]
    FOREIGN KEY ([IdCarroceria]) REFERENCES [dbo].[TiposCarroceria] ([Id]);

ALTER TABLE [dbo].[reglasIncompatibilidad] ADD CONSTRAINT [FK_reglasIncompatibilidad_categoriaMercancia_IdCategoria1]
    FOREIGN KEY ([IdCategoria1]) REFERENCES [dbo].[categoriaMercancia] ([Id]);
ALTER TABLE [dbo].[reglasIncompatibilidad] ADD CONSTRAINT [FK_reglasIncompatibilidad_categoriaMercancia_IdCategoria2]
    FOREIGN KEY ([IdCategoria2]) REFERENCES [dbo].[categoriaMercancia] ([Id]);

ALTER TABLE [dbo].[recomendacionVehiculo] ADD CONSTRAINT [FK_recomendacionVehiculo_vehiculos_IdTipoVehiculo]
    FOREIGN KEY ([IdTipoVehiculo]) REFERENCES [dbo].[vehiculos] ([Id]);
ALTER TABLE [dbo].[recomendacionVehiculo] ADD CONSTRAINT [FK_recomendacionVehiculo_TiposCarroceria_IdCarroceria]
    FOREIGN KEY ([IdCarroceria]) REFERENCES [dbo].[TiposCarroceria] ([Id]);

ALTER TABLE [dbo].[recomendacionVehiculoMercancia] ADD CONSTRAINT [FK_recomendacionVehiculoMercancia_recomendacionVehiculo_IdRecomendacionVehiculo]
    FOREIGN KEY ([IdRecomendacionVehiculo]) REFERENCES [dbo].[recomendacionVehiculo] ([Id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[recomendacionVehiculoMercancia] ADD CONSTRAINT [FK_recomendacionVehiculoMercancia_tiposMercancia_IdMercancia]
    FOREIGN KEY ([IdMercancia]) REFERENCES [dbo].[tiposMercancia] ([Id]);
GO

-- --- Índices sobre FK de consulta frecuente ------------------------------------

CREATE INDEX [IX_tiposMercancia_IdCategoria] ON [dbo].[tiposMercancia] ([IdCategoria]);
CREATE INDEX [IX_relacionVehiculosCarroceria_IdCarroceria] ON [dbo].[relacionVehiculosCarroceria] ([IdCarroceria]);
CREATE INDEX [IX_recomendacionVehiculo_IdLineaSolicitud_OrigenSistema] ON [dbo].[recomendacionVehiculo] ([IdLineaSolicitud], [OrigenSistema]);
CREATE INDEX [IX_recomendacionVehiculoMercancia_IdRecomendacionVehiculo] ON [dbo].[recomendacionVehiculoMercancia] ([IdRecomendacionVehiculo]);
GO
