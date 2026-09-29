using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.Solicitudes;

/// <summary>Alta de una solicitud spot (o cabecera de licitación; las líneas se cargan luego en bulk).</summary>
public class clsCrearSolicitudRequest
{
    /// <summary>
    /// Issue #206: uno de los dos es obligatorio (lo valida el handler, no aquí, para
    /// devolver el mismo código CLIENTE_REQUERIDO que ya usa Captura). ClienteBp resuelve
    /// o crea el espejo local del cliente real del catálogo DW por NIT.
    /// </summary>
    public Guid? ClienteId { get; set; }

    public string? ClienteBp { get; set; }

    [Required]
    [RegularExpression("(?i)^(spot|licitacion)$", ErrorMessage = "Tipo debe ser 'spot' o 'licitacion'.")]
    public string Tipo { get; set; } = null!;

    public string? Modalidad { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorEstimado { get; set; }

    [Range(0, 100, ErrorMessage = "La probabilidad debe estar entre 0 y 100.")]
    public decimal? ProbabilidadCierre { get; set; }

    public clsCrearLineaRequest? Linea { get; set; }
}

public class clsCrearLineaRequest
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
public class clsCargarLineasBulkRequest
{
    [Required, MinLength(1, ErrorMessage = "Debe incluir al menos una línea.")]
    public List<clsCrearLineaRequest> Lineas { get; set; } = [];
}

/// <summary>Rechazo de una solicitud por el equipo comercial, con su motivo.</summary>
public class clsRechazarSolicitudRequest
{
    [Required, MaxLength(100)]
    public string Motivo { get; set; } = null!;

    [MaxLength(500)]
    public string? Comentario { get; set; }
}
