using Microsoft.EntityFrameworkCore.Migrations;
using Nexo.Infrastructure.Persistence;

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
                name: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                newName: "vehiculos_cubicacion",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "usuarios",
                newName: "usuarios",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                newName: "trabajos_auto_costeo",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                newName: "tarifas_sicetac",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                newName: "tarifas_historicas",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "solicitudes",
                newName: "solicitudes",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                newName: "servicios_conexos",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "rutas",
                newName: "rutas",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                newName: "reglas_solo_sicetac",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                newName: "planes_cargue",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "parametros_score",
                newName: "parametros_score",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "OutboxState",
                newName: "OutboxState",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                newName: "OutboxMessage",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                newName: "mapeo_rol_portal",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                newName: "lineas_solicitud",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                newName: "lineas_pendientes_de_costeo",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                newName: "items_plan_cargue",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "InboxState",
                newName: "InboxState",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                newName: "cotizaciones",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                newName: "cotizacion_lineas",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                newName: "cotizacion_conexos",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                newName: "configuraciones_vehiculo",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                newName: "colocaciones_cargue",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "clientes",
                newName: "clientes",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                newName: "cadena_priorizacion",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "audit_log",
                newName: "audit_log",
                newSchema: clsEsquemaNexo.Nombre);

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                newName: "ajustes_vigencia_plaza",
                newSchema: clsEsquemaNexo.Nombre);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "vehiculos_cubicacion",
                schema: clsEsquemaNexo.Nombre,
                newName: "vehiculos_cubicacion");

            migrationBuilder.RenameTable(
                name: "usuarios",
                schema: clsEsquemaNexo.Nombre,
                newName: "usuarios");

            migrationBuilder.RenameTable(
                name: "trabajos_auto_costeo",
                schema: clsEsquemaNexo.Nombre,
                newName: "trabajos_auto_costeo");

            migrationBuilder.RenameTable(
                name: "tarifas_sicetac",
                schema: clsEsquemaNexo.Nombre,
                newName: "tarifas_sicetac");

            migrationBuilder.RenameTable(
                name: "tarifas_historicas",
                schema: clsEsquemaNexo.Nombre,
                newName: "tarifas_historicas");

            migrationBuilder.RenameTable(
                name: "solicitudes",
                schema: clsEsquemaNexo.Nombre,
                newName: "solicitudes");

            migrationBuilder.RenameTable(
                name: "servicios_conexos",
                schema: clsEsquemaNexo.Nombre,
                newName: "servicios_conexos");

            migrationBuilder.RenameTable(
                name: "rutas",
                schema: clsEsquemaNexo.Nombre,
                newName: "rutas");

            migrationBuilder.RenameTable(
                name: "reglas_solo_sicetac",
                schema: clsEsquemaNexo.Nombre,
                newName: "reglas_solo_sicetac");

            migrationBuilder.RenameTable(
                name: "planes_cargue",
                schema: clsEsquemaNexo.Nombre,
                newName: "planes_cargue");

            migrationBuilder.RenameTable(
                name: "parametros_score",
                schema: clsEsquemaNexo.Nombre,
                newName: "parametros_score");

            migrationBuilder.RenameTable(
                name: "OutboxState",
                schema: clsEsquemaNexo.Nombre,
                newName: "OutboxState");

            migrationBuilder.RenameTable(
                name: "OutboxMessage",
                schema: clsEsquemaNexo.Nombre,
                newName: "OutboxMessage");

            migrationBuilder.RenameTable(
                name: "mapeo_rol_portal",
                schema: clsEsquemaNexo.Nombre,
                newName: "mapeo_rol_portal");

            migrationBuilder.RenameTable(
                name: "lineas_solicitud",
                schema: clsEsquemaNexo.Nombre,
                newName: "lineas_solicitud");

            migrationBuilder.RenameTable(
                name: "lineas_pendientes_de_costeo",
                schema: clsEsquemaNexo.Nombre,
                newName: "lineas_pendientes_de_costeo");

            migrationBuilder.RenameTable(
                name: "items_plan_cargue",
                schema: clsEsquemaNexo.Nombre,
                newName: "items_plan_cargue");

            migrationBuilder.RenameTable(
                name: "InboxState",
                schema: clsEsquemaNexo.Nombre,
                newName: "InboxState");

            migrationBuilder.RenameTable(
                name: "cotizaciones",
                schema: clsEsquemaNexo.Nombre,
                newName: "cotizaciones");

            migrationBuilder.RenameTable(
                name: "cotizacion_lineas",
                schema: clsEsquemaNexo.Nombre,
                newName: "cotizacion_lineas");

            migrationBuilder.RenameTable(
                name: "cotizacion_conexos",
                schema: clsEsquemaNexo.Nombre,
                newName: "cotizacion_conexos");

            migrationBuilder.RenameTable(
                name: "configuraciones_vehiculo",
                schema: clsEsquemaNexo.Nombre,
                newName: "configuraciones_vehiculo");

            migrationBuilder.RenameTable(
                name: "colocaciones_cargue",
                schema: clsEsquemaNexo.Nombre,
                newName: "colocaciones_cargue");

            migrationBuilder.RenameTable(
                name: "clientes",
                schema: clsEsquemaNexo.Nombre,
                newName: "clientes");

            migrationBuilder.RenameTable(
                name: "cadena_priorizacion",
                schema: clsEsquemaNexo.Nombre,
                newName: "cadena_priorizacion");

            migrationBuilder.RenameTable(
                name: "audit_log",
                schema: clsEsquemaNexo.Nombre,
                newName: "audit_log");

            migrationBuilder.RenameTable(
                name: "ajustes_vigencia_plaza",
                schema: clsEsquemaNexo.Nombre,
                newName: "ajustes_vigencia_plaza");
        }
    }
}
