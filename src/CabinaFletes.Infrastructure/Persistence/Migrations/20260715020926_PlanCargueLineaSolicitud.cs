using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanCargueLineaSolicitud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LineaSolicitudId",
                table: "planes_cargue",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_planes_cargue_LineaSolicitudId",
                table: "planes_cargue",
                column: "LineaSolicitudId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_planes_cargue_LineaSolicitudId",
                table: "planes_cargue");

            migrationBuilder.DropColumn(
                name: "LineaSolicitudId",
                table: "planes_cargue");
        }
    }
}
