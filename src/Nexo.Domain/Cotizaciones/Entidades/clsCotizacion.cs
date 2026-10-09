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

    /// <summary>
    /// S4-HU-15.6/#252: distingue una cotización auto-costeada (nunca pasa por
    /// /entregar — no tiene sentido notificar por email al mismo Comercial que la
    /// generó) de una manual (SÍ exige Entregada antes de marcar resultado, decisión
    /// de negocio #171). Sin esto, auto-costeo quedaba bloqueado para siempre: el
    /// paso de Entregar es exclusivo de Tarifas/Coordinador, que nunca interviene
    /// en ese camino.
    /// </summary>
    public bool EsAutoCosteo { get; set; }

    [Range(typeof(decimal), "0", "99999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Total { get; set; }

    public Guid? EntregadaPor { get; set; }

    [JsonIgnore]
    public clsUsuario? EntregadaPorUsuario { get; set; }

    public DateTimeOffset? EntregadaAt { get; set; }

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

public class clsCotizacionConexo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CotizacionId { get; set; }

    /// <summary>
    /// Línea de la cotización a la que aplica este conexo (S4-HU-15.4/#250). Obligatoria
    /// a propósito: antes un conexo colgaba de la cotización completa sin poder decir a
    /// cuál línea aplicaba — ya no existe la opción de un conexo "general". Apunta a
    /// clsCotizacionLinea (no a clsLineaSolicitud): si la solicitud se recostea, cada
    /// recosteo tiene sus propias líneas de cotización, y el conexo sigue esa misma vida.
    /// </summary>
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

    /// <summary>
    /// S5-HU-18/#262: "individual" (el usuario lo agregó a ESTA línea puntual) o
    /// "general" (el usuario pidió agregarlo a TODAS las líneas de la cotización de una
    /// sola vez, lo que en la práctica crea un clsCotizacionConexo por línea — sigue
    /// existiendo una línea por conexo, igual que decidió #250; esto es solo la
    /// trazabilidad de CÓMO se agregó, no un conexo "general" sin línea). Antes el
    /// frontend tenía que adivinar esto; ahora es un dato real.
    /// </summary>
    [Required, MaxLength(20)]
    public string Origen { get; set; } = "individual";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
