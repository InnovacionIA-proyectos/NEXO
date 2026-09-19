using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjustesVigenciaPlaza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ajustes_vigencia_plaza",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TarifaPlazaId = table.Column<int>(type: "int", nullable: false),
                    VigenciaAjustada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenciaOriginal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AjustadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ajustes_vigencia_plaza", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ajustes_vigencia_plaza_TarifaPlazaId",
                table: "ajustes_vigencia_plaza",
                column: "TarifaPlazaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ajustes_vigencia_plaza");
        }
    }
}
