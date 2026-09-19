using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CamposConsultaTarifaLinea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaseContenedor",
                table: "lineas_solicitud",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaseViaje",
                table: "lineas_solicitud",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Exclusivo",
                table: "lineas_solicitud",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LevanteContenedorDiferente",
                table: "lineas_solicitud",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LineaNegocio",
                table: "lineas_solicitud",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaseContenedor",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "ClaseViaje",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "Exclusivo",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "LevanteContenedorDiferente",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "LineaNegocio",
                table: "lineas_solicitud");
        }
    }
}
