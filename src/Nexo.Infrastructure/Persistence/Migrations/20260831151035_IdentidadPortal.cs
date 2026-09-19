using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class clsIdentidadPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_AdObjectId",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "AdObjectId",
                table: "usuarios");

            migrationBuilder.AddColumn<long>(
                name: "IdentidadPortalId",
                table: "usuarios",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mapeo_rol_portal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdRolPortal = table.Column<long>(type: "bigint", nullable: true),
                    DescripcionRolPortal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mapeo_rol_portal", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "mapeo_rol_portal",
                columns: new[] { "Id", "CreatedAt", "DescripcionRolPortal", "IdRolPortal", "Rol" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TarifasComercial", null, "Comercial" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TarifasAdministrador", null, "AdminTarifas" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administracion", null, "Admin" }
                });

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "IdentidadPortalId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_IdentidadPortalId",
                table: "usuarios",
                column: "IdentidadPortalId",
                unique: true,
                filter: "[IdentidadPortalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_mapeo_rol_portal_IdRolPortal",
                table: "mapeo_rol_portal",
                column: "IdRolPortal",
                unique: true,
                filter: "[IdRolPortal] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mapeo_rol_portal");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_IdentidadPortalId",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "IdentidadPortalId",
                table: "usuarios");

            migrationBuilder.AddColumn<string>(
                name: "AdObjectId",
                table: "usuarios",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "AdObjectId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_AdObjectId",
                table: "usuarios",
                column: "AdObjectId",
                unique: true,
                filter: "[AdObjectId] IS NOT NULL");
        }
    }
}
