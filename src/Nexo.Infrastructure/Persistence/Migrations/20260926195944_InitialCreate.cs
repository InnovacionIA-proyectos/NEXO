using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "db_NEXO");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "db_NEXO",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ajustes_vigencia_plaza",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TarifaPlazaId = table.Column<int>(type: "int", nullable: false),
                    VigenciaAjustada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenciaOriginal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AjustadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ajustes_vigencia_plaza", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_cadena_priorizacion",
                schema: "db_NEXO",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenTic = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArmadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RearmadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UltimoRecalculoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecalculosPorReloj = table.Column<int>(type: "int", nullable: false),
                    RecalculosPorParametros = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cadena_priorizacion", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_clientes",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefonos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_configuraciones_vehiculo",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoVehiculo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Carroceria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RangoPeso = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_configuraciones_vehiculo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_lineas_pendientes_de_costeo",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_lineas_pendientes_de_costeo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_mapeo_rol_portal",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdRolPortal = table.Column<long>(type: "bigint", nullable: true),
                    DescripcionRolPortal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_mapeo_rol_portal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_municipios",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoDane = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Departamento = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NombreTarifasPlaza = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ZonaTarificacion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_municipios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_parametros_score",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PesoUrgencia = table.Column<double>(type: "float", nullable: false),
                    PesoValor = table.Column<double>(type: "float", nullable: false),
                    PesoProbabilidad = table.Column<double>(type: "float", nullable: false),
                    PesoTipo = table.Column<double>(type: "float", nullable: false),
                    HorizonteHoras = table.Column<double>(type: "float", nullable: false),
                    ValorReferencia = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PuntajeLicitacion = table.Column<double>(type: "float", nullable: false),
                    PuntajeSpot = table.Column<double>(type: "float", nullable: false),
                    ValorPorDefectoSinValor = table.Column<double>(type: "float", nullable: false),
                    ProbabilidadPorDefecto = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_parametros_score", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_reglas_solo_sicetac",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TipoVehiculo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_reglas_solo_sicetac", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_rutas",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_rutas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_trabajos_auto_costeo",
                schema: "db_NEXO",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TerminadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalLineas = table.Column<int>(type: "int", nullable: true),
                    Resueltas = table.Column<int>(type: "int", nullable: false),
                    PendientesTecnicas = table.Column<int>(type: "int", nullable: false),
                    RequierenManual = table.Column<int>(type: "int", nullable: false),
                    Secuencia = table.Column<int>(type: "int", nullable: false),
                    TokenExpiracion = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_trabajos_auto_costeo", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_usuarios",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IdentidadPortalId = table.Column<long>(type: "bigint", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_vehiculos_cubicacion",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LargoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoInternoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CapacidadKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TaraKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaAnchoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaAltoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PuertaPosicion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EjesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToleranciaParedesMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ToleranciaEntreUnidadesMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    EsPreset = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_vehiculos_cubicacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "db_NEXO",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "db_NEXO",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "db_NEXO",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "tbl_tarifas_sicetac",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiceId = table.Column<long>(type: "bigint", nullable: true),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigenDane = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    DestinoDane = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    NombreRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Configuracion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoCarga = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UnidadTransporte = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CondicionCarga = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValorPiso = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorHora = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HorasPactadas = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    HorasRecorrido = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    ValorSiceHora = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ViaEstandar = table.Column<bool>(type: "bit", nullable: true),
                    FechaCreacionSice = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FechaModificacionSice = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImportadoAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_tarifas_sicetac", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_tarifas_sicetac_tbl_rutas_RutaId",
                        column: x => x.RutaId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_audit_log",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Entidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Diff = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_audit_log_tbl_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_servicios_conexos",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReglaCobro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorBase = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ActualizadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_servicios_conexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_servicios_conexos_tbl_usuarios_ActualizadoPor",
                        column: x => x.ActualizadoPor,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_solicitudes",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Modalidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CondicionesServicio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Deadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProbabilidadCierre = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    ValorEstimado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ScorePrioridad = table.Column<decimal>(type: "decimal(6,2)", nullable: false, defaultValue: 0m),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_solicitudes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_solicitudes_tbl_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_solicitudes_tbl_usuarios_CreadoPor",
                        column: x => x.CreadoPor,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_planes_cargue",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeroVehiculo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estrategia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PonderacionesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Algoritmo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Semilla = table.Column<int>(type: "int", nullable: true),
                    PesoTotalKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    VolumenUtilizadoM3 = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    UtilizacionVolumen = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    UtilizacionPiso = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Puntaje = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    IncidenciasJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_planes_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_planes_cargue_tbl_vehiculos_cubicacion_VehiculoId",
                        column: x => x.VehiculoId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_vehiculos_cubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_cotizaciones",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Borrador"),
                    Resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "SinDefinir"),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    EntregadaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntregadaAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cotizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_cotizaciones_tbl_solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_cotizaciones_tbl_usuarios_EntregadaPor",
                        column: x => x.EntregadaPor,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_lineas_solicitud",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigVehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipologia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LineaNegocio = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ClaseViaje = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClaseContenedor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Exclusivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Consolidado = table.Column<bool>(type: "bit", nullable: false),
                    RetornaContenedor = table.Column<bool>(type: "bit", nullable: false),
                    CiudadDevolucionContenedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LevanteContenedorDiferente = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CiudadLevanteContenedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ValorAprovisionamiento = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Peso = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Dimensiones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorMercancia = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstadoLinea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PorCostear"),
                    MotivoPendiente = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_lineas_solicitud", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_lineas_solicitud_tbl_configuraciones_vehiculo_ConfigVehiculoId",
                        column: x => x.ConfigVehiculoId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_configuraciones_vehiculo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_lineas_solicitud_tbl_rutas_RutaId",
                        column: x => x.RutaId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_lineas_solicitud_tbl_solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_items_plan_cargue",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Pedido = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Destino = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SecuenciaEntrega = table.Column<int>(type: "int", nullable: false),
                    LargoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PesoKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    TipoEmbalaje = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Apilable = table.Column<bool>(type: "bit", nullable: false),
                    MaxNiveles = table.Column<int>(type: "int", nullable: false),
                    MaxPesoEncimaKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Rotable = table.Column<bool>(type: "bit", nullable: false),
                    MantenerVertical = table.Column<bool>(type: "bit", nullable: false),
                    Fragil = table.Column<bool>(type: "bit", nullable: false),
                    Peligroso = table.Column<bool>(type: "bit", nullable: false),
                    Refrigerado = table.Column<bool>(type: "bit", nullable: false),
                    GrupoCompatibilidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_items_plan_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_items_plan_cargue_tbl_planes_cargue_PlanCargueId",
                        column: x => x.PlanCargueId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_planes_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_cotizacion_conexos",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServicioConexoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ValorAplicado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cotizacion_conexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_cotizacion_conexos_tbl_cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_cotizacion_conexos_tbl_servicios_conexos_ServicioConexoId",
                        column: x => x.ServicioConexoId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_servicios_conexos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_tarifas_historicas",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigVehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipologia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_tarifas_historicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_tarifas_historicas_tbl_configuraciones_vehiculo_ConfigVehiculoId",
                        column: x => x.ConfigVehiculoId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_configuraciones_vehiculo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_tarifas_historicas_tbl_cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_tarifas_historicas_tbl_rutas_RutaId",
                        column: x => x.RutaId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_cotizacion_lineas",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorFlete = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorAprovisionamiento = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValorRetornoContenedor = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Fuente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cotizacion_lineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_cotizacion_lineas_tbl_cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_cotizacion_lineas_tbl_lineas_solicitud_LineaSolicitudId",
                        column: x => x.LineaSolicitudId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_lineas_solicitud",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_colocaciones_cargue",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemPlanCargueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndiceUnidad = table.Column<int>(type: "int", nullable: false),
                    Cargada = table.Column<bool>(type: "bit", nullable: false),
                    X = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Y = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Z = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    LargoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AnchoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AltoColocadoMm = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    OrientacionIndice = table.Column<int>(type: "int", nullable: false),
                    SecuenciaCargue = table.Column<int>(type: "int", nullable: false),
                    SoporteColocacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PorcentajeSoporte = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    PesoEncimaKg = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    EstadoEstabilidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Accesibilidad = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Bloqueada = table.Column<bool>(type: "bit", nullable: false),
                    AjusteManual = table.Column<bool>(type: "bit", nullable: false),
                    CodigoRechazo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RazonRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_colocaciones_cargue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_colocaciones_cargue_tbl_items_plan_cargue_ItemPlanCargueId",
                        column: x => x.ItemPlanCargueId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_items_plan_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_colocaciones_cargue_tbl_planes_cargue_PlanCargueId",
                        column: x => x.PlanCargueId,
                        principalSchema: "db_NEXO",
                        principalTable: "tbl_planes_cargue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "db_NEXO",
                table: "tbl_mapeo_rol_portal",
                columns: new[] { "Id", "CreatedAt", "DescripcionRolPortal", "IdRolPortal", "Rol" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TarifasComercial", null, "Comercial" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TarifasAdministrador", null, "AdminTarifas" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administracion", null, "Admin" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ComercialNEXO", 48L, "Comercial" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "CoordinadorFletesNEXO", 49L, "CoordinadorFletes" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AdminTarifasNEXO", 50L, "AdminTarifas" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ServiciosLogisticosNEXO", 51L, "ServiciosLogisticos" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "LiderProcesoNEXO", 52L, "LiderProceso" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AdminNEXO", 53L, "Admin" }
                });

            migrationBuilder.InsertData(
                schema: "db_NEXO",
                table: "tbl_parametros_score",
                columns: new[] { "Id", "CreatedAt", "HorizonteHoras", "PesoProbabilidad", "PesoTipo", "PesoUrgencia", "PesoValor", "ProbabilidadPorDefecto", "PuntajeLicitacion", "PuntajeSpot", "UpdatedAt", "ValorPorDefectoSinValor", "ValorReferencia" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 240.0, 0.20000000000000001, 0.10000000000000001, 0.40000000000000002, 0.29999999999999999, 50.0, 100.0, 50.0, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30.0, 100000000m });

            migrationBuilder.InsertData(
                schema: "db_NEXO",
                table: "tbl_usuarios",
                columns: new[] { "Id", "Activo", "CreatedAt", "Email", "IdentidadPortalId", "Nombre", "Rol", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system@boterosoto.com.co", null, "Sistema (placeholder)", "Admin", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "db_NEXO",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "db_NEXO",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "db_NEXO",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "db_NEXO",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "db_NEXO",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "db_NEXO",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ajustes_vigencia_plaza_TarifaPlazaId",
                schema: "db_NEXO",
                table: "tbl_ajustes_vigencia_plaza",
                column: "TarifaPlazaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_audit_log_Entidad_EntidadId",
                schema: "db_NEXO",
                table: "tbl_audit_log",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_audit_log_UsuarioId",
                schema: "db_NEXO",
                table: "tbl_audit_log",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_clientes_Nit",
                schema: "db_NEXO",
                table: "tbl_clientes",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_colocaciones_cargue_ItemPlanCargueId",
                schema: "db_NEXO",
                table: "tbl_colocaciones_cargue",
                column: "ItemPlanCargueId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_colocaciones_cargue_PlanCargueId_Cargada",
                schema: "db_NEXO",
                table: "tbl_colocaciones_cargue",
                columns: new[] { "PlanCargueId", "Cargada" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizacion_conexos_CotizacionId",
                schema: "db_NEXO",
                table: "tbl_cotizacion_conexos",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizacion_conexos_ServicioConexoId",
                schema: "db_NEXO",
                table: "tbl_cotizacion_conexos",
                column: "ServicioConexoId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizacion_lineas_CotizacionId",
                schema: "db_NEXO",
                table: "tbl_cotizacion_lineas",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizacion_lineas_LineaSolicitudId",
                schema: "db_NEXO",
                table: "tbl_cotizacion_lineas",
                column: "LineaSolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizaciones_EntregadaPor",
                schema: "db_NEXO",
                table: "tbl_cotizaciones",
                column: "EntregadaPor");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cotizaciones_SolicitudId",
                schema: "db_NEXO",
                table: "tbl_cotizaciones",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_items_plan_cargue_PlanCargueId",
                schema: "db_NEXO",
                table: "tbl_items_plan_cargue",
                column: "PlanCargueId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_lineas_pendientes_de_costeo_SolicitudId",
                schema: "db_NEXO",
                table: "tbl_lineas_pendientes_de_costeo",
                column: "SolicitudId",
                filter: "[ResueltaEn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_lineas_pendientes_de_costeo_TrabajoId_LineaSolicitudId_Tipo",
                schema: "db_NEXO",
                table: "tbl_lineas_pendientes_de_costeo",
                columns: new[] { "TrabajoId", "LineaSolicitudId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_lineas_solicitud_ConfigVehiculoId",
                schema: "db_NEXO",
                table: "tbl_lineas_solicitud",
                column: "ConfigVehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_lineas_solicitud_RutaId",
                schema: "db_NEXO",
                table: "tbl_lineas_solicitud",
                column: "RutaId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_lineas_solicitud_SolicitudId",
                schema: "db_NEXO",
                table: "tbl_lineas_solicitud",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_mapeo_rol_portal_IdRolPortal",
                schema: "db_NEXO",
                table: "tbl_mapeo_rol_portal",
                column: "IdRolPortal",
                unique: true,
                filter: "[IdRolPortal] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_municipios_CodigoDane_Pais",
                schema: "db_NEXO",
                table: "tbl_municipios",
                columns: new[] { "CodigoDane", "Pais" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_municipios_NombreTarifasPlaza",
                schema: "db_NEXO",
                table: "tbl_municipios",
                column: "NombreTarifasPlaza");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_planes_cargue_LineaSolicitudId",
                schema: "db_NEXO",
                table: "tbl_planes_cargue",
                column: "LineaSolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_planes_cargue_VehiculoId",
                schema: "db_NEXO",
                table: "tbl_planes_cargue",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_reglas_solo_sicetac_Origen_Destino_TipoVehiculo",
                schema: "db_NEXO",
                table: "tbl_reglas_solo_sicetac",
                columns: new[] { "Origen", "Destino", "TipoVehiculo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_rutas_Origen_Destino",
                schema: "db_NEXO",
                table: "tbl_rutas",
                columns: new[] { "Origen", "Destino" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_servicios_conexos_ActualizadoPor",
                schema: "db_NEXO",
                table: "tbl_servicios_conexos",
                column: "ActualizadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_solicitudes_ClienteId",
                schema: "db_NEXO",
                table: "tbl_solicitudes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_solicitudes_CreadoPor",
                schema: "db_NEXO",
                table: "tbl_solicitudes",
                column: "CreadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_solicitudes_Deadline",
                schema: "db_NEXO",
                table: "tbl_solicitudes",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_solicitudes_Estado_ScorePrioridad",
                schema: "db_NEXO",
                table: "tbl_solicitudes",
                columns: new[] { "Estado", "ScorePrioridad" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_tarifas_historicas_ConfigVehiculoId",
                schema: "db_NEXO",
                table: "tbl_tarifas_historicas",
                column: "ConfigVehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_tarifas_historicas_CotizacionId",
                schema: "db_NEXO",
                table: "tbl_tarifas_historicas",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_tarifas_historicas_RutaId_ConfigVehiculoId_Tipologia",
                schema: "db_NEXO",
                table: "tbl_tarifas_historicas",
                columns: new[] { "RutaId", "ConfigVehiculoId", "Tipologia" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_tarifas_sicetac_Periodo_OrigenDane_DestinoDane_Configuracion_TipoCarga_UnidadTransporte_CondicionCarga",
                schema: "db_NEXO",
                table: "tbl_tarifas_sicetac",
                columns: new[] { "Periodo", "OrigenDane", "DestinoDane", "Configuracion", "TipoCarga", "UnidadTransporte", "CondicionCarga" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_tarifas_sicetac_RutaId",
                schema: "db_NEXO",
                table: "tbl_tarifas_sicetac",
                column: "RutaId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_usuarios_Email",
                schema: "db_NEXO",
                table: "tbl_usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_usuarios_IdentidadPortalId",
                schema: "db_NEXO",
                table: "tbl_usuarios",
                column: "IdentidadPortalId",
                unique: true,
                filter: "[IdentidadPortalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_ajustes_vigencia_plaza",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_audit_log",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_cadena_priorizacion",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_colocaciones_cargue",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_cotizacion_conexos",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_cotizacion_lineas",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_lineas_pendientes_de_costeo",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_mapeo_rol_portal",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_municipios",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_parametros_score",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_reglas_solo_sicetac",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_tarifas_historicas",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_tarifas_sicetac",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_trabajos_auto_costeo",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_items_plan_cargue",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_servicios_conexos",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_lineas_solicitud",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_cotizaciones",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_planes_cargue",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_configuraciones_vehiculo",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_rutas",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_solicitudes",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_vehiculos_cubicacion",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_clientes",
                schema: "db_NEXO");

            migrationBuilder.DropTable(
                name: "tbl_usuarios",
                schema: "db_NEXO");
        }
    }
}
