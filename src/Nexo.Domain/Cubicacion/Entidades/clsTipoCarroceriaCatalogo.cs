using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Cubicacion.Entidades;

/// <summary>
/// S5-HU-18.5/#277: proyección del catálogo de tipos de carrocería del Cubicador
/// (<c>GET /tipos-carroceria</c>, <c>cubicador/api/dtos/catalogo.py::TipoCarroceriaDto</c>),
/// mantenida al día por <c>CarroceriaCreada</c>/<c>CarroceriaActualizada</c>/
/// <c>CarroceriaEliminada</c>. Ver <see cref="clsTipoMercanciaCatalogo"/> para el criterio
/// completo (mismo patrón, catálogo hermano).
/// </summary>
public class clsTipoCarroceriaCatalogo
{
    [MaxLength(64)]
    public string Id { get; set; } = null!;

    [Required, MaxLength(40)]
    public string CodigoCarroceria { get; set; } = null!;

    [Required, MaxLength(150)]
    public string NombreCarroceria { get; set; } = null!;

    public bool Activo { get; set; }

    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
