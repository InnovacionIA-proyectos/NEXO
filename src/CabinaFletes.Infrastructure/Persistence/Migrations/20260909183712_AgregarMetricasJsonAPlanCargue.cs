using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMetricasJsonAPlanCargue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetricasJson",
                schema: "dbo_NEXO_prueba",
                table: "planes_cargue",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetricasJson",
                schema: "dbo_NEXO_prueba",
                table: "planes_cargue");
        }
    }
}
