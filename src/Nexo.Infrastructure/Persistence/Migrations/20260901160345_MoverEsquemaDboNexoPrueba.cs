using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoverEsquemaDboNexoPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                newName: "vehiculos_cubicacion",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "usuarios",
                newName: "usuarios",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                newName: "trabajos_auto_costeo",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                newName: "tarifas_sicetac",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                newName: "tarifas_historicas",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "solicitudes",
                newName: "solicitudes",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                newName: "servicios_conexos",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "rutas",
                newName: "rutas",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                newName: "reglas_solo_sicetac",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                newName: "planes_cargue",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "parametros_score",
                newName: "parametros_score",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "OutboxState",
                newName: "OutboxState",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                newName: "OutboxMessage",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                newName: "mapeo_rol_portal",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                newName: "lineas_solicitud",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                newName: "lineas_pendientes_de_costeo",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                newName: "items_plan_cargue",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "InboxState",
                newName: "InboxState",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                newName: "cotizaciones",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                newName: "cotizacion_lineas",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                newName: "cotizacion_conexos",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                newName: "configuraciones_vehiculo",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                newName: "colocaciones_cargue",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "clientes",
                newName: "clientes",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                newName: "cadena_priorizacion",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "audit_log",
                newName: "audit_log",
                newSchema: "dbo_NEXO_prueba");

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                newName: "ajustes_vigencia_plaza",
                newSchema: "dbo_NEXO_prueba");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                schema: "dbo_NEXO_prueba",
                newName: "vehiculos_cubicacion");

            migrationBuilder.RenameTable(
                name: "usuarios",
                schema: "dbo_NEXO_prueba",
                newName: "usuarios");

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                schema: "dbo_NEXO_prueba",
                newName: "trabajos_auto_costeo");

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                schema: "dbo_NEXO_prueba",
                newName: "tarifas_sicetac");

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                schema: "dbo_NEXO_prueba",
                newName: "tarifas_historicas");

            migrationBuilder.RenameTable(
                name: "solicitudes",
                schema: "dbo_NEXO_prueba",
                newName: "solicitudes");

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                schema: "dbo_NEXO_prueba",
                newName: "servicios_conexos");

            migrationBuilder.RenameTable(
                name: "rutas",
                schema: "dbo_NEXO_prueba",
                newName: "rutas");

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                schema: "dbo_NEXO_prueba",
                newName: "reglas_solo_sicetac");

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                schema: "dbo_NEXO_prueba",
                newName: "planes_cargue");

            migrationBuilder.RenameTable(
                name: "parametros_score",
                schema: "dbo_NEXO_prueba",
                newName: "parametros_score");

            migrationBuilder.RenameTable(
                name: "OutboxState",
                schema: "dbo_NEXO_prueba",
                newName: "OutboxState");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                schema: "dbo_NEXO_prueba",
                newName: "OutboxMessage");

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                schema: "dbo_NEXO_prueba",
                newName: "mapeo_rol_portal");

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                schema: "dbo_NEXO_prueba",
                newName: "lineas_solicitud");

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                schema: "dbo_NEXO_prueba",
                newName: "lineas_pendientes_de_costeo");

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                schema: "dbo_NEXO_prueba",
                newName: "items_plan_cargue");

            migrationBuilder.RenameTable(
                name: "InboxState",
                schema: "dbo_NEXO_prueba",
                newName: "InboxState");

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                schema: "dbo_NEXO_prueba",
                newName: "cotizaciones");

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                schema: "dbo_NEXO_prueba",
                newName: "cotizacion_lineas");

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                schema: "dbo_NEXO_prueba",
                newName: "cotizacion_conexos");

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                schema: "dbo_NEXO_prueba",
                newName: "configuraciones_vehiculo");

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                schema: "dbo_NEXO_prueba",
                newName: "colocaciones_cargue");

            migrationBuilder.RenameTable(
                name: "clientes",
                schema: "dbo_NEXO_prueba",
                newName: "clientes");

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                schema: "dbo_NEXO_prueba",
                newName: "cadena_priorizacion");

            migrationBuilder.RenameTable(
                name: "audit_log",
                schema: "dbo_NEXO_prueba",
                newName: "audit_log");

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                schema: "dbo_NEXO_prueba",
                newName: "ajustes_vigencia_plaza");
        }
    }
}
