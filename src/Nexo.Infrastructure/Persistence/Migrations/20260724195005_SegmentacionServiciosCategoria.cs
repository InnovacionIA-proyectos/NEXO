using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class clsSegmentacionServiciosCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nueva columna de segmento; los existentes quedan en 'logistico' por defecto.
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "servicios_conexos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "logistico");

            // Relleno: los servicios cuya categoría vivía embebida como JSON en ReglaCobro
            // ({"categoria":"adicional"}) pasan al campo propio.
            migrationBuilder.Sql(
                @"UPDATE servicios_conexos SET Categoria = 'adicional'
                  WHERE ReglaCobro LIKE '%""categoria""%""adicional""%';");

            // Limpieza: los marcadores que SOLO llevaban la categoría (creados por la carga
            // Excel anterior) dejan libre ReglaCobro para fórmulas de cobro reales.
            migrationBuilder.Sql(
                @"UPDATE servicios_conexos SET ReglaCobro = NULL
                  WHERE ReglaCobro LIKE '{""categoria""%}' AND ReglaCobro NOT LIKE '%""tipo""%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "servicios_conexos");
        }
    }
}
