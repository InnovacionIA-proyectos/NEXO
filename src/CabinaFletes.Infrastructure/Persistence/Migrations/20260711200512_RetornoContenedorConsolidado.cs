using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetornoContenedorConsolidado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CiudadDevolucionContenedor",
                table: "lineas_solicitud",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Consolidado",
                table: "lineas_solicitud",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RetornaContenedor",
                table: "lineas_solicitud",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CiudadDevolucionContenedor",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "Consolidado",
                table: "lineas_solicitud");

            migrationBuilder.DropColumn(
                name: "RetornaContenedor",
                table: "lineas_solicitud");
        }
    }
}
