using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Cubicacion.Entidades;

/// <summary>Proyección del catálogo de tipos de carrocería del Cubicador, mantenida por los eventos de carrocería. Mismo patrón que <see cref="clsTipoMercanciaCatalogo"/>.</summary>
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
