using System.ComponentModel.DataAnnotations;

namespace CabinaFletes.Api.Dtos;

/// <summary>Alta de una solicitud spot (o cabecera de licitación; las líneas se cargan luego en bulk).</summary>
public class CrearSolicitudRequest
{
    [Required]
    public Guid ClienteId { get; set; }

    [Required]
    [RegularExpression("(?i)^(spot|licitacion)$", ErrorMessage = "Tipo debe ser 'spot' o 'licitacion'.")]
    public string Tipo { get; set; } = null!;

    public string? Modalidad { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorEstimado { get; set; }

    [Range(0, 100, ErrorMessage = "La probabilidad debe estar entre 0 y 100.")]
    public decimal? ProbabilidadCierre { get; set; }

    public CrearLineaRequest? Linea { get; set; }
}

public class CrearLineaRequest
{
    public Guid? RutaId { get; set; }
    public Guid? ConfigVehiculoId { get; set; }
    public string? Tipologia { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }
}

/// <summary>Carga masiva de líneas de un pliego de licitación.</summary>
public class CargarLineasBulkRequest
{
    [Required, MinLength(1, ErrorMessage = "Debe incluir al menos una línea.")]
    public List<CrearLineaRequest> Lineas { get; set; } = [];
}

/// <summary>Rechazo de una solicitud por el equipo comercial, con su motivo.</summary>
public class RechazarSolicitudRequest
{
    [Required, MaxLength(100)]
    public string Motivo { get; set; } = null!;

    [MaxLength(500)]
    public string? Comentario { get; set; }
}
