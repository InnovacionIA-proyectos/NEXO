using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LevanteContenedorAprovisionamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CiudadLevanteContenedor",
                table: "lineas_solicitud",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAprovisionamiento",
                table: "lineas_solicitud",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CiudadLevanteContenedor",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "ValorAprovisionamiento",
                table: "lineas_solicitud");
        }
    }
}
