using System.ComponentModel.DataAnnotations;

namespace Nexo.Domain.Cubicacion.Entidades;

/// <summary>
/// Proyección del catálogo de tipos de mercancía del Cubicador, mantenida por los eventos de mercancía.
/// <see cref="Id"/> es el que asigna el Cubicador (string, no necesariamente UUID). El contrato de cubicación
/// y la Captura usan <see cref="CodigoMercancia"/> (estable y legible). <see cref="CarroceriaPrincipal"/>/
/// <see cref="CarroceriaSecundaria"/> llegan resueltas a código, no a id.
/// </summary>
public class clsTipoMercanciaCatalogo
{
    [MaxLength(64)]
    public string Id { get; set; } = null!;

    [Required, MaxLength(40)]
    public string CodigoMercancia { get; set; } = null!;

    [Required, MaxLength(150)]
    public string NombreMercancia { get; set; } = null!;

    [MaxLength(100)]
    public string Categoria { get; set; } = null!;

    public bool Cubicable { get; set; }
    public bool CondicionadaCubicacion { get; set; }

    [MaxLength(40)]
    public string? CarroceriaPrincipal { get; set; }

    [MaxLength(40)]
    public string? CarroceriaSecundaria { get; set; }

    public bool CarroceriaSecundariaCondicionada { get; set; }

    /// <summary>NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL | REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO</summary>
    [Required, MaxLength(40)]
    public string ControlAmbientalRequerido { get; set; } = null!;

    public bool Apilable { get; set; }
    public bool Fragil { get; set; }
    public bool RotacionLibre { get; set; }

    [MaxLength(500)]
    public string? ComentarioCondicion { get; set; }

    /// <summary>Se guardan también las inactivas (como el Cubicador); el desplegable de Captura es quien filtra por Activo.</summary>
    public bool Activo { get; set; }

    /// <summary>Concurrencia optimista del Cubicador; informativa, NEXO no escribe esta tabla.</summary>
    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
