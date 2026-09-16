using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMotivoPendienteALinea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoPendiente",
                schema: "dbo_NEXO_prueba",
                table: "lineas_solicitud",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoPendiente",
                schema: "dbo_NEXO_prueba",
                table: "lineas_solicitud");
        }
    }
}
