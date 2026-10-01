using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.Cotizaciones;

// DTOs de ENTRADA del API: la validación HTTP (DataAnnotations, [ApiController])
// vive aquí. El controller los mapea 1:1 a un Command de Application.

public class clsCrearCotizacionRequest
{
    [Required]
    public Guid SolicitudId { get; set; }
}

public class clsAgregarLineaCotizacionRequest
{
    public Guid? LineaSolicitudId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorFlete { get; set; }

    /// <summary>Devolución del contenedor vacío (línea 6 exclusiva); suma al total como concepto aparte.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorRetornoContenedor { get; set; }

    [Required]
    [RegularExpression("(?i)^(sicetac|historica|manual|tarifario)$",
        ErrorMessage = "Fuente debe ser 'sicetac', 'historica', 'manual' o 'tarifario'.")]
    public string Fuente { get; set; } = null!;
}

public class clsAgregarConexosRequest
{
    [Required, MinLength(1, ErrorMessage = "Debe incluir al menos un servicio conexo.")]
    public List<clsConexoItemRequest> Items { get; set; } = new();
}

public class clsConexoItemRequest
{
    /// <summary>Línea de la cotización a la que aplica (S4-HU-15.4/#250) — obligatorio,
    /// ya no existe el conexo "general" sin línea.</summary>
    [Required]
    public Guid LineaId { get; set; }

    [Required]
    public Guid ServicioConexoId { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; } = 1;

    /// <summary>Si no se envía, se calcula como ValorBase * Cantidad del servicio.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAplicado { get; set; }
}

public class clsMarcarResultadoRequest
{
    [Required]
    [RegularExpression("(?i)^(ganada|perdida)$", ErrorMessage = "Resultado debe ser 'ganada' o 'perdida'.")]
    public string Resultado { get; set; } = null!;

    [MaxLength(100)]
    public string? Motivo { get; set; }

    [MaxLength(500)]
    public string? Comentario { get; set; }
}
