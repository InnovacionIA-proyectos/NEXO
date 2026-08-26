using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CabinaFletes.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// VA VACÍA A PROPÓSITO. El cambio es de modelo, no de esquema: las claves Guid pasan
    /// a declararse como asignadas por el dominio en vez de generadas al insertar, y eso
    /// no toca ninguna columna — las tablas nunca tuvieron un DEFAULT NEWID().
    ///
    /// Existe para que la instantánea del modelo quede al día. Sin ella, la próxima
    /// migración que alguien genere arrastraría este cambio mezclado con el suyo.
    /// </summary>
    public partial class ClaveAsignadaPorElDominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
