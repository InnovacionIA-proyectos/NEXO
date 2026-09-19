using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Common;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Domain.Catalogos.Entidades;

public class Ruta : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(150)]
    public string Origen { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Destino { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ConfiguracionVehiculo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string TipoVehiculo { get; set; } = null!;

    [MaxLength(100)]
    public string? Carroceria { get; set; }

    [MaxLength(50)]
    public string? RangoPeso { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Catálogo de servicios conexos mantenido por Servicios Logísticos.</summary>
public class ServicioConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    public string Nombre { get; set; } = null!;

    /// <summary>Segmento del catálogo: "logistico" (por defecto) o "adicional".</summary>
    [Required, MaxLength(20)]
    public string Categoria { get; set; } = "logistico";

    /// <summary>Fórmula/parámetros de cobro serializados como JSON.</summary>
    public string? ReglaCobro { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorBase { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public bool Activo { get; set; } = true;

    public Guid? ActualizadoPor { get; set; }

    [JsonIgnore]
    public Usuario? ActualizadoPorUsuario { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Token de concurrencia optimista (rowversion).</summary>
    public byte[]? RowVersion { get; set; }
}
