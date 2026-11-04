using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Domain.Cotizaciones.Entidades;

/// <summary>Cotización integral consolidada (flete + servicios conexos).</summary>
public class clsCotizacion : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SolicitudId { get; set; }

    [JsonIgnore]
    public clsSolicitud? clsSolicitud { get; set; }

    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;
    public ResultadoCotizacion Resultado { get; set; } = ResultadoCotizacion.SinDefinir;

    /// <summary>Cotización auto-costeada: nunca pasa por entregar. Una manual sí exige Entregada antes de marcar resultado.</summary>
    public bool EsAutoCosteo { get; set; }

    [Range(typeof(decimal), "0", "99999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Total { get; set; }

    public Guid? EntregadaPor { get; set; }

    [JsonIgnore]
    public clsUsuario? EntregadaPorUsuario { get; set; }

    public DateTimeOffset? EntregadaAt { get; set; }

    /// <summary>Última vez que se agregó o quitó un conexo tras la entrega. No mueve <see cref="Estado"/> ni <see cref="EntregadaAt"/>: el SLA mide la entrega original.</summary>
    public DateTimeOffset? ModificadaTrasEntregaAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Token de concurrencia optimista (rowversion): dos usuarios no se pisan en silencio.</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<clsCotizacionLinea> Lineas { get; set; } = new List<clsCotizacionLinea>();
    public ICollection<clsCotizacionConexo> Conexos { get; set; } = new List<clsCotizacionConexo>();
}

public class clsCotizacionLinea : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CotizacionId { get; set; }

    public Guid? LineaSolicitudId { get; set; }

    [JsonIgnore]
    public clsLineaSolicitud? clsLineaSolicitud { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorFlete { get; set; }

    /// <summary>Aprovisionamiento del contenedor (vacío levante → origen); concepto separado del flete que suma al total.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorAprovisionamiento { get; set; }

    /// <summary>Devolución del contenedor vacío al finalizar el viaje; concepto separado del flete que suma al total.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorRetornoContenedor { get; set; }

    public FuenteFlete Fuente { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class clsCotizacionConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CotizacionId { get; set; }

    /// <summary>Línea de la cotización a la que aplica el conexo (obligatoria). Apunta a clsCotizacionLinea y no a la línea de solicitud porque cada recosteo tiene sus propias líneas.</summary>
    [Required]
    public Guid CotizacionLineaId { get; set; }

    [JsonIgnore]
    public clsCotizacionLinea? clsCotizacionLinea { get; set; }

    public Guid? ServicioConexoId { get; set; }

    [JsonIgnore]
    public clsServicioConexo? clsServicioConexo { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Cantidad { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal ValorAplicado { get; set; }

    /// <summary>Precio de venta unitario al agregarlo (el de la biblioteca o, si no tenía, su costo); queda fijo. <see cref="ValorAplicado"/> es el costo; lo que se cotiza es <see cref="ValorVenta"/>.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal PrecioVentaUnitario { get; set; }

    /// <summary>Lo que se cotiza del conexo: <see cref="PrecioVentaUnitario"/> × <see cref="Cantidad"/>, a 2 decimales (como el costo).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal ValorVenta => Math.Round(PrecioVentaUnitario * Cantidad, 2, MidpointRounding.AwayFromZero);

    /// <summary>"individual" (agregado a esta línea) o "general" (agregado a todas de una vez; igual crea un conexo por línea). Solo trazabilidad de cómo se agregó.</summary>
    [Required, MaxLength(20)]
    public string Origen { get; set; } = "individual";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
