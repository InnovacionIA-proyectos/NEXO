using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// La tabla de la cadena de recálculo del score.
    ///
    /// Una sola fila, siempre: el CorrelationId es una constante y la clave primaria hace
    /// el resto. Eso es lo que garantiza que no existan dos relojes, cosa que con un
    /// PeriodicTimer por proceso era imposible de asegurar en cuanto hubiera dos réplicas
    /// del API.
    ///
    /// Escrita a mano porque el instalador de dotnet-ef no pudo alcanzar NuGet desde el
    /// contenedor. EF la valida igual: si la instantánea del modelo no coincidiera con el
    /// modelo real, MigrateAsync fallaría con PendingModelChangesWarning.
    /// </summary>
    public partial class CadenaDePriorizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cadena_priorizacion",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenTic = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArmadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RearmadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UltimoRecalculoEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecalculosPorReloj = table.Column<int>(type: "int", nullable: false),
                    RecalculosPorParametros = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cadena_priorizacion", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cadena_priorizacion");
        }
    }
}
