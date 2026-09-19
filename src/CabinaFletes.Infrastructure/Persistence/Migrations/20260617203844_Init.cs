using System;
using CabinaFletes.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Entidad = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EntidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Diff = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telefonos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "configuraciones_vehiculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoVehiculo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Carroceria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RangoPeso = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuraciones_vehiculo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cotizaciones",
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
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotizaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parametros_score",
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
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametros_score", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rutas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rutas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "servicios_conexos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReglaCobro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorBase = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ActualizadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicios_conexos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tarifas_historicas",
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
                    table.PrimaryKey("PK_tarifas_historicas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tarifas_sicetac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiceId = table.Column<long>(type: "bigint", nullable: true),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrigenDane = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DestinoDane = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreRuta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Configuracion = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoCarga = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnidadTransporte = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    table.PrimaryKey("PK_tarifas_sicetac", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AdObjectId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes",
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
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_solicitudes_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotizacion_conexos",
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
                    table.PrimaryKey("PK_cotizacion_conexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cotizacion_conexos_cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotizacion_lineas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorFlete = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Fuente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotizacion_lineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cotizacion_lineas_cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lineas_solicitud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigVehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipologia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Peso = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Dimensiones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorMercancia = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EstadoLinea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PorCostear"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_solicitud", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lineas_solicitud_solicitudes_SolicitudId",
                        column: x => x.SolicitudId,
                        principalTable: "solicitudes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "parametros_score",
                columns: new[] { "Id", "CreatedAt", "HorizonteHoras", "PesoProbabilidad", "PesoTipo", "PesoUrgencia", "PesoValor", "ProbabilidadPorDefecto", "PuntajeLicitacion", "PuntajeSpot", "UpdatedAt", "ValorPorDefectoSinValor", "ValorReferencia" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 240.0, 0.20000000000000001, 0.10000000000000001, 0.40000000000000002, 0.29999999999999999, 50.0, 100.0, 50.0, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30.0, 100000000m });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_Entidad_EntidadId",
                table: "audit_log",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_clientes_Nit",
                table: "clientes",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_conexos_CotizacionId",
                table: "cotizacion_conexos",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_lineas_CotizacionId",
                table: "cotizacion_lineas",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_solicitud_SolicitudId",
                table: "lineas_solicitud",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_rutas_Origen_Destino",
                table: "rutas",
                columns: new[] { "Origen", "Destino" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_ClienteId",
                table: "solicitudes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_Deadline",
                table: "solicitudes",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_Estado_ScorePrioridad",
                table: "solicitudes",
                columns: new[] { "Estado", "ScorePrioridad" });

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_historicas_RutaId_ConfigVehiculoId_Tipologia",
                table: "tarifas_historicas",
                columns: new[] { "RutaId", "ConfigVehiculoId", "Tipologia" });

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_sicetac_Periodo_OrigenDane_DestinoDane_Configuracion_TipoCarga_UnidadTransporte_CondicionCarga",
                table: "tarifas_sicetac",
                columns: new[] { "Periodo", "OrigenDane", "DestinoDane", "Configuracion", "TipoCarga", "UnidadTransporte", "CondicionCarga" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_AdObjectId",
                table: "usuarios",
                column: "AdObjectId",
                unique: true,
                filter: "[AdObjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "configuraciones_vehiculo");

            migrationBuilder.DropTable(
                name: "cotizacion_conexos");

            migrationBuilder.DropTable(
                name: "cotizacion_lineas");

            migrationBuilder.DropTable(
                name: "lineas_solicitud");

            migrationBuilder.DropTable(
                name: "parametros_score");

            migrationBuilder.DropTable(
                name: "rutas");

            migrationBuilder.DropTable(
                name: "servicios_conexos");

            migrationBuilder.DropTable(
                name: "tarifas_historicas");

            migrationBuilder.DropTable(
                name: "tarifas_sicetac");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "cotizaciones");

            migrationBuilder.DropTable(
                name: "solicitudes");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
