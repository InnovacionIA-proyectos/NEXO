using System.ComponentModel.DataAnnotations;

namespace Nexo.Api.ServiciosConexos;

/// <summary>
/// Origen/Destino/Puerto/Ruta/TipoMaquina son opcionales: cada tipo de servicio usa solo
/// las que le corresponden (S4-HU-15.1/#223) — las demás se normalizan a "NO APLICA".
/// </summary>
public class clsCrearServicioConexoRequest
{
    [Required, MaxLength(30)]
    public string CodigoServicio { get; set; } = null!;

    [Required, MaxLength(120)]
    public string TipoServicio { get; set; } = null!;

    [MaxLength(150)]
    public string? Origen { get; set; }

    [MaxLength(150)]
    public string? Destino { get; set; }

    [MaxLength(50)]
    public string? Puerto { get; set; }

    [MaxLength(50)]
    public string? Ruta { get; set; }

    [MaxLength(60)]
    public string? TipoMaquina { get; set; }

    [Required, Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal CostoUnitario { get; set; }

    [Required, MaxLength(30)]
    public string Unidad { get; set; } = null!;

    [Required]
    public DateOnly Vigencia { get; set; }

    /// <summary>Segmento del catálogo: "logistico" o "adicional". Por defecto "logistico".</summary>
    [MaxLength(20)]
    public string? Categoria { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>PATCH: solo se actualizan los campos presentes (no nulos).</summary>
public class clsEditarServicioConexoRequest
{
    [MaxLength(30)]
    public string? CodigoServicio { get; set; }

    [MaxLength(120)]
    public string? TipoServicio { get; set; }

    [MaxLength(150)]
    public string? Origen { get; set; }

    [MaxLength(150)]
    public string? Destino { get; set; }

    [MaxLength(50)]
    public string? Puerto { get; set; }

    [MaxLength(50)]
    public string? Ruta { get; set; }

    [MaxLength(60)]
    public string? TipoMaquina { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? CostoUnitario { get; set; }

    [MaxLength(30)]
    public string? Unidad { get; set; }

    public DateOnly? Vigencia { get; set; }

    [MaxLength(20)]
    public string? Categoria { get; set; }

    public bool? Activo { get; set; }
}
