using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SagaAutoCosteo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trabajos_auto_costeo",
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
                    TokenExpiracion = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabajos_auto_costeo", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabajos_auto_costeo");
        }
    }
}
