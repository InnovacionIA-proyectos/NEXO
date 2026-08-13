using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CotizacionLineaExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorAprovisionamiento",
                table: "cotizacion_lineas",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRetornoContenedor",
                table: "cotizacion_lineas",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorAprovisionamiento",
                table: "cotizacion_lineas");

            migrationBuilder.DropColumn(
                name: "ValorRetornoContenedor",
                table: "cotizacion_lineas");
        }
    }
}
