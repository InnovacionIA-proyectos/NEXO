using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReglasSoloSicetac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reglas_solo_sicetac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TipoVehiculo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reglas_solo_sicetac", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reglas_solo_sicetac_Origen_Destino_TipoVehiculo",
                table: "reglas_solo_sicetac",
                columns: new[] { "Origen", "Destino", "TipoVehiculo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reglas_solo_sicetac");
        }
    }
}
