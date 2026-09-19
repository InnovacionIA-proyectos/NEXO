using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.AjustesVigenciaPlaza;

/// <summary>Ajuste 1 a 1 de la vigencia de una tarifa de plaza.</summary>
public class AjustarVigenciaPlazaRequest
{
    [Required]
    public DateTime NuevaVigencia { get; set; }

    [MaxLength(300)]
    public string? Motivo { get; set; }
}

/// <summary>
/// Ajuste MASIVO de vigencia sobre un conjunto de tarifas de plaza (por Id).
/// Modo "fecha": fija la misma NuevaVigencia a todas. Modo "extender": suma Cantidad
/// de Unidad ("dias"|"meses") a la vigencia efectiva de cada una.
/// </summary>
public class AjusteVigenciaMasivoRequest
{
    [Required, MinLength(1, ErrorMessage = "Indica al menos una tarifa.")]
    public List<int> TarifaIds { get; set; } = new();

    [Required]
    [RegularExpression("(?i)^(fecha|extender)$", ErrorMessage = "Modo debe ser 'fecha' o 'extender'.")]
    public string Modo { get; set; } = null!;

    public DateTime? NuevaVigencia { get; set; }

    public int? Cantidad { get; set; }

    [RegularExpression("(?i)^(dias|meses)$", ErrorMessage = "Unidad debe ser 'dias' o 'meses'.")]
    public string? Unidad { get; set; }

    [MaxLength(300)]
    public string? Motivo { get; set; }
}
