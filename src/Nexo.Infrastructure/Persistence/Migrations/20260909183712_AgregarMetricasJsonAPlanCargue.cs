using Microsoft.EntityFrameworkCore.Migrations;
using Nexo.Infrastructure.Persistence;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class clsAgregarMetricasJsonAPlanCargue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetricasJson",
                schema: clsEsquemaNexo.Nombre,
                table: "planes_cargue",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetricasJson",
                schema: clsEsquemaNexo.Nombre,
                table: "planes_cargue");
        }
    }
}
