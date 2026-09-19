using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Domain.Cotizaciones.Entidades;

/// <summary>Cotización integral consolidada (flete + servicios conexos).</summary>
public class Cotizacion : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SolicitudId { get; set; }

    [JsonIgnore]
    public Solicitud? Solicitud { get; set; }

    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;
    public ResultadoCotizacion Resultado { get; set; } = ResultadoCotizacion.SinDefinir;

    [Range(typeof(decimal), "0", "99999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Total { get; set; }

    public Guid? EntregadaPor { get; set; }

    [JsonIgnore]
    public Usuario? EntregadaPorUsuario { get; set; }

    public DateTimeOffset? EntregadaAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Token de concurrencia optimista (rowversion): dos usuarios no se pisan en silencio.</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<CotizacionLinea> Lineas { get; set; } = new List<CotizacionLinea>();
    public ICollection<CotizacionConexo> Conexos { get; set; } = new List<CotizacionConexo>();
}

public class CotizacionLinea : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CotizacionId { get; set; }

    public Guid? LineaSolicitudId { get; set; }

    [JsonIgnore]
    public LineaSolicitud? LineaSolicitud { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorFlete { get; set; }

    /// <summary>
    /// Aprovisionamiento del contenedor (posicionar el vacío levante → origen,
    /// Res. 16075). Concepto separado del flete que suma al total.
    /// </summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    /// <summary>
    /// Devolución del contenedor vacío al finalizar el viaje (tarifa línea 6
    /// exclusiva). Concepto separado del flete que suma al total.
    /// </summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorRetornoContenedor { get; set; }

    public FuenteFlete Fuente { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CotizacionConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CotizacionId { get; set; }

    public Guid? ServicioConexoId { get; set; }

    [JsonIgnore]
    public ServicioConexo? ServicioConexo { get; set; }

    [Range(typeof(decimal), "0", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorAplicado { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
