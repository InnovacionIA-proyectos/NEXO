-- =============================================================================
-- Esquema de base de datos del Cubicador (SQL Server 2019)
-- =============================================================================
-- Base: dbCubicador_Pruebas   Esquema: dbo   Servidor: 10.10.40.85,1433
--
-- Este archivo es la única definición del esquema. No hay migraciones: se ejecuta
-- completo, una sola vez, contra una base vacía. Un cambio de estructura se hace
-- editando este archivo y aplicando a mano el ALTER equivalente en cada base que
-- ya exista.
--
-- Lo ejecuta una persona con permisos para crear tablas, no la aplicación:
--   py scripts/ejecutar_schema_y_seeders.py --server 10.10.40.85,1433
-- (crea las tablas y carga el catálogo inicial; ver ../README.md).
--
-- Contenido (16 tablas):
--   Parte 1. Catálogo: vehiculos, TiposCarroceria, categoriaMercancia, tiposMercancia,
--            relacionVehiculosCarroceria, reglasIncompatibilidad, parametrosMotor.
--            Recomendación: recomendaciones, recomendacionVehiculo,
--            recomendacionVehiculoMercancia, iaSombra.
--   Parte 2. Motor 3D: parametros_cubicacion, vehiculos_cubicacion, planes_cargue,
--            items_plan_cargue, colocaciones_cargue.
--
-- Convención de columnas: Id primero, después las de negocio y al final la
-- auditoría (CreadoPor, CreatedAt, UpdatedAt, Version).
-- =============================================================================

-- =============================================================================
-- Parte 1: catálogo y recomendación
-- =============================================================================
-- Los catálogos se editan desde la aplicación. La columna Version es el control de
-- concurrencia optimista de esas ediciones: la aplicación la incrementa en cada
-- cambio y rechaza un guardado hecho sobre una versión vieja.

-- --- vehiculos -----------------------------------------------------------------
-- Configuraciones de vehículo (Turbo 2L2, Sencillo C2, Dobletroque C3, Minimula
-- 2S2, Tractomula 3S3). Medidas en centímetros. CodigoVehiculo es único porque se
-- usa para cruzar con SICE-TAC y el tarifario; si un vehículo carga distinto según
-- la carrocería, eso se define en relacionVehiculosCarroceria.
-- AlturaMaximaReglamentariaCm es el límite legal de altura del tipo de vehículo; el
-- valor por defecto (440 cm) solo aplica si se crea un vehículo sin ese dato.
CREATE TABLE [dbo].[vehiculos] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [CodigoVehiculo] nvarchar(30) NOT NULL,
    [NombreVehiculo] nvarchar(120) NOT NULL,
    [LargoCm] decimal(9,2) NOT NULL,
    [AnchoCm] decimal(9,2) NOT NULL,
    [AlturaMaximaReglamentariaCm] decimal(9,2) NOT NULL DEFAULT (440),
    [CapacidadMaxima] decimal(18,2) NOT NULL,
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_vehiculos_Version] DEFAULT (1)
    , CONSTRAINT [PK_vehiculos] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_vehiculos_CodigoVehiculo] UNIQUE ([CodigoVehiculo])
);
GO

-- --- TiposCarroceria ------------------------------------------------------------
-- Catálogo de carrocerías: código fijo (el que usan las demás tablas) y nombre
-- legible.
CREATE TABLE [dbo].[TiposCarroceria] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [CodigoCarroceria] nvarchar(50) NOT NULL,
    [NombreCarroceria] nvarchar(150) NOT NULL,
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_TiposCarroceria_Version] DEFAULT (1)
    , CONSTRAINT [PK_TiposCarroceria] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_TiposCarroceria_CodigoCarroceria] UNIQUE ([CodigoCarroceria])
);
GO

-- --- categoriaMercancia ---------------------------------------------------------
-- Categorías de mercancía (GENERAL, ALIMENTOS, FARMACEUTICO...). Es el nivel al que
-- se definen las incompatibilidades.
CREATE TABLE [dbo].[categoriaMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [NombreCategoria] nvarchar(100) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_categoriaMercancia_Version] DEFAULT (1)
    , CONSTRAINT [PK_categoriaMercancia] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_categoriaMercancia_NombreCategoria] UNIQUE ([NombreCategoria])
);
GO

-- --- tiposMercancia --------------------------------------------------------------
-- Mercancías que se pueden transportar, con su categoría y sus carrocerías
-- (principal y alternativa). Cubicable indica si el motor la procesa;
-- CondicionadaCubicacion, si además requiere validación manual. Son independientes.
-- IdCarroceriaPrincipal queda en NULL para mercancías no cubicables.
CREATE TABLE [dbo].[tiposMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [CodigoMercancia] nvarchar(50) NOT NULL,
    [NombreMercancia] nvarchar(200) NOT NULL,
    [IdCategoria] uniqueidentifier NOT NULL,
    [Cubicable] bit NOT NULL DEFAULT (1),
    [CondicionadaCubicacion] bit NOT NULL DEFAULT (0),
    [ComentarioCondicion] nvarchar(500) NULL,
    [IdCarroceriaPrincipal] uniqueidentifier NULL,
    [IdCarroceriaSecundaria] uniqueidentifier NULL,
    [CarroceriaSecundariaCondicionada] bit NOT NULL DEFAULT (0),
    [ControlAmbientalRequerido] nvarchar(40) NOT NULL DEFAULT (N'NINGUNO'),
    [Apilable] bit NOT NULL DEFAULT (1),
    [Fragil] bit NOT NULL DEFAULT (0),
    [RotacionLibre] bit NOT NULL DEFAULT (1),
    [Activo] bit NOT NULL DEFAULT (1),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_tiposMercancia_Version] DEFAULT (1)
    , CONSTRAINT [PK_tiposMercancia] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_tiposMercancia_CodigoMercancia] UNIQUE ([CodigoMercancia])
    , CONSTRAINT [CK_tiposMercancia_CarroceriasDistintas]
        CHECK ([IdCarroceriaSecundaria] IS NULL OR [IdCarroceriaSecundaria] <> [IdCarroceriaPrincipal])
    , CONSTRAINT [CK_tiposMercancia_SecundariaCondicionadaRequiereSecundaria]
        CHECK ([IdCarroceriaSecundaria] IS NOT NULL OR [CarroceriaSecundariaCondicionada] = 0)
);
GO

-- --- relacionVehiculosCarroceria --------------------------------------------------
-- Qué carrocerías acepta cada vehículo. Si la capacidad con esa carrocería es
-- distinta a la del vehículo, UsaCapacidadMaximaDelVehiculo = 0 y la capacidad va en
-- CapacidadMaximaEspecifica (por ejemplo, 3S3 con Furgón: 30.000 kg en vez de 34.000).
-- No tiene Version: se edita junto con su vehículo, que es el que lleva la versión.
CREATE TABLE [dbo].[relacionVehiculosCarroceria] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdTipoVehiculo] uniqueidentifier NOT NULL,
    [IdCarroceria] uniqueidentifier NOT NULL,
    [UsaCapacidadMaximaDelVehiculo] bit NOT NULL DEFAULT (1),
    [CapacidadMaximaEspecifica] decimal(18,2) NOT NULL DEFAULT (0),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_relacionVehiculosCarroceria] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_relacionVehiculosCarroceria_Par] UNIQUE ([IdTipoVehiculo], [IdCarroceria])
    , CONSTRAINT [CK_relacionVehiculosCarroceria_CapacidadEspecifica]
        CHECK ([UsaCapacidadMaximaDelVehiculo] = 1 OR [CapacidadMaximaEspecifica] > 0)
);
GO

-- --- reglasIncompatibilidad -------------------------------------------------------
-- Pares de categorías que no pueden viajar en el mismo vehículo. Cada par se guarda
-- una sola vez, con el Id menor primero comparado como texto; así el UNIQUE impide la
-- misma regla en los dos sentidos.
CREATE TABLE [dbo].[reglasIncompatibilidad] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdCategoria1] uniqueidentifier NOT NULL,
    [IdCategoria2] uniqueidentifier NOT NULL,
    [Motivo] nvarchar(300) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_reglasIncompatibilidad_Version] DEFAULT (1)
    , CONSTRAINT [PK_reglasIncompatibilidad] PRIMARY KEY ([Id])
    , CONSTRAINT [CK_reglasIncompatibilidad_CategoriasDistintas] CHECK ([IdCategoria1] <> [IdCategoria2])
    , CONSTRAINT [UQ_reglasIncompatibilidad_Par] UNIQUE ([IdCategoria1], [IdCategoria2])
);
GO

-- --- parametrosMotor --------------------------------------------------------------
-- Parámetros del motor de recomendación (k_max, factor_vol_util,
-- umbral_alerta_llenado...). Están en tabla para ajustarlos sin desplegar.
CREATE TABLE [dbo].[parametrosMotor] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [Clave] nvarchar(50) NOT NULL,
    [Valor] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(300) NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [Version] int NOT NULL CONSTRAINT [DF_parametrosMotor_Version] DEFAULT (1)
    , CONSTRAINT [PK_parametrosMotor] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_parametrosMotor_Clave] UNIQUE ([Clave])
);
GO

-- --- recomendaciones --------------------------------------------------------------
-- Una fila por cada vez que el motor recomienda vehículos para una línea de
-- solicitud de NEXO o de Cotifly (OrigenSistema). Los vehículos recomendados van en
-- recomendacionVehiculo. Las tablas de recomendación solo se agregan, no se editan:
-- una recomendación consultada después debe decir lo mismo que el día que se hizo.
CREATE TABLE [dbo].[recomendaciones] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_recomendaciones_Id] DEFAULT (newid()),
    [IdLineaSolicitud] nvarchar(100) NOT NULL,
    [OrigenSistema] nvarchar(20) NOT NULL,
    [Resultado] nvarchar(30) NOT NULL,
    [MotivoSinSeleccion] nvarchar(500) NOT NULL CONSTRAINT [DF_recomendaciones_MotivoSinSeleccion] DEFAULT (N'NINGUNO'),
    [Condicionada] bit NOT NULL CONSTRAINT [DF_recomendaciones_Condicionada] DEFAULT ((0)),
    [AdvertenciasJson] nvarchar(max) NOT NULL CONSTRAINT [DF_recomendaciones_AdvertenciasJson] DEFAULT (N'[]'),
    [ExtradimensionJson] nvarchar(max) NOT NULL CONSTRAINT [DF_recomendaciones_ExtradimensionJson] DEFAULT (N'{}'),
    [MercanciasCondicionadasJson] nvarchar(max) NOT NULL CONSTRAINT [DF_recomendaciones_MercanciasCondicionadasJson] DEFAULT (N'[]'),
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_recomendaciones_CreatedAt] DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_recomendaciones_UpdatedAt] DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_recomendaciones] PRIMARY KEY ([Id])
);
GO

-- --- recomendacionVehiculo ----------------------------------------------------------
-- Cada vehículo de una recomendación (NumeroVehiculo: 1, 2, 3...). Vehículo,
-- carrocería y PorcentajeLlenado quedan en NULL cuando el resultado es
-- SIN_SELECCION_AUTOMATICA. AlertaBajoLlenado se guarda calculada, porque el umbral
-- que la define se puede cambiar después.
CREATE TABLE [dbo].[recomendacionVehiculo] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdRecomendacion] uniqueidentifier NOT NULL,
    [IdLineaSolicitud] nvarchar(100) NOT NULL,
    [OrigenSistema] nvarchar(20) NOT NULL,
    [NumeroVehiculo] int NOT NULL,
    [IdTipoVehiculo] uniqueidentifier NULL,
    [IdCarroceria] uniqueidentifier NULL,
    [Heterogenea] bit NOT NULL DEFAULT (0),
    [PesoTotalKg] decimal(18,2) NOT NULL,
    [VolumenTotalM3] decimal(18,3) NOT NULL,
    [PorcentajeLlenado] decimal(5,2) NULL,
    [PorcentajeLlenadoVolumen] decimal(5,2) NOT NULL,
    [PorcentajeLlenadoPeso] decimal(5,2) NOT NULL,
    [AlertaBajoLlenado] bit NOT NULL CONSTRAINT [DF_recomendacionVehiculo_AlertaBajoLlenado] DEFAULT (0),
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

-- --- recomendacionVehiculoMercancia -------------------------------------------------
-- Qué mercancía y cuánto lleva cada vehículo recomendado. Varias filas con el mismo
-- IdRecomendacionVehiculo = carga mixta en ese vehículo. IndicePosicion es la
-- posición de la mercancía en la solicitud (NULL en recomendaciones antiguas).
CREATE TABLE [dbo].[recomendacionVehiculoMercancia] (
    [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
    [IdRecomendacionVehiculo] uniqueidentifier NOT NULL,
    [IdMercancia] uniqueidentifier NOT NULL,
    [IndicePosicion] int NULL,
    [CantidadUnidadesMercancia] int NOT NULL,
    [PesoMercanciaKg] decimal(18,2) NOT NULL,
    [VolumenMercanciaM3] decimal(18,4) NOT NULL,
    [CreadoPor] nvarchar(100) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_recomendacionVehiculoMercancia] PRIMARY KEY ([Id])
);
GO

-- --- iaSombra ---------------------------------------------------------------------
-- Comparación entre la recomendación del motor y la de un modelo de lenguaje que
-- corre en paralelo sin afectar la respuesta (modo sombra). Una fila por
-- recomendación comparada o por intento fallido (Estado trae el motivo). No guarda
-- el contenido enviado al modelo.
CREATE TABLE [dbo].[iaSombra] (
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
    [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_iaSombra_CreatedAt] DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_iaSombra] PRIMARY KEY ([Id])
    , CONSTRAINT [CK_iaSombra_OrigenSistema] CHECK ([OrigenSistema] IN (N'NEXO', N'COTIFLY'))
    , CONSTRAINT [CK_iaSombra_FlotaMotor] CHECK (ISJSON([FlotaMotor]) = 1)
    , CONSTRAINT [CK_iaSombra_FlotaIa] CHECK ([FlotaIa] IS NULL OR ISJSON([FlotaIa]) = 1)
    , CONSTRAINT [CK_iaSombra_Violaciones] CHECK ([Violaciones] IS NULL OR ISJSON([Violaciones]) = 1)
);
GO

-- --- Llaves foráneas ----------------------------------------------------------------

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
ALTER TABLE [dbo].[recomendacionVehiculo] ADD CONSTRAINT [FK_recomendacionVehiculo_recomendaciones]
    FOREIGN KEY ([IdRecomendacion]) REFERENCES [dbo].[recomendaciones] ([Id]);

ALTER TABLE [dbo].[recomendacionVehiculoMercancia] ADD CONSTRAINT [FK_recomendacionVehiculoMercancia_recomendacionVehiculo_IdRecomendacionVehiculo]
    FOREIGN KEY ([IdRecomendacionVehiculo]) REFERENCES [dbo].[recomendacionVehiculo] ([Id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[recomendacionVehiculoMercancia] ADD CONSTRAINT [FK_recomendacionVehiculoMercancia_tiposMercancia_IdMercancia]
    FOREIGN KEY ([IdMercancia]) REFERENCES [dbo].[tiposMercancia] ([Id]);
GO

-- --- Índices ------------------------------------------------------------------------

CREATE INDEX [IX_tiposMercancia_IdCategoria] ON [dbo].[tiposMercancia] ([IdCategoria]);
CREATE INDEX [IX_relacionVehiculosCarroceria_IdCarroceria] ON [dbo].[relacionVehiculosCarroceria] ([IdCarroceria]);
CREATE INDEX [IX_recomendaciones_Linea_Origen] ON [dbo].[recomendaciones] ([IdLineaSolicitud], [OrigenSistema]);
CREATE INDEX [IX_recomendacionVehiculo_IdLineaSolicitud_OrigenSistema] ON [dbo].[recomendacionVehiculo] ([IdLineaSolicitud], [OrigenSistema]);
CREATE INDEX [IX_recomendacionVehiculo_IdRecomendacion] ON [dbo].[recomendacionVehiculo] ([IdRecomendacion]);
CREATE INDEX [IX_recomendacionVehiculoMercancia_IdRecomendacionVehiculo] ON [dbo].[recomendacionVehiculoMercancia] ([IdRecomendacionVehiculo]);
CREATE INDEX [IX_iaSombra_IdRecomendacion] ON [dbo].[iaSombra] ([IdRecomendacion]);
CREATE INDEX [IX_iaSombra_CreatedAt] ON [dbo].[iaSombra] ([CreatedAt]) INCLUDE ([TokensEntrada], [TokensSalida]);
GO

-- =============================================================================
-- Parte 2: motor 3D (planes de cargue)
-- =============================================================================
-- Tablas del cálculo de acomodo tridimensional de la carga. Nombres en snake_case
-- porque siguen el modelo de datos de la aplicación (SQLAlchemy).

-- Límites generales de las piezas que acepta el motor 3D.
CREATE TABLE dbo.parametros_cubicacion (
    id INTEGER NOT NULL IDENTITY,
    largo_max_cm FLOAT NOT NULL,
    ancho_max_cm FLOAT NOT NULL,
    alto_max_cm FLOAT NOT NULL,
    peso_max_kg FLOAT NOT NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    PRIMARY KEY (id)
);
GO

-- Vehículos del motor 3D, con medidas internas en milímetros. Los predefinidos
-- (es_preset = 1) los crea la aplicación al arrancar si no existen.
CREATE TABLE dbo.vehiculos_cubicacion (
    id VARCHAR(36) NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    descripcion VARCHAR(300) NULL,
    largo_interno_mm FLOAT NOT NULL,
    ancho_interno_mm FLOAT NOT NULL,
    alto_interno_mm FLOAT NOT NULL,
    capacidad_kg FLOAT NOT NULL,
    tara_kg FLOAT NOT NULL,
    puerta_ancho_mm FLOAT NOT NULL,
    puerta_alto_mm FLOAT NOT NULL,
    puerta_posicion VARCHAR(20) NOT NULL,
    ejes_json VARCHAR(MAX) NOT NULL,
    tolerancia_paredes_mm FLOAT NOT NULL,
    tolerancia_entre_unidades_mm FLOAT NOT NULL,
    es_preset BIT NOT NULL,
    created_at DATETIMEOFFSET NOT NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    PRIMARY KEY (id)
);
GO

-- Un plan de cargue: el acomodo calculado de una carga en un vehículo.
CREATE TABLE dbo.planes_cargue (
    id VARCHAR(36) NOT NULL,
    nombre VARCHAR(150) NOT NULL,
    vehiculo_id VARCHAR(36) NOT NULL,
    linea_solicitud_id VARCHAR(36) NULL,
    numero_vehiculo INTEGER NOT NULL,
    estado VARCHAR(20) NOT NULL,
    estrategia VARCHAR(40) NOT NULL,
    ponderaciones_json VARCHAR(MAX) NULL,
    algoritmo VARCHAR(10) NOT NULL,
    semilla INTEGER NULL,
    peso_total_kg FLOAT NOT NULL,
    volumen_utilizado_m3 FLOAT NOT NULL,
    utilizacion_volumen FLOAT NOT NULL,
    utilizacion_piso FLOAT NOT NULL,
    puntaje FLOAT NOT NULL,
    incidencias_json VARCHAR(MAX) NOT NULL,
    error_mensaje VARCHAR(500) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY(vehiculo_id) REFERENCES dbo.vehiculos_cubicacion (id)
);
GO

CREATE INDEX ix_planes_cargue_linea_solicitud_id ON dbo.planes_cargue (linea_solicitud_id);
GO

-- Las piezas de un plan de cargue, con sus medidas y restricciones de manejo.
CREATE TABLE dbo.items_plan_cargue (
    id VARCHAR(36) NOT NULL,
    plan_cargue_id VARCHAR(36) NOT NULL,
    sku VARCHAR(60) NOT NULL,
    descripcion VARCHAR(200) NOT NULL,
    pedido VARCHAR(60) NULL,
    cliente VARCHAR(120) NULL,
    destino VARCHAR(120) NULL,
    secuencia_entrega INTEGER NOT NULL,
    largo_mm FLOAT NOT NULL,
    ancho_mm FLOAT NOT NULL,
    alto_mm FLOAT NOT NULL,
    peso_kg FLOAT NOT NULL,
    cantidad INTEGER NOT NULL,
    color VARCHAR(9) NULL,
    prioridad INTEGER NOT NULL,
    tipo_embalaje VARCHAR(40) NULL,
    apilable BIT NOT NULL,
    max_niveles INTEGER NOT NULL,
    max_peso_encima_kg FLOAT NOT NULL,
    rotable BIT NOT NULL,
    mantener_vertical BIT NOT NULL,
    fragil BIT NOT NULL,
    peligroso BIT NOT NULL,
    refrigerado BIT NOT NULL,
    grupo_compatibilidad VARCHAR(40) NULL,
    observaciones VARCHAR(300) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY(plan_cargue_id) REFERENCES dbo.planes_cargue (id)
);
GO

CREATE INDEX ix_items_plan_cargue_plan_cargue_id ON dbo.items_plan_cargue (plan_cargue_id);
GO

-- La posición de cada unidad dentro del vehículo (coordenadas en milímetros),
-- con su orden de cargue, soporte y estabilidad.
CREATE TABLE dbo.colocaciones_cargue (
    id VARCHAR(36) NOT NULL,
    plan_cargue_id VARCHAR(36) NOT NULL,
    item_plan_cargue_id VARCHAR(36) NOT NULL,
    indice_unidad INTEGER NOT NULL,
    cargada BIT NOT NULL,
    x FLOAT NOT NULL,
    y FLOAT NOT NULL,
    z FLOAT NOT NULL,
    largo_colocado_mm FLOAT NOT NULL,
    ancho_colocado_mm FLOAT NOT NULL,
    alto_colocado_mm FLOAT NOT NULL,
    orientacion_indice INTEGER NOT NULL,
    secuencia_cargue INTEGER NOT NULL,
    soporte_colocacion_id VARCHAR(36) NULL,
    porcentaje_soporte FLOAT NOT NULL,
    peso_encima_kg FLOAT NOT NULL,
    estado_estabilidad VARCHAR(20) NOT NULL,
    accesibilidad VARCHAR(30) NOT NULL,
    bloqueada BIT NOT NULL,
    ajuste_manual BIT NOT NULL,
    codigo_rechazo VARCHAR(30) NULL,
    razon_rechazo VARCHAR(500) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    updated_at DATETIMEOFFSET NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY(item_plan_cargue_id) REFERENCES dbo.items_plan_cargue (id),
    FOREIGN KEY(plan_cargue_id) REFERENCES dbo.planes_cargue (id)
);
GO

CREATE INDEX ix_colocaciones_cargue_item_plan_cargue_id ON dbo.colocaciones_cargue (item_plan_cargue_id);
GO

CREATE INDEX ix_colocaciones_cargue_plan_cargue_id ON dbo.colocaciones_cargue (plan_cargue_id);
GO
