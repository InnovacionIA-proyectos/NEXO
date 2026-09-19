using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class clsLineasEsperandoTarifa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lineas_esperando_tarifa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_esperando_tarifa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lineas_esperando_tarifa_SolicitudId",
                table: "lineas_esperando_tarifa",
                column: "SolicitudId",
                filter: "[ResueltaEn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_esperando_tarifa_TrabajoId_LineaSolicitudId",
                table: "lineas_esperando_tarifa",
                columns: new[] { "TrabajoId", "LineaSolicitudId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_esperando_tarifa");
        }
    }
}
