using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class clsMoverEsquemaDboNexoPrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                newName: "vehiculos_cubicacion",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "usuarios",
                newName: "usuarios",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                newName: "trabajos_auto_costeo",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                newName: "tarifas_sicetac",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                newName: "tarifas_historicas",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "solicitudes",
                newName: "solicitudes",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                newName: "servicios_conexos",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "rutas",
                newName: "rutas",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                newName: "reglas_solo_sicetac",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                newName: "planes_cargue",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "parametros_score",
                newName: "parametros_score",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "OutboxState",
                newName: "OutboxState",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                newName: "OutboxMessage",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                newName: "mapeo_rol_portal",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                newName: "lineas_solicitud",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                newName: "lineas_pendientes_de_costeo",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                newName: "items_plan_cargue",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "InboxState",
                newName: "InboxState",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                newName: "cotizaciones",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                newName: "cotizacion_lineas",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                newName: "cotizacion_conexos",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                newName: "configuraciones_vehiculo",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                newName: "colocaciones_cargue",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "clientes",
                newName: "clientes",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                newName: "cadena_priorizacion",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "audit_log",
                newName: "audit_log",
                newSchema: "db_NEXO");

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                newName: "ajustes_vigencia_plaza",
                newSchema: "db_NEXO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                schema: "db_NEXO",
                newName: "vehiculos_cubicacion");

            migrationBuilder.RenameTable(
                name: "usuarios",
                schema: "db_NEXO",
                newName: "usuarios");

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                schema: "db_NEXO",
                newName: "trabajos_auto_costeo");

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                schema: "db_NEXO",
                newName: "tarifas_sicetac");

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                schema: "db_NEXO",
                newName: "tarifas_historicas");

            migrationBuilder.RenameTable(
                name: "solicitudes",
                schema: "db_NEXO",
                newName: "solicitudes");

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                schema: "db_NEXO",
                newName: "servicios_conexos");

            migrationBuilder.RenameTable(
                name: "rutas",
                schema: "db_NEXO",
                newName: "rutas");

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                schema: "db_NEXO",
                newName: "reglas_solo_sicetac");

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                schema: "db_NEXO",
                newName: "planes_cargue");

            migrationBuilder.RenameTable(
                name: "parametros_score",
                schema: "db_NEXO",
                newName: "parametros_score");

            migrationBuilder.RenameTable(
                name: "OutboxState",
                schema: "db_NEXO",
                newName: "OutboxState");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                schema: "db_NEXO",
                newName: "OutboxMessage");

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                schema: "db_NEXO",
                newName: "mapeo_rol_portal");

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                schema: "db_NEXO",
                newName: "lineas_solicitud");

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                schema: "db_NEXO",
                newName: "lineas_pendientes_de_costeo");

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                schema: "db_NEXO",
                newName: "items_plan_cargue");

            migrationBuilder.RenameTable(
                name: "InboxState",
                schema: "db_NEXO",
                newName: "InboxState");

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                schema: "db_NEXO",
                newName: "cotizaciones");

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                schema: "db_NEXO",
                newName: "cotizacion_lineas");

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                schema: "db_NEXO",
                newName: "cotizacion_conexos");

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                schema: "db_NEXO",
                newName: "configuraciones_vehiculo");

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                schema: "db_NEXO",
                newName: "colocaciones_cargue");

            migrationBuilder.RenameTable(
                name: "clientes",
                schema: "db_NEXO",
                newName: "clientes");

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                schema: "db_NEXO",
                newName: "cadena_priorizacion");

            migrationBuilder.RenameTable(
                name: "audit_log",
                schema: "db_NEXO",
                newName: "audit_log");

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                schema: "db_NEXO",
                newName: "ajustes_vigencia_plaza");
        }
    }
}
