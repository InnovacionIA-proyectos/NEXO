using System.ComponentModel.DataAnnotations;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Api.Solicitudes;

/// <summary>
/// Alta de una solicitud completa (cabecera + líneas) con origen/destino/vehículo
/// como TEXTO LIBRE: el servicio resuelve o crea las rutas y configuraciones de
/// vehículo del catálogo. Sirve tanto al formulario manual como a la importación
/// de Excel (el frontend mapea columnas → líneas antes de enviar).
/// </summary>
public class CapturaSolicitudRequest
{
    public Guid? ClienteId { get; set; }

    [MaxLength(20)]
    public string? ClienteBp { get; set; }

    [Required]
    [RegularExpression("(?i)^(spot|licitacion)$", ErrorMessage = "Tipo debe ser 'spot' o 'licitacion'.")]
    public string Tipo { get; set; } = null!;

    public string? Modalidad { get; set; }

    public DateTimeOffset? Deadline { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorEstimado { get; set; }

    public string? Referencia { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasCargue { get; set; }

    [Range(typeof(decimal), "0", "240", ParseLimitsInInvariantCulture = true)]
    public decimal? HorasDescargue { get; set; }

    [Required, MinLength(1, ErrorMessage = "La solicitud debe traer al menos una línea.")]
    public List<CapturaLineaRequest> Lineas { get; set; } = [];
}

public class CapturaLineaRequest
{
    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    [MaxLength(100)]
    public string? TipoVehiculo { get; set; }

    [MaxLength(100)]
    public string? Carroceria { get; set; }

    public string? Tipologia { get; set; }

    [MaxLength(100)]
    public string? LineaNegocio { get; set; }

    [MaxLength(50)]
    public string? ClaseViaje { get; set; }

    [MaxLength(20)]
    public string? ClaseContenedor { get; set; }

    public bool? Exclusivo { get; set; }
    public bool? Consolidado { get; set; }
    public bool? RetornaContenedor { get; set; }

    [MaxLength(150)]
    public string? CiudadDevolucionContenedor { get; set; }

    public bool? LevanteContenedorDiferente { get; set; }

    [MaxLength(150)]
    public string? CiudadLevanteContenedor { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Peso { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorMercancia { get; set; }

    [MaxLength(200, ErrorMessage = "Una línea admite máximo 200 posiciones.")]
    public List<CapturaPosicionRequest>? Posiciones { get; set; }
}

/// <summary>
/// Una línea de texto libre a resolver contra el catálogo (sin crear ninguna solicitud):
/// entrada de POST /api/solicitudes/resolver-catalogos.
/// </summary>
public class ResolverCatalogoLineaRequest
{
    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    [MaxLength(100)]
    public string? TipoVehiculo { get; set; }

    [MaxLength(100)]
    public string? Carroceria { get; set; }
}

/// <summary>Una pieza/bulto a transportar, con dimensiones en centímetros.</summary>
public class CapturaPosicionRequest
{
    [MaxLength(200)]
    public string? Descripcion { get; set; }

    [Range(1, 100000)]
    public int Cantidad { get; set; } = 1;

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal LargoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AnchoCm { get; set; }

    [Required, Range(typeof(decimal), "0.1", "999999", ParseLimitsInInvariantCulture = true)]
    public decimal AltoCm { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? PesoKg { get; set; }
}
