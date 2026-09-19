using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class clsRelacionesYValidaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "usuarios",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "usuarios",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "AdObjectId",
                table: "usuarios",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnidadTransporte",
                table: "tarifas_sicetac",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "TipoCarga",
                table: "tarifas_sicetac",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "OrigenDane",
                table: "tarifas_sicetac",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "NombreRuta",
                table: "tarifas_sicetac",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DestinoDane",
                table: "tarifas_sicetac",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Configuracion",
                table: "tarifas_sicetac",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "servicios_conexos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "rutas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Destino",
                table: "rutas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "TipoVehiculo",
                table: "configuraciones_vehiculo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RangoPeso",
                table: "configuraciones_vehiculo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Carroceria",
                table: "configuraciones_vehiculo",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RazonSocial",
                table: "clientes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "clientes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Nit",
                table: "clientes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Entidad",
                table: "audit_log",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Accion",
                table: "audit_log",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "usuarios",
                columns: new[] { "Id", "Activo", "AdObjectId", "CreatedAt", "Email", "Nombre", "Rol", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), true, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system@boterosoto.com.co", "Sistema (placeholder)", "Admin", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_sicetac_RutaId",
                table: "tarifas_sicetac",
                column: "RutaId");

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_historicas_ConfigVehiculoId",
                table: "tarifas_historicas",
                column: "ConfigVehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_tarifas_historicas_CotizacionId",
                table: "tarifas_historicas",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_CreadoPor",
                table: "solicitudes",
                column: "CreadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_servicios_conexos_ActualizadoPor",
                table: "servicios_conexos",
                column: "ActualizadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_solicitud_ConfigVehiculoId",
                table: "lineas_solicitud",
                column: "ConfigVehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_lineas_solicitud_RutaId",
                table: "lineas_solicitud",
                column: "RutaId");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_EntregadaPor",
                table: "cotizaciones",
                column: "EntregadaPor");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_SolicitudId",
                table: "cotizaciones",
                column: "SolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_lineas_LineaSolicitudId",
                table: "cotizacion_lineas",
                column: "LineaSolicitudId");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_conexos_ServicioConexoId",
                table: "cotizacion_conexos",
                column: "ServicioConexoId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_UsuarioId",
                table: "audit_log",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_audit_log_usuarios_UsuarioId",
                table: "audit_log",
                column: "UsuarioId",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cotizacion_conexos_servicios_conexos_ServicioConexoId",
                table: "cotizacion_conexos",
                column: "ServicioConexoId",
                principalTable: "servicios_conexos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cotizacion_lineas_lineas_solicitud_LineaSolicitudId",
                table: "cotizacion_lineas",
                column: "LineaSolicitudId",
                principalTable: "lineas_solicitud",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cotizaciones_solicitudes_SolicitudId",
                table: "cotizaciones",
                column: "SolicitudId",
                principalTable: "solicitudes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cotizaciones_usuarios_EntregadaPor",
                table: "cotizaciones",
                column: "EntregadaPor",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lineas_solicitud_configuraciones_vehiculo_ConfigVehiculoId",
                table: "lineas_solicitud",
                column: "ConfigVehiculoId",
                principalTable: "configuraciones_vehiculo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lineas_solicitud_rutas_RutaId",
                table: "lineas_solicitud",
                column: "RutaId",
                principalTable: "rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_servicios_conexos_usuarios_ActualizadoPor",
                table: "servicios_conexos",
                column: "ActualizadoPor",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_solicitudes_usuarios_CreadoPor",
                table: "solicitudes",
                column: "CreadoPor",
                principalTable: "usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId",
                table: "tarifas_historicas",
                column: "ConfigVehiculoId",
                principalTable: "configuraciones_vehiculo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tarifas_historicas_cotizaciones_CotizacionId",
                table: "tarifas_historicas",
                column: "CotizacionId",
                principalTable: "cotizaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tarifas_historicas_rutas_RutaId",
                table: "tarifas_historicas",
                column: "RutaId",
                principalTable: "rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tarifas_sicetac_rutas_RutaId",
                table: "tarifas_sicetac",
                column: "RutaId",
                principalTable: "rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_log_usuarios_UsuarioId",
                table: "audit_log");

            migrationBuilder.DropForeignKey(
                name: "FK_cotizacion_conexos_servicios_conexos_ServicioConexoId",
                table: "cotizacion_conexos");

            migrationBuilder.DropForeignKey(
                name: "FK_cotizacion_lineas_lineas_solicitud_LineaSolicitudId",
                table: "cotizacion_lineas");

            migrationBuilder.DropForeignKey(
                name: "FK_cotizaciones_solicitudes_SolicitudId",
                table: "cotizaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_cotizaciones_usuarios_EntregadaPor",
                table: "cotizaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_lineas_solicitud_configuraciones_vehiculo_ConfigVehiculoId",
                table: "lineas_solicitud");

            migrationBuilder.DropForeignKey(
                name: "FK_lineas_solicitud_rutas_RutaId",
                table: "lineas_solicitud");

            migrationBuilder.DropForeignKey(
                name: "FK_servicios_conexos_usuarios_ActualizadoPor",
                table: "servicios_conexos");

            migrationBuilder.DropForeignKey(
                name: "FK_solicitudes_usuarios_CreadoPor",
                table: "solicitudes");

            migrationBuilder.DropForeignKey(
                name: "FK_tarifas_historicas_configuraciones_vehiculo_ConfigVehiculoId",
                table: "tarifas_historicas");

            migrationBuilder.DropForeignKey(
                name: "FK_tarifas_historicas_cotizaciones_CotizacionId",
                table: "tarifas_historicas");

            migrationBuilder.DropForeignKey(
                name: "FK_tarifas_historicas_rutas_RutaId",
                table: "tarifas_historicas");

            migrationBuilder.DropForeignKey(
                name: "FK_tarifas_sicetac_rutas_RutaId",
                table: "tarifas_sicetac");

            migrationBuilder.DropIndex(
                name: "IX_tarifas_sicetac_RutaId",
                table: "tarifas_sicetac");

            migrationBuilder.DropIndex(
                name: "IX_tarifas_historicas_ConfigVehiculoId",
                table: "tarifas_historicas");

            migrationBuilder.DropIndex(
                name: "IX_tarifas_historicas_CotizacionId",
                table: "tarifas_historicas");

            migrationBuilder.DropIndex(
                name: "IX_solicitudes_CreadoPor",
                table: "solicitudes");

            migrationBuilder.DropIndex(
                name: "IX_servicios_conexos_ActualizadoPor",
                table: "servicios_conexos");

            migrationBuilder.DropIndex(
                name: "IX_lineas_solicitud_ConfigVehiculoId",
                table: "lineas_solicitud");

            migrationBuilder.DropIndex(
                name: "IX_lineas_solicitud_RutaId",
                table: "lineas_solicitud");

            migrationBuilder.DropIndex(
                name: "IX_cotizaciones_EntregadaPor",
                table: "cotizaciones");

            migrationBuilder.DropIndex(
                name: "IX_cotizaciones_SolicitudId",
                table: "cotizaciones");

            migrationBuilder.DropIndex(
                name: "IX_cotizacion_lineas_LineaSolicitudId",
                table: "cotizacion_lineas");

            migrationBuilder.DropIndex(
                name: "IX_cotizacion_conexos_ServicioConexoId",
                table: "cotizacion_conexos");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_UsuarioId",
                table: "audit_log");

            migrationBuilder.DeleteData(
                table: "usuarios",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "usuarios",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "usuarios",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "AdObjectId",
                table: "usuarios",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnidadTransporte",
                table: "tarifas_sicetac",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "TipoCarga",
                table: "tarifas_sicetac",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "OrigenDane",
                table: "tarifas_sicetac",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "NombreRuta",
                table: "tarifas_sicetac",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DestinoDane",
                table: "tarifas_sicetac",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<string>(
                name: "Configuracion",
                table: "tarifas_sicetac",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "servicios_conexos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "rutas",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Destino",
                table: "rutas",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "TipoVehiculo",
                table: "configuraciones_vehiculo",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RangoPeso",
                table: "configuraciones_vehiculo",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Carroceria",
                table: "configuraciones_vehiculo",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RazonSocial",
                table: "clientes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "clientes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Nit",
                table: "clientes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Entidad",
                table: "audit_log",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Accion",
                table: "audit_log",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
