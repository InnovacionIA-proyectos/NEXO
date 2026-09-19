using System.ComponentModel.DataAnnotations;

namespace CabinaFletes.Api.ServiciosConexos;

public class CrearServicioConexoRequest
{
    [Required, MaxLength(120)]
    public string Nombre { get; set; } = null!;

    /// <summary>Segmento del catálogo: "logistico" o "adicional". Por defecto "logistico".</summary>
    [MaxLength(20)]
    public string? Categoria { get; set; }

    /// <summary>Regla de cobro como JSON (p. ej. {"tipo":"por_hora","valor":45000}).</summary>
    public string? ReglaCobro { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorBase { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>PATCH: solo se actualizan los campos presentes (no nulos).</summary>
public class EditarServicioConexoRequest
{
    [MaxLength(120)]
    public string? Nombre { get; set; }

    [MaxLength(20)]
    public string? Categoria { get; set; }

    public string? ReglaCobro { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorBase { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool? Activo { get; set; }
}
