using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRolesPortalNexo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                columns: new[] { "Id", "CreatedAt", "DescripcionRolPortal", "IdRolPortal", "Rol" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ComercialNEXO", 48L, "Comercial" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "CoordinadorFletesNEXO", 49L, "CoordinadorFletes" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AdminTarifasNEXO", 50L, "AdminTarifas" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ServiciosLogisticosNEXO", 51L, "ServiciosLogisticos" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "LiderProcesoNEXO", 52L, "LiderProceso" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AdminNEXO", 53L, "Admin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"));

            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000005"));

            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000006"));

            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000007"));

            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000008"));

            migrationBuilder.DeleteData(
                schema: "dbo_NEXO_prueba",
                table: "mapeo_rol_portal",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000009"));
        }
    }
}
