-- =============================================================================
-- Esquema de NEXO (Nexo) — dbInformesAutomatizados_Pruebas.db_NEXO
-- =============================================================================
-- dbInformesAutomatizados_Pruebas es una base COMPARTIDA con otra app ajena a
-- NEXO (esa otra app ya vivía en 'dbo' y tiene su propio esquema 'dbo_prueba').
-- Por eso NexoDbContext fija HasDefaultSchema(clsEsquemaNexo.Nombre): así
-- las tablas de NEXO no se mezclan con las de esa otra app.
--
-- "db_NEXO" (usado en todo este archivo) es el NOMBRE POR DEFECTO documentado
-- en .env.example, no un valor fijo en código: el esquema real se lee de la
-- variable de entorno Database__Esquema (ver clsEsquemaNexo.cs), obligatoria y
-- sin default en el código (fail-closed). Para desplegar contra otro nombre de
-- esquema, cambie esa variable — no hace falta tocar este archivo ni el código,
-- solo reemplazar mentalmente "db_NEXO" por el valor configurado al leer este DDL.
--
-- Este archivo ES la fuente de la verdad del esquema — este proyecto NO usa
-- migraciones de EF Core. Para crear la base la primera vez (producción o
-- cualquier ambiente nuevo), correr este script completo, de una sola pasada,
-- contra una base vacía. Cualquier cambio de esquema se hace editando
-- directamente este archivo (y aplicándolo a mano contra cada base existente),
-- nunca con `dotnet ef migrations add` — no hay carpeta Migrations en el
-- proyecto. Después de crear el esquema, ver docs/Database/seeder/README.md
-- para los catálogos que hay que poblar con datos ya calculados (tampoco por
-- migración).
--
-- Orden: 1) tablas agrupadas por módulo de negocio, cada una con un comentario
-- corto de su objetivo; 2) llaves foráneas; 3) índices; 4) triggers. Las FK
-- van aparte porque varias tablas se referencian cruzado (p. ej.
-- tbl_cotizaciones <-> tbl_tarifas_historicas) y así el orden de creación de
-- las tablas no importa. Los triggers van al final porque necesitan que sus
-- tablas ya existan (SQL Server exige que `CREATE TRIGGER` sea la única
-- sentencia de su lote — por eso llevan `GO` antes y después).
-- =============================================================================

-- =============================================================================
-- 1. TABLAS
-- =============================================================================

-- --- Identidad y autorización -------------------------------------------------
-- No hay login propio: la identidad la resuelve el portal de la Administración
-- (X-Portal-Usuario-Id en cada petición, ver docs/contrato-integracion). Estas
-- dos tablas son la copia local de esa identidad y la traducción de sus roles.

-- Cuenta local de cada persona que ha entrado a Nexo: nombre, email,
-- uno de los 6 roles de Nexo y (si vino del portal real) su IdentidadPortalId.
CREATE TABLE [db_NEXO].[tbl_usuarios] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(200) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Rol] nvarchar(40) NOT NULL,
    [Activo] bit NOT NULL DEFAULT (CONVERT([bit],(1))),
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [IdentidadPortalId] bigint NULL
    , CONSTRAINT [PK_usuarios] PRIMARY KEY ([Id])
);

-- Traduce el rol/permiso tal como lo nombra el portal (por id o por texto) a
-- uno de los 6 roles de Nexo. Sin esta fila, el usuario se autentica pero
-- queda sin rol (403, nunca 401 — ver IdentidadPortalAuthenticationHandler).
CREATE TABLE [db_NEXO].[tbl_mapeo_rol_portal] (
    [Id] uniqueidentifier NOT NULL,
    [IdRolPortal] bigint NULL,
    [DescripcionRolPortal] nvarchar(200) NULL,
    [Rol] nvarchar(40) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL
    , CONSTRAINT [PK_mapeo_rol_portal] PRIMARY KEY ([Id])
);

-- --- Catálogos base ------------------------------------------------------------
-- Maestros que el resto del sistema referencia: a quién se le cobra, entre qué
-- ciudades, en qué tipo de vehículo y con qué servicios adicionales.

-- Cliente al que se le cotiza el flete (razón social, NIT único, teléfonos).
CREATE TABLE [db_NEXO].[tbl_clientes] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(200) NOT NULL,
    [Nit] nvarchar(20) NOT NULL,
    [RazonSocial] nvarchar(250) NULL,
    [Telefonos] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_clientes] PRIMARY KEY ([Id])
);

-- Par origen-destino (único por par) sobre el que se cotiza y se cargan tarifas.
CREATE TABLE [db_NEXO].[tbl_rutas] (
    [Id] uniqueidentifier NOT NULL,
    [Origen] nvarchar(150) NOT NULL,
    [Destino] nvarchar(150) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_rutas] PRIMARY KEY ([Id])
);

-- Tipo/carrocería/rango de peso del vehículo que puede prestar el servicio.
CREATE TABLE [db_NEXO].[tbl_configuraciones_vehiculo] (
    [Id] uniqueidentifier NOT NULL,
    [TipoVehiculo] nvarchar(100) NOT NULL,
    [Carroceria] nvarchar(100) NULL,
    [RangoPeso] nvarchar(50) NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_configuraciones_vehiculo] PRIMARY KEY ([Id])
);

-- Catálogo propio de municipios (Colombia/Venezuela/Ecuador) para el
-- desplegable de Origen/Destino en Captura de Solicitudes (S4-HU-09.1).
-- Poblada UNA SOLA VEZ desde tblSAPDANE/tblZonasTarificacionRelacion/
-- tblSAPTarifas(Log) del datacenter corporativo — sin sincronización
-- periódica, con un script de poblado aparte (no por migración de EF Core).
-- CodigoDane no es único por sí solo (choca entre Venezuela y Ecuador) — Id
-- es autoincremental a propósito, la llave natural real es (CodigoDane, Pais).
CREATE TABLE [db_NEXO].[tbl_municipios] (
    [Id] int NOT NULL IDENTITY,
    [CodigoDane] nvarchar(20) NOT NULL,
    [Municipio] nvarchar(150) NOT NULL,
    [Departamento] nvarchar(150) NOT NULL,
    [Pais] nvarchar(2) NOT NULL,
    [NombreTarifasPlaza] nvarchar(60) NULL,
    [ZonaTarificacion] nvarchar(60) NULL
    , CONSTRAINT [PK_municipios] PRIMARY KEY ([Id])
);

-- Catálogo CRUD propio de bandas de peso (kg) para el formulario de Tarifas de
-- Plaza (S4-HU-10.1) — reemplaza el número libre del sistema externo. Poblada
-- inicialmente con las 7 bandas de negocio vía docs/Database/seeder/pesos.csv
-- (no por migración de EF Core). El CRUD (crear/editar/eliminar/listar) es
-- trabajo pendiente de backend, no está en este repo todavía.
-- PesoMaximo NULL = banda abierta (PBV +10500).
CREATE TABLE [db_NEXO].[tbl_pesos] (
    [Id] int NOT NULL IDENTITY,
    [PesoMinimo] int NOT NULL,
    [PesoMaximo] int NULL
    , CONSTRAINT [PK_pesos] PRIMARY KEY ([Id])
);

-- Dimensiones/capacidad físicas reales de un vehículo, para el Plan de Cargue
-- (cubicación 3D): distinto de tbl_configuraciones_vehiculo, que es solo catálogo
-- comercial (tipo/carrocería), no geometría.
CREATE TABLE [db_NEXO].[tbl_vehiculos_cubicacion] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(300) NULL,
    [LargoInternoMm] decimal(12,2) NOT NULL,
    [AnchoInternoMm] decimal(12,2) NOT NULL,
    [AltoInternoMm] decimal(12,2) NOT NULL,
    [CapacidadKg] decimal(12,2) NOT NULL,
    [TaraKg] decimal(12,2) NOT NULL,
    [PuertaAnchoMm] decimal(12,2) NOT NULL,
    [PuertaAltoMm] decimal(12,2) NOT NULL,
    [PuertaPosicion] nvarchar(20) NOT NULL,
    [EjesJson] nvarchar(max) NOT NULL,
    [ToleranciaParedesMm] decimal(12,2) NOT NULL,
    [ToleranciaEntreUnidadesMm] decimal(12,2) NOT NULL,
    [EsPreset] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_vehiculos_cubicacion] PRIMARY KEY ([Id])
);

-- Servicio adicional facturable (cargue, descargue, etc.) que se agrega a una
-- cotización; lo mantiene ServiciosLogisticos/AdminTarifas/Admin.
CREATE TABLE [db_NEXO].[tbl_servicios_conexos] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(120) NOT NULL,
    [ReglaCobro] nvarchar(max) NULL,
    [ValorBase] decimal(18,2) NULL,
    [VigenteHasta] date NULL,
    [Activo] bit NOT NULL DEFAULT (CONVERT([bit],(1))),
    [ActualizadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [RowVersion] timestamp NULL,
    [Categoria] nvarchar(20) NOT NULL DEFAULT (N'logistico')
    , CONSTRAINT [PK_servicios_conexos] PRIMARY KEY ([Id])
);

-- --- Solicitudes y costeo -------------------------------------------------------
-- El flujo operativo: una solicitud (spot o licitación) del cliente, sus líneas
-- de flete, y el rastro de qué líneas quedaron sin poder costearse solas.

-- Cabecera de una solicitud de flete (spot/licitación), con el score que la
-- prioriza en la bandeja del CoordinadorFletes.
CREATE TABLE [db_NEXO].[tbl_solicitudes] (
    [Id] uniqueidentifier NOT NULL,
    [ClienteId] uniqueidentifier NOT NULL,
    [Tipo] nvarchar(20) NOT NULL,
    [Modalidad] nvarchar(30) NULL,
    [CondicionesServicio] nvarchar(max) NULL,
    [Deadline] datetimeoffset NULL,
    [ProbabilidadCierre] decimal(9,4) NULL,
    [ValorEstimado] decimal(18,2) NULL,
    [ScorePrioridad] decimal(6,2) NOT NULL DEFAULT ((0.0)),
    [Estado] nvarchar(20) NOT NULL DEFAULT (N'Pendiente'),
    [CreadoPor] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [RowVersion] timestamp NULL
    , CONSTRAINT [PK_solicitudes] PRIMARY KEY ([Id])
);

-- Cada tramo/línea de carga dentro de una solicitud: ruta, vehículo, peso,
-- dimensiones — la unidad real que se termina costeando o cargando.
CREATE TABLE [db_NEXO].[tbl_lineas_solicitud] (
    [Id] uniqueidentifier NOT NULL,
    [SolicitudId] uniqueidentifier NOT NULL,
    [RutaId] uniqueidentifier NULL,
    [ConfigVehiculoId] uniqueidentifier NULL,
    [Tipologia] nvarchar(30) NULL,
    [Peso] decimal(18,2) NULL,
    [Dimensiones] nvarchar(max) NULL,
    [ValorMercancia] decimal(18,2) NULL,
    [EstadoLinea] nvarchar(20) NOT NULL DEFAULT (N'PorCostear'),
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [ClaseContenedor] nvarchar(20) NULL,
    [ClaseViaje] nvarchar(20) NULL,
    [Exclusivo] bit NOT NULL DEFAULT (CONVERT([bit],(0))),
    [LevanteContenedorDiferente] bit NOT NULL DEFAULT (CONVERT([bit],(0))),
    [LineaNegocio] nvarchar(30) NULL,
    [CiudadLevanteContenedor] nvarchar(150) NULL,
    [ValorAprovisionamiento] decimal(18,2) NULL,
    [CiudadDevolucionContenedor] nvarchar(150) NULL,
    [Consolidado] bit NOT NULL DEFAULT (CONVERT([bit],(0))),
    [RetornaContenedor] bit NOT NULL DEFAULT (CONVERT([bit],(0))),
    [MotivoPendiente] nvarchar(500) NULL
    , CONSTRAINT [PK_lineas_solicitud] PRIMARY KEY ([Id])
);

-- Líneas que la carga automática desde Excel NO pudo costear sola (falta de
-- tarifa, dato técnico, etc.), con el motivo — para que alguien las resuelva
-- a mano. Un solo sitio para el motivo: dos sitios contando lo mismo acaban
-- contradiciéndose.
CREATE TABLE [db_NEXO].[tbl_lineas_pendientes_de_costeo] (
    [Id] uniqueidentifier NOT NULL,
    [TrabajoId] uniqueidentifier NOT NULL,
    [SolicitudId] uniqueidentifier NOT NULL,
    [LineaSolicitudId] uniqueidentifier NOT NULL,
    [Tipo] nvarchar(20) NOT NULL,
    [Motivo] nvarchar(500) NOT NULL,
    [ResueltaEn] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_lineas_pendientes_de_costeo] PRIMARY KEY ([Id])
);

-- Estado de la SAGA (MassTransit) del auto-costeo de un Excel capturado: cuántas
-- líneas se resolvieron solas, cuántas quedaron pendientes y por qué. Vive
-- aquí y no en otra base porque ES la respuesta al cliente que pregunta por
-- su trabajo: un reinicio no puede borrarla.
CREATE TABLE [db_NEXO].[tbl_trabajos_auto_costeo] (
    [CorrelationId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [CurrentState] nvarchar(64) NOT NULL,
    [SolicitudId] uniqueidentifier NOT NULL,
    [SolicitadoPor] uniqueidentifier NOT NULL,
    [SolicitadoEn] datetimeoffset NOT NULL,
    [TerminadoEn] datetimeoffset NULL,
    [TotalLineas] int NULL,
    [Resueltas] int NOT NULL,
    [PendientesTecnicas] int NOT NULL,
    [RequierenManual] int NOT NULL,
    [TokenExpiracion] uniqueidentifier NULL,
    [Secuencia] int NOT NULL DEFAULT ((0))
    , CONSTRAINT [PK_trabajos_auto_costeo] PRIMARY KEY ([CorrelationId])
);

-- --- Cotizaciones ----------------------------------------------------------------
-- Lo que se le entrega al comercial: el precio del flete por solicitud, línea
-- por línea, con sus servicios conexos.

-- Cabecera de la cotización de una solicitud: estado (Borrador→Entregada),
-- resultado (ganada/perdida) y total.
CREATE TABLE [db_NEXO].[tbl_cotizaciones] (
    [Id] uniqueidentifier NOT NULL,
    [SolicitudId] uniqueidentifier NOT NULL,
    [Estado] nvarchar(20) NOT NULL DEFAULT (N'Borrador'),
    [Resultado] nvarchar(20) NOT NULL DEFAULT (N'SinDefinir'),
    [Total] decimal(18,2) NOT NULL DEFAULT ((0.0)),
    [EntregadaPor] uniqueidentifier NULL,
    [EntregadaAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [RowVersion] timestamp NULL
    , CONSTRAINT [PK_cotizaciones] PRIMARY KEY ([Id])
);

-- Valor del flete cotizado para una línea de solicitud puntual, y de dónde
-- salió ese valor (Fuente: SAP/SICE-TAC/histórico/manual).
CREATE TABLE [db_NEXO].[tbl_cotizacion_lineas] (
    [Id] uniqueidentifier NOT NULL,
    [CotizacionId] uniqueidentifier NOT NULL,
    [LineaSolicitudId] uniqueidentifier NULL,
    [ValorFlete] decimal(18,2) NOT NULL,
    [Fuente] nvarchar(20) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [ValorAprovisionamiento] decimal(18,2) NULL,
    [ValorRetornoContenedor] decimal(18,2) NULL
    , CONSTRAINT [PK_cotizacion_lineas] PRIMARY KEY ([Id])
);

-- Servicio conexo aplicado a una cotización, con la cantidad y el valor que
-- realmente se cobró (puede diferir del ValorBase del catálogo).
CREATE TABLE [db_NEXO].[tbl_cotizacion_conexos] (
    [Id] uniqueidentifier NOT NULL,
    [CotizacionId] uniqueidentifier NOT NULL,
    [ServicioConexoId] uniqueidentifier NULL,
    [Cantidad] decimal(12,2) NOT NULL,
    [ValorAplicado] decimal(18,2) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_cotizacion_conexos] PRIMARY KEY ([Id])
);

-- --- Biblioteca de tarifas ---------------------------------------------------------
-- Las fuentes de precio que alimentan la propuesta de tarifa: piso SICE-TAC
-- oficial, histórico de lo realmente cobrado, y ajustes/reglas manuales.

-- Piso de tarifa SICE-TAC importado desde Excel (matriz oficial del
-- Ministerio): único por periodo+ruta DANE+configuración+tipo de carga.
CREATE TABLE [db_NEXO].[tbl_tarifas_sicetac] (
    [Id] uniqueidentifier NOT NULL,
    [SiceId] bigint NULL,
    [Periodo] int NOT NULL,
    [RutaId] uniqueidentifier NULL,
    [OrigenDane] nvarchar(12) NOT NULL,
    [DestinoDane] nvarchar(12) NOT NULL,
    [NombreRuta] nvarchar(300) NULL,
    [Configuracion] nvarchar(50) NOT NULL,
    [TipoCarga] nvarchar(80) NOT NULL,
    [UnidadTransporte] nvarchar(80) NOT NULL,
    [CondicionCarga] nvarchar(20) NOT NULL,
    [ValorPiso] decimal(18,2) NOT NULL,
    [ValorHora] decimal(18,2) NULL,
    [HorasPactadas] decimal(9,2) NULL,
    [HorasRecorrido] decimal(9,2) NULL,
    [ValorSiceHora] decimal(18,2) NULL,
    [ViaEstandar] bit NULL,
    [FechaCreacionSice] datetimeoffset NULL,
    [FechaModificacionSice] datetimeoffset NULL,
    [ImportadoAt] datetimeoffset NOT NULL,
    [ImportadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_tarifas_sicetac] PRIMARY KEY ([Id])
);

-- Lo que REALMENTE se cobró en tbl_cotizaciones pasadas (Tarifas Ganadas), para
-- proponer tarifa por precedente cuando SAP/SICE-TAC no alcanzan.
CREATE TABLE [db_NEXO].[tbl_tarifas_historicas] (
    [Id] uniqueidentifier NOT NULL,
    [RutaId] uniqueidentifier NULL,
    [ConfigVehiculoId] uniqueidentifier NULL,
    [Tipologia] nvarchar(30) NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Fecha] date NOT NULL,
    [CotizacionId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_tarifas_historicas] PRIMARY KEY ([Id])
);

-- Ruta+vehículo donde SOLO aplica SICE-TAC (se excluye la plaza SAP de la
-- propuesta) — decisión manual de AdminTarifas/Admin, única por combinación.
CREATE TABLE [db_NEXO].[tbl_reglas_solo_sicetac] (
    [Id] uniqueidentifier NOT NULL,
    [Origen] nvarchar(150) NOT NULL,
    [Destino] nvarchar(150) NOT NULL,
    [TipoVehiculo] nvarchar(40) NOT NULL,
    [CreadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_reglas_solo_sicetac] PRIMARY KEY ([Id])
);

-- Corrección manual de la vigencia de una tarifa de plaza SAP puntual (única
-- por TarifaPlazaId), con quién y por qué la ajustó.
CREATE TABLE [db_NEXO].[tbl_ajustes_vigencia_plaza] (
    [Id] uniqueidentifier NOT NULL,
    [TarifaPlazaId] int NOT NULL,
    [VigenciaAjustada] datetime2 NOT NULL,
    [VigenciaOriginal] datetime2 NULL,
    [Motivo] nvarchar(300) NULL,
    [AjustadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_ajustes_vigencia_plaza] PRIMARY KEY ([Id])
);

-- Tarifa de plaza gobernada por Nexo (S4-HU-10.1) — recrea el formulario de
-- captura que hoy vive en un sistema externo; NO es una copia de solo lectura
-- de tblSAPTarifas (a diferencia de tbl_tarifas_sicetac).
--
-- Origen/Destino referencian el catálogo propio de municipios
-- (tbl_municipios). RangoPeso (PesoId, catálogo tbl_pesos) y TipoVehiculo
-- (código "2S2"/"3S3"/"C2", igual que clsConfiguracionVehiculo.TipoVehiculo)
-- son dos dimensiones alternativas — contenedor por peso vs. tarifa nacional
-- por tipo de vehículo (Turbo/Sencillo/Mini Mula/Mula) — una tarifa real usa
-- solo una de las dos, por eso ambas son NULLABLE.
--
-- LineaNegocio/ClaseViaje/ClaseContenedor guardan el NOMBRE completo (no el
-- ID) de su catálogo externo (tblSAPLineaNegocio/tblSAPClaseViaje/
-- tblSAPClaseContenedor, dbSolucionesTI): ese ID no significa nada dentro de
-- Nexo (otra base de datos, otro servidor, sin FK real posible), así que el
-- nombre deja la fila legible sin consultar el catálogo externo.
-- ClaseContenedor es NULL cuando la tarifa no es de contenedor (ej. carga
-- suelta nacional).
--
-- Única por sus 7 dimensiones (línea de negocio + clase de viaje + origen +
-- destino + clase de contenedor + rango de peso + tipo de vehículo);
-- Valor/Vigencia/DetallesTarifa es lo que cambia con el tiempo para una misma
-- combinación — ver tbl_tarifas_plaza_historico, que guarda cada valor
-- anterior.
CREATE TABLE [db_NEXO].[tbl_tarifas_plaza] (
    [Id] uniqueidentifier NOT NULL,
    [LineaNegocio] nvarchar(100) NOT NULL,
    [ClaseViaje] nvarchar(50) NOT NULL,
    [OrigenMunicipioId] int NOT NULL,
    [DestinoMunicipioId] int NOT NULL,
    [ClaseContenedor] nvarchar(50) NULL,
    [PesoId] int NULL,
    [TipoVehiculo] nvarchar(40) NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Vigencia] date NOT NULL,
    [DetallesTarifa] nvarchar(2000) NOT NULL,
    [CreadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_tarifas_plaza] PRIMARY KEY ([Id])
    , CONSTRAINT [UQ_tarifas_plaza_dimensiones] UNIQUE ([LineaNegocio], [ClaseViaje], [OrigenMunicipioId], [DestinoMunicipioId], [ClaseContenedor], [PesoId], [TipoVehiculo])
);

-- Snapshot del valor ANTERIOR de una tarifa de plaza, cada vez que se
-- actualiza una fila de tbl_tarifas_plaza — igual que tblSAPTarifasLog es al
-- tblSAPTarifas del sistema externo. La llena sola el trigger
-- TRG_tarifas_plaza_historico (sección 4, TRIGGERS) — no se escribe a mano.
CREATE TABLE [db_NEXO].[tbl_tarifas_plaza_historico] (
    [Id] uniqueidentifier NOT NULL,
    [TarifaPlazaId] uniqueidentifier NOT NULL,
    [LineaNegocio] nvarchar(100) NOT NULL,
    [ClaseViaje] nvarchar(50) NOT NULL,
    [OrigenMunicipioId] int NOT NULL,
    [DestinoMunicipioId] int NOT NULL,
    [ClaseContenedor] nvarchar(50) NULL,
    [PesoId] int NULL,
    [TipoVehiculo] nvarchar(40) NULL,
    [Valor] decimal(18,2) NOT NULL,
    [Vigencia] date NOT NULL,
    [DetallesTarifa] nvarchar(2000) NOT NULL,
    [CreadoPor] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ReemplazadoAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_tarifas_plaza_historico] PRIMARY KEY ([Id])
);

-- --- Priorización ------------------------------------------------------------------
-- Qué tan urgente es cada solicitud y con qué pesos se calcula ese puntaje.

-- Estado de la SAGA (MassTransit) que rearma el score de una solicitud cada
-- vez que cambian sus datos o pasa el reloj — el reloj vive en el broker, no
-- en el proceso del API, así que escalar a más réplicas no lo duplica.
CREATE TABLE [db_NEXO].[tbl_cadena_priorizacion] (
    [CorrelationId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [CurrentState] nvarchar(64) NOT NULL,
    [TokenTic] uniqueidentifier NULL,
    [ArmadaEn] datetimeoffset NOT NULL,
    [RearmadaEn] datetimeoffset NOT NULL,
    [UltimoRecalculoEn] datetimeoffset NULL,
    [RecalculosPorReloj] int NOT NULL,
    [RecalculosPorParametros] int NOT NULL
    , CONSTRAINT [PK_cadena_priorizacion] PRIMARY KEY ([CorrelationId])
);

-- Pesos y constantes de la fórmula del score (urgencia/valor/probabilidad/
-- tipo) — fila única de configuración, editable solo por Admin.
CREATE TABLE [db_NEXO].[tbl_parametros_score] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [PesoUrgencia] float NOT NULL,
    [PesoValor] float NOT NULL,
    [PesoProbabilidad] float NOT NULL,
    [PesoTipo] float NOT NULL,
    [HorizonteHoras] float NOT NULL,
    [ValorReferencia] decimal(18,2) NOT NULL,
    [PuntajeLicitacion] float NOT NULL,
    [PuntajeSpot] float NOT NULL,
    [ValorPorDefectoSinValor] float NOT NULL,
    [ProbabilidadPorDefecto] float NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [RowVersion] timestamp NULL
    , CONSTRAINT [PK_parametros_score] PRIMARY KEY ([Id])
);

-- --- Plan de cargue / cubicación 3D -------------------------------------------------
-- El resultado de la optimización de cargue: qué plan, qué ítems entraron y
-- dónde quedó colocado cada uno dentro del vehículo.

-- Un plan de cargue optimizado para un vehículo: estrategia usada, algoritmo,
-- utilización lograda y las incidencias encontradas.
CREATE TABLE [db_NEXO].[tbl_planes_cargue] (
    [Id] uniqueidentifier NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [VehiculoId] uniqueidentifier NOT NULL,
    [Estado] nvarchar(20) NOT NULL,
    [Estrategia] nvarchar(40) NOT NULL,
    [PonderacionesJson] nvarchar(max) NULL,
    [Algoritmo] nvarchar(10) NOT NULL,
    [Semilla] int NULL,
    [PesoTotalKg] decimal(12,2) NOT NULL,
    [VolumenUtilizadoM3] decimal(12,3) NOT NULL,
    [UtilizacionVolumen] decimal(6,2) NOT NULL,
    [UtilizacionPiso] decimal(6,2) NOT NULL,
    [Puntaje] decimal(6,2) NOT NULL,
    [IncidenciasJson] nvarchar(max) NOT NULL,
    [MetricasJson] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [LineaSolicitudId] uniqueidentifier NULL,
    [NumeroVehiculo] int NOT NULL DEFAULT ((0))
    , CONSTRAINT [PK_planes_cargue] PRIMARY KEY ([Id])
);

-- Mercancía a cargar dentro de un plan: dimensiones, peso, y reglas físicas
-- (apilable, frágil, peligroso, refrigerado) que el algoritmo debe respetar.
CREATE TABLE [db_NEXO].[tbl_items_plan_cargue] (
    [Id] uniqueidentifier NOT NULL,
    [PlanCargueId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(60) NOT NULL,
    [Descripcion] nvarchar(200) NOT NULL,
    [Pedido] nvarchar(60) NULL,
    [Cliente] nvarchar(120) NULL,
    [Destino] nvarchar(120) NULL,
    [SecuenciaEntrega] int NOT NULL,
    [LargoMm] decimal(12,2) NOT NULL,
    [AnchoMm] decimal(12,2) NOT NULL,
    [AltoMm] decimal(12,2) NOT NULL,
    [PesoKg] decimal(12,2) NOT NULL,
    [Cantidad] int NOT NULL,
    [Color] nvarchar(9) NULL,
    [Prioridad] int NOT NULL,
    [TipoEmbalaje] nvarchar(40) NULL,
    [Apilable] bit NOT NULL,
    [MaxNiveles] int NOT NULL,
    [MaxPesoEncimaKg] decimal(12,2) NOT NULL,
    [Rotable] bit NOT NULL,
    [MantenerVertical] bit NOT NULL,
    [Fragil] bit NOT NULL,
    [Peligroso] bit NOT NULL,
    [Refrigerado] bit NOT NULL,
    [GrupoCompatibilidad] nvarchar(40) NULL,
    [Observaciones] nvarchar(300) NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_items_plan_cargue] PRIMARY KEY ([Id])
);

-- Dónde quedó colocada (X/Y/Z, orientación, secuencia) cada unidad de un ítem
-- dentro del vehículo, y si el algoritmo la rechazó y por qué.
CREATE TABLE [db_NEXO].[tbl_colocaciones_cargue] (
    [Id] uniqueidentifier NOT NULL,
    [PlanCargueId] uniqueidentifier NOT NULL,
    [ItemPlanCargueId] uniqueidentifier NOT NULL,
    [IndiceUnidad] int NOT NULL,
    [Cargada] bit NOT NULL,
    [X] decimal(12,2) NOT NULL,
    [Y] decimal(12,2) NOT NULL,
    [Z] decimal(12,2) NOT NULL,
    [LargoColocadoMm] decimal(12,2) NOT NULL,
    [AnchoColocadoMm] decimal(12,2) NOT NULL,
    [AltoColocadoMm] decimal(12,2) NOT NULL,
    [OrientacionIndice] int NOT NULL,
    [SecuenciaCargue] int NOT NULL,
    [SoporteColocacionId] uniqueidentifier NULL,
    [PorcentajeSoporte] decimal(12,2) NOT NULL,
    [PesoEncimaKg] decimal(12,2) NOT NULL,
    [EstadoEstabilidad] nvarchar(20) NOT NULL,
    [Accesibilidad] nvarchar(30) NOT NULL,
    [Bloqueada] bit NOT NULL,
    [AjusteManual] bit NOT NULL,
    [CodigoRechazo] nvarchar(30) NULL,
    [RazonRechazo] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset()),
    [UpdatedAt] datetimeoffset NOT NULL DEFAULT (sysdatetimeoffset())
    , CONSTRAINT [PK_colocaciones_cargue] PRIMARY KEY ([Id])
);

-- --- Auditoría -----------------------------------------------------------------------

-- Rastro de quién cambió qué (entidad + diff) y cuándo, para trazabilidad.
CREATE TABLE [db_NEXO].[tbl_audit_log] (
    [Id] uniqueidentifier NOT NULL,
    [UsuarioId] uniqueidentifier NULL,
    [Entidad] nvarchar(100) NULL,
    [EntidadId] uniqueidentifier NULL,
    [Accion] nvarchar(50) NULL,
    [Diff] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL
    , CONSTRAINT [PK_audit_log] PRIMARY KEY ([Id])
);

-- --- Mensajería (MassTransit — Transactional Outbox/Inbox) ---------------------------
-- Viven en ESTA base a propósito: la garantía de atomicidad depende de que el
-- mensaje y los datos del caso de uso se guarden en la misma transacción.

-- Deduplicación de mensajes entrantes por consumidor (evita procesar dos veces
-- el mismo mensaje si el broker lo reentrega).
CREATE TABLE [db_NEXO].[InboxState] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [ConsumerId] uniqueidentifier NOT NULL,
    [LockId] uniqueidentifier NOT NULL,
    [RowVersion] timestamp NULL,
    [Received] datetime2 NOT NULL,
    [ReceiveCount] int NOT NULL,
    [ExpirationTime] datetime2 NULL,
    [Consumed] datetime2 NULL,
    [Delivered] datetime2 NULL,
    [LastSequenceNumber] bigint NULL
    , CONSTRAINT [PK_InboxState] PRIMARY KEY ([Id])
);

-- Cola transaccional de mensajes salientes: se escriben aquí en la MISMA
-- transacción del caso de uso, y un proceso aparte los publica al broker.
CREATE TABLE [db_NEXO].[OutboxMessage] (
    [SequenceNumber] bigint IDENTITY(1,1) NOT NULL,
    [EnqueueTime] datetime2 NULL,
    [SentTime] datetime2 NOT NULL,
    [Headers] nvarchar(max) NULL,
    [Properties] nvarchar(max) NULL,
    [InboxMessageId] uniqueidentifier NULL,
    [InboxConsumerId] uniqueidentifier NULL,
    [OutboxId] uniqueidentifier NULL,
    [MessageId] uniqueidentifier NOT NULL,
    [ContentType] nvarchar(256) NOT NULL,
    [MessageType] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [ConversationId] uniqueidentifier NULL,
    [CorrelationId] uniqueidentifier NULL,
    [InitiatorId] uniqueidentifier NULL,
    [RequestId] uniqueidentifier NULL,
    [SourceAddress] nvarchar(256) NULL,
    [DestinationAddress] nvarchar(256) NULL,
    [ResponseAddress] nvarchar(256) NULL,
    [FaultAddress] nvarchar(256) NULL,
    [ExpirationTime] datetime2 NULL
    , CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([SequenceNumber])
);

-- Un outbox por unidad lógica de trabajo (p. ej. una saga): agrupa y ordena
-- los OutboxMessage que le pertenecen.
CREATE TABLE [db_NEXO].[OutboxState] (
    [OutboxId] uniqueidentifier NOT NULL,
    [LockId] uniqueidentifier NOT NULL,
    [RowVersion] timestamp NULL,
    [Created] datetime2 NOT NULL,
    [Delivered] datetime2 NULL,
    [LastSequenceNumber] bigint NULL
    , CONSTRAINT [PK_OutboxState] PRIMARY KEY ([OutboxId])
);

-- --- Libro de control de EF Core ------------------------------------------------------
-- Qué migraciones ya se aplicaron contra esta base. La mantiene EF Core solo
-- (dotnet ef / Database.Migrate()) — nunca se edita a mano.
CREATE TABLE [db_NEXO].[__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);

-- =============================================================================
-- 2. LLAVES FORÁNEAS
-- =============================================================================
-- Todas se agregan después de crear las tablas para no depender del orden de
-- creación (hay referencias cruzadas, p. ej. tbl_cotizaciones <-> tbl_tarifas_historicas).

ALTER TABLE [db_NEXO].[tbl_audit_log] ADD CONSTRAINT [FK_audit_log_usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_colocaciones_cargue] ADD CONSTRAINT [FK_colocaciones_cargue_items_plan_cargue_ItemPlanCargueId] FOREIGN KEY ([ItemPlanCargueId]) REFERENCES [db_NEXO].[tbl_items_plan_cargue] ([Id]);
ALTER TABLE [db_NEXO].[tbl_colocaciones_cargue] ADD CONSTRAINT [FK_colocaciones_cargue_planes_cargue_PlanCargueId] FOREIGN KEY ([PlanCargueId]) REFERENCES [db_NEXO].[tbl_planes_cargue] ([Id]) ON DELETE CASCADE;
ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] ADD CONSTRAINT [FK_cotizacion_conexos_cotizaciones_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [db_NEXO].[tbl_cotizaciones] ([Id]) ON DELETE CASCADE;
ALTER TABLE [db_NEXO].[tbl_cotizacion_conexos] ADD CONSTRAINT [FK_cotizacion_conexos_servicios_conexos_ServicioConexoId] FOREIGN KEY ([ServicioConexoId]) REFERENCES [db_NEXO].[tbl_servicios_conexos] ([Id]);
ALTER TABLE [db_NEXO].[tbl_cotizacion_lineas] ADD CONSTRAINT [FK_cotizacion_lineas_cotizaciones_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [db_NEXO].[tbl_cotizaciones] ([Id]) ON DELETE CASCADE;
ALTER TABLE [db_NEXO].[tbl_cotizacion_lineas] ADD CONSTRAINT [FK_cotizacion_lineas_lineas_solicitud_LineaSolicitudId] FOREIGN KEY ([LineaSolicitudId]) REFERENCES [db_NEXO].[tbl_lineas_solicitud] ([Id]);
ALTER TABLE [db_NEXO].[tbl_cotizaciones] ADD CONSTRAINT [FK_cotizaciones_solicitudes_SolicitudId] FOREIGN KEY ([SolicitudId]) REFERENCES [db_NEXO].[tbl_solicitudes] ([Id]);
ALTER TABLE [db_NEXO].[tbl_cotizaciones] ADD CONSTRAINT [FK_cotizaciones_usuarios_EntregadaPor] FOREIGN KEY ([EntregadaPor]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_items_plan_cargue] ADD CONSTRAINT [FK_items_plan_cargue_planes_cargue_PlanCargueId] FOREIGN KEY ([PlanCargueId]) REFERENCES [db_NEXO].[tbl_planes_cargue] ([Id]) ON DELETE CASCADE;
ALTER TABLE [db_NEXO].[tbl_lineas_solicitud] ADD CONSTRAINT [FK_lineas_solicitud_configuraciones_vehiculo_ConfigVehiculoId] FOREIGN KEY ([ConfigVehiculoId]) REFERENCES [db_NEXO].[tbl_configuraciones_vehiculo] ([Id]);
ALTER TABLE [db_NEXO].[tbl_lineas_solicitud] ADD CONSTRAINT [FK_lineas_solicitud_rutas_RutaId] FOREIGN KEY ([RutaId]) REFERENCES [db_NEXO].[tbl_rutas] ([Id]);
ALTER TABLE [db_NEXO].[tbl_lineas_solicitud] ADD CONSTRAINT [FK_lineas_solicitud_solicitudes_SolicitudId] FOREIGN KEY ([SolicitudId]) REFERENCES [db_NEXO].[tbl_solicitudes] ([Id]) ON DELETE CASCADE;
ALTER TABLE [db_NEXO].[OutboxMessage] ADD CONSTRAINT [FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId] FOREIGN KEY ([InboxMessageId], [InboxConsumerId]) REFERENCES [db_NEXO].[InboxState] ([MessageId], [ConsumerId]);
ALTER TABLE [db_NEXO].[OutboxMessage] ADD CONSTRAINT [FK_OutboxMessage_OutboxState_OutboxId] FOREIGN KEY ([OutboxId]) REFERENCES [db_NEXO].[OutboxState] ([OutboxId]);
ALTER TABLE [db_NEXO].[tbl_planes_cargue] ADD CONSTRAINT [FK_planes_cargue_vehiculos_cubicacion_VehiculoId] FOREIGN KEY ([VehiculoId]) REFERENCES [db_NEXO].[tbl_vehiculos_cubicacion] ([Id]);
ALTER TABLE [db_NEXO].[tbl_servicios_conexos] ADD CONSTRAINT [FK_servicios_conexos_usuarios_ActualizadoPor] FOREIGN KEY ([ActualizadoPor]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_solicitudes] ADD CONSTRAINT [FK_solicitudes_clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [db_NEXO].[tbl_clientes] ([Id]);
ALTER TABLE [db_NEXO].[tbl_solicitudes] ADD CONSTRAINT [FK_solicitudes_usuarios_CreadoPor] FOREIGN KEY ([CreadoPor]) REFERENCES [db_NEXO].[tbl_usuarios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] ADD CONSTRAINT [FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId] FOREIGN KEY ([ConfigVehiculoId]) REFERENCES [db_NEXO].[tbl_configuraciones_vehiculo] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] ADD CONSTRAINT [FK_tarifas_historicas_cotizaciones_CotizacionId] FOREIGN KEY ([CotizacionId]) REFERENCES [db_NEXO].[tbl_cotizaciones] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_historicas] ADD CONSTRAINT [FK_tarifas_historicas_rutas_RutaId] FOREIGN KEY ([RutaId]) REFERENCES [db_NEXO].[tbl_rutas] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_sicetac] ADD CONSTRAINT [FK_tarifas_sicetac_rutas_RutaId] FOREIGN KEY ([RutaId]) REFERENCES [db_NEXO].[tbl_rutas] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_plaza] ADD CONSTRAINT [FK_tarifas_plaza_municipios_OrigenMunicipioId] FOREIGN KEY ([OrigenMunicipioId]) REFERENCES [db_NEXO].[tbl_municipios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_plaza] ADD CONSTRAINT [FK_tarifas_plaza_municipios_DestinoMunicipioId] FOREIGN KEY ([DestinoMunicipioId]) REFERENCES [db_NEXO].[tbl_municipios] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_plaza] ADD CONSTRAINT [FK_tarifas_plaza_pesos_PesoId] FOREIGN KEY ([PesoId]) REFERENCES [db_NEXO].[tbl_pesos] ([Id]);
ALTER TABLE [db_NEXO].[tbl_tarifas_plaza_historico] ADD CONSTRAINT [FK_tarifas_plaza_historico_tarifas_plaza_TarifaPlazaId] FOREIGN KEY ([TarifaPlazaId]) REFERENCES [db_NEXO].[tbl_tarifas_plaza] ([Id]) ON DELETE CASCADE;

-- =============================================================================
-- 3. ÍNDICES
-- =============================================================================
-- Únicos: reglas de negocio (NIT de cliente, ruta origen+destino, email de
-- usuario, etc.). El resto son de rendimiento, sobre las columnas por las que
-- de verdad se filtra (FKs, bandeja por Estado+ScorePrioridad, etc.).

CREATE UNIQUE INDEX [IX_ajustes_vigencia_plaza_TarifaPlazaId] ON [db_NEXO].[tbl_ajustes_vigencia_plaza] ([TarifaPlazaId]);
CREATE INDEX [IX_audit_log_Entidad_EntidadId] ON [db_NEXO].[tbl_audit_log] ([Entidad], [EntidadId]);
CREATE INDEX [IX_audit_log_UsuarioId] ON [db_NEXO].[tbl_audit_log] ([UsuarioId]);
CREATE UNIQUE INDEX [IX_clientes_Nit] ON [db_NEXO].[tbl_clientes] ([Nit]);
CREATE INDEX [IX_colocaciones_cargue_ItemPlanCargueId] ON [db_NEXO].[tbl_colocaciones_cargue] ([ItemPlanCargueId]);
CREATE INDEX [IX_colocaciones_cargue_PlanCargueId_Cargada] ON [db_NEXO].[tbl_colocaciones_cargue] ([PlanCargueId], [Cargada]);
CREATE INDEX [IX_cotizacion_conexos_CotizacionId] ON [db_NEXO].[tbl_cotizacion_conexos] ([CotizacionId]);
CREATE INDEX [IX_cotizacion_conexos_ServicioConexoId] ON [db_NEXO].[tbl_cotizacion_conexos] ([ServicioConexoId]);
CREATE INDEX [IX_cotizacion_lineas_CotizacionId] ON [db_NEXO].[tbl_cotizacion_lineas] ([CotizacionId]);
CREATE INDEX [IX_cotizacion_lineas_LineaSolicitudId] ON [db_NEXO].[tbl_cotizacion_lineas] ([LineaSolicitudId]);
CREATE INDEX [IX_cotizaciones_EntregadaPor] ON [db_NEXO].[tbl_cotizaciones] ([EntregadaPor]);
CREATE INDEX [IX_cotizaciones_SolicitudId] ON [db_NEXO].[tbl_cotizaciones] ([SolicitudId]);
CREATE UNIQUE INDEX [AK_InboxState_MessageId_ConsumerId] ON [db_NEXO].[InboxState] ([MessageId], [ConsumerId]);
CREATE INDEX [IX_InboxState_Delivered] ON [db_NEXO].[InboxState] ([Delivered]);
CREATE INDEX [IX_items_plan_cargue_PlanCargueId] ON [db_NEXO].[tbl_items_plan_cargue] ([PlanCargueId]);
-- Filtrado a las pendientes: indexar las ya resueltas no aporta y la tabla crece.
CREATE INDEX [IX_lineas_pendientes_de_costeo_SolicitudId] ON [db_NEXO].[tbl_lineas_pendientes_de_costeo] ([SolicitudId]) WHERE ([ResueltaEn] IS NULL);
CREATE INDEX [IX_lineas_pendientes_de_costeo_TrabajoId_LineaSolicitudId_Tipo] ON [db_NEXO].[tbl_lineas_pendientes_de_costeo] ([TrabajoId], [LineaSolicitudId], [Tipo]);
CREATE INDEX [IX_lineas_solicitud_ConfigVehiculoId] ON [db_NEXO].[tbl_lineas_solicitud] ([ConfigVehiculoId]);
CREATE INDEX [IX_lineas_solicitud_RutaId] ON [db_NEXO].[tbl_lineas_solicitud] ([RutaId]);
CREATE INDEX [IX_lineas_solicitud_SolicitudId] ON [db_NEXO].[tbl_lineas_solicitud] ([SolicitudId]);
CREATE UNIQUE INDEX [IX_mapeo_rol_portal_IdRolPortal] ON [db_NEXO].[tbl_mapeo_rol_portal] ([IdRolPortal]) WHERE ([IdRolPortal] IS NOT NULL);
-- Llave natural real (el código DANE solo no es único, choca entre Venezuela y Ecuador).
CREATE UNIQUE INDEX [IX_municipios_CodigoDane_Pais] ON [db_NEXO].[tbl_municipios] ([CodigoDane], [Pais]);
CREATE INDEX [IX_municipios_NombreTarifasPlaza] ON [db_NEXO].[tbl_municipios] ([NombreTarifasPlaza]);
CREATE INDEX [IX_OutboxMessage_EnqueueTime] ON [db_NEXO].[OutboxMessage] ([EnqueueTime]);
CREATE INDEX [IX_OutboxMessage_ExpirationTime] ON [db_NEXO].[OutboxMessage] ([ExpirationTime]);
CREATE UNIQUE INDEX [IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber] ON [db_NEXO].[OutboxMessage] ([InboxMessageId], [InboxConsumerId], [SequenceNumber]) WHERE ([InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL);
CREATE UNIQUE INDEX [IX_OutboxMessage_OutboxId_SequenceNumber] ON [db_NEXO].[OutboxMessage] ([OutboxId], [SequenceNumber]) WHERE ([OutboxId] IS NOT NULL);
CREATE INDEX [IX_OutboxState_Created] ON [db_NEXO].[OutboxState] ([Created]);
CREATE INDEX [IX_planes_cargue_LineaSolicitudId] ON [db_NEXO].[tbl_planes_cargue] ([LineaSolicitudId]);
CREATE INDEX [IX_planes_cargue_VehiculoId] ON [db_NEXO].[tbl_planes_cargue] ([VehiculoId]);
CREATE UNIQUE INDEX [IX_reglas_solo_sicetac_Origen_Destino_TipoVehiculo] ON [db_NEXO].[tbl_reglas_solo_sicetac] ([Origen], [Destino], [TipoVehiculo]);
CREATE UNIQUE INDEX [IX_rutas_Origen_Destino] ON [db_NEXO].[tbl_rutas] ([Origen], [Destino]);
CREATE INDEX [IX_servicios_conexos_ActualizadoPor] ON [db_NEXO].[tbl_servicios_conexos] ([ActualizadoPor]);
CREATE INDEX [IX_solicitudes_ClienteId] ON [db_NEXO].[tbl_solicitudes] ([ClienteId]);
CREATE INDEX [IX_solicitudes_CreadoPor] ON [db_NEXO].[tbl_solicitudes] ([CreadoPor]);
CREATE INDEX [IX_solicitudes_Deadline] ON [db_NEXO].[tbl_solicitudes] ([Deadline]);
-- La consulta real de la bandeja del CoordinadorFletes: filtra por estado y ordena por score.
CREATE INDEX [IX_solicitudes_Estado_ScorePrioridad] ON [db_NEXO].[tbl_solicitudes] ([Estado], [ScorePrioridad]);
CREATE INDEX [IX_tarifas_historicas_ConfigVehiculoId] ON [db_NEXO].[tbl_tarifas_historicas] ([ConfigVehiculoId]);
CREATE INDEX [IX_tarifas_historicas_CotizacionId] ON [db_NEXO].[tbl_tarifas_historicas] ([CotizacionId]);
CREATE INDEX [IX_tarifas_historicas_RutaId_ConfigVehiculoId_Tipologia] ON [db_NEXO].[tbl_tarifas_historicas] ([RutaId], [ConfigVehiculoId], [Tipologia]);
CREATE UNIQUE INDEX [IX_tarifas_sicetac_Periodo_OrigenDane_DestinoDane_Configuracion_TipoCarga_UnidadTransporte_CondicionCarga] ON [db_NEXO].[tbl_tarifas_sicetac] ([Periodo], [OrigenDane], [DestinoDane], [Configuracion], [TipoCarga], [UnidadTransporte], [CondicionCarga]);
CREATE INDEX [IX_tarifas_sicetac_RutaId] ON [db_NEXO].[tbl_tarifas_sicetac] ([RutaId]);
CREATE INDEX [IX_tarifas_plaza_OrigenMunicipioId] ON [db_NEXO].[tbl_tarifas_plaza] ([OrigenMunicipioId]);
CREATE INDEX [IX_tarifas_plaza_DestinoMunicipioId] ON [db_NEXO].[tbl_tarifas_plaza] ([DestinoMunicipioId]);
CREATE INDEX [IX_tarifas_plaza_PesoId] ON [db_NEXO].[tbl_tarifas_plaza] ([PesoId]);
CREATE INDEX [IX_tarifas_plaza_historico_TarifaPlazaId] ON [db_NEXO].[tbl_tarifas_plaza_historico] ([TarifaPlazaId]);
CREATE UNIQUE INDEX [IX_usuarios_Email] ON [db_NEXO].[tbl_usuarios] ([Email]);
CREATE UNIQUE INDEX [IX_usuarios_IdentidadPortalId] ON [db_NEXO].[tbl_usuarios] ([IdentidadPortalId]) WHERE ([IdentidadPortalId] IS NOT NULL);

-- =============================================================================
-- 4. TRIGGERS
-- =============================================================================

GO

-- Cada vez que se actualiza el Valor, la Vigencia o los DetallesTarifa de una
-- fila de tbl_tarifas_plaza, archiva el valor ANTERIOR completo (todas sus
-- dimensiones) en tbl_tarifas_plaza_historico antes de perderlo. No dispara en un
-- UPDATE que no toque esas 3 columnas (ej. si algo más las vuelve a guardar
-- igual). Soporta actualizar varias filas en una sola sentencia (opera sobre
-- toda la pseudo-tabla "deleted", no fila por fila).
CREATE TRIGGER [db_NEXO].[TRG_tarifas_plaza_historico]
ON [db_NEXO].[tbl_tarifas_plaza]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT (UPDATE(Valor) OR UPDATE(Vigencia) OR UPDATE(DetallesTarifa))
        RETURN;

    INSERT INTO [db_NEXO].[tbl_tarifas_plaza_historico] (
        [Id], [TarifaPlazaId], [LineaNegocio], [ClaseViaje], [OrigenMunicipioId],
        [DestinoMunicipioId], [ClaseContenedor], [PesoId], [TipoVehiculo], [Valor],
        [Vigencia], [DetallesTarifa], [CreadoPor], [CreatedAt], [ReemplazadoAt]
    )
    SELECT
        NEWID(), d.[Id], d.[LineaNegocio], d.[ClaseViaje], d.[OrigenMunicipioId],
        d.[DestinoMunicipioId], d.[ClaseContenedor], d.[PesoId], d.[TipoVehiculo], d.[Valor],
        d.[Vigencia], d.[DetallesTarifa], d.[CreadoPor], d.[CreatedAt], sysdatetimeoffset()
    FROM deleted d;
END;

GO
