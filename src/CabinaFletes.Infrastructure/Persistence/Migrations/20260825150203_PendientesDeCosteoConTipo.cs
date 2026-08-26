using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendientesDeCosteoConTipo : Migration
    {
        /// <inheritdoc />
        // EF generó BORRAR Y RECREAR en vez de renombrar, y avisó de posible pérdida de
        // datos. Aquí es inocuo: lineas_esperando_tarifa se creó en la issue #15, en esta
        // misma pila de ramas sin mergear, y nunca se desplegó — no hay una sola fila que
        // perder.
        //
        // Si esta migración se estuviera aplicando sobre un ambiente con datos, habría que
        // reescribirla como RenameTable + AddColumn con un valor por defecto. Se deja
        // anotado porque el aviso de EF va a volver a aparecer y conviene saber por qué se
        // aceptó esta vez.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_esperando_tarifa");

            migrationBuilder.CreateTable(
                name: "lineas_pendientes_de_costeo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_pendientes_de_costeo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lineas_pendientes_de_costeo_SolicitudId",
                table: "lineas_pendientes_de_costeo",
                column: "SolicitudId",
                filter: "[ResueltaEn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_pendientes_de_costeo_TrabajoId_LineaSolicitudId_Tipo",
                table: "lineas_pendientes_de_costeo",
                columns: new[] { "TrabajoId", "LineaSolicitudId", "Tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lineas_pendientes_de_costeo");

            migrationBuilder.CreateTable(
                name: "lineas_esperando_tarifa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    LineaSolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabajoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lineas_esperando_tarifa", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lineas_esperando_tarifa_SolicitudId",
                table: "lineas_esperando_tarifa",
                column: "SolicitudId",
                filter: "[ResueltaEn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_esperando_tarifa_TrabajoId_LineaSolicitudId",
                table: "lineas_esperando_tarifa",
                columns: new[] { "TrabajoId", "LineaSolicitudId" });
        }
    }
}
