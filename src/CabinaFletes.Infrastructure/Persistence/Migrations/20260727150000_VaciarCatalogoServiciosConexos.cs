using CabinaFletes.Domain.Cotizaciones.Entidades;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VaciarCatalogoServiciosConexos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Vaciado del catálogo de servicios conexos: se eliminan TODAS las filas,
            // tanto las de la carga masiva del tarifario 2026 ('logistico'/'adicional')
            // como las del catálogo base. Equivale al DELETE solicitado, que por sí solo
            // no podía correr: 'cotizacion_conexos' referencia esta tabla con FK
            // Restrict (ver CabinaFletesDbContext, entidad CotizacionConexo) y abortaba
            // con error 547 sin borrar nada.

            // 1) Las líneas de conexos de las cotizaciones deben caer primero.
            //    Ojo: Cotizacion.Total NO se recalcula — conserva el valor con el que
            //    la cotización se entregó al cliente, así que a partir de aquí el total
            //    de las cotizaciones afectadas ya no cuadra con la suma de sus líneas.
            migrationBuilder.Sql("DELETE FROM cotizacion_conexos;");

            // 2) Ya sin referencias, el catálogo se vacía.
            migrationBuilder.Sql("DELETE FROM servicios_conexos;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: los datos borrados no se pueden reconstruir desde la migración.
            // Para recuperarlos: restaurar respaldo, o recargar el catálogo con
            // scripts/carga-servicios-2026.sql.
        }
    }
}
