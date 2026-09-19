using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Contador que solo crece, uno por trabajo, para que el navegador pueda descartar
    /// los avisos rezagados. Arranca en cero para las sagas que ya existan: la primera
    /// vez que cambien empezarán a contar y los clientes se sincronizan solos.
    /// </summary>
    public partial class AvisoConSecuencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Secuencia",
                table: "trabajos_auto_costeo",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Secuencia",
                table: "trabajos_auto_costeo");
        }
    }
}
