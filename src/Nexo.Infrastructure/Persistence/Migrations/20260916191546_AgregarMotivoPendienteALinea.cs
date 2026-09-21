using Microsoft.EntityFrameworkCore.Migrations;
using Nexo.Infrastructure.Persistence;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class clsAgregarMotivoPendienteALinea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoPendiente",
                schema: clsEsquemaNexo.Nombre,
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
                schema: clsEsquemaNexo.Nombre,
                table: "lineas_solicitud");
        }
    }
}
