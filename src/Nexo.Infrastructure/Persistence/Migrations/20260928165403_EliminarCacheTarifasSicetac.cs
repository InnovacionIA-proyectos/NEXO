using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EliminarCacheTarifasSicetac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_tarifas_sicetac",
                schema: "db_NEXO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_tarifas_sicetac",
                schema: "db_NEXO",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RutaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CondicionCarga = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Configuracion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    DestinoDane = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    FechaCreacionSice = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FechaModificacionSice = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HorasPactadas = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    HorasRecorrido = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    ImportadoAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NombreRuta = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OrigenDane = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Periodo = table.Column<int>(type: "int", nullable: false),
                    SiceId = table.Column<long>(type: "bigint", nullable: true),
                    TipoCarga = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UnidadTransporte = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    ValorHora = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValorPiso = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorSiceHora = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ViaEstandar = table.Column<bool>(type: "bit", nullable: true)
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
        }
    }
}
