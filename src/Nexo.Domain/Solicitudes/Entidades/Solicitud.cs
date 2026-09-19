using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Domain.Solicitudes.Entidades;

/// <summary>Cabecera de una solicitud de cotización (spot o licitación).</summary>
public class Solicitud : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClienteId { get; set; }

    [JsonIgnore]
    public Cliente? Cliente { get; set; }

    [Required]
    public TipoSolicitud Tipo { get; set; }

    public ModalidadSolicitud? Modalidad { get; set; }

    /// <summary>Condiciones del servicio (recogida/entrega, ratas cargue/descargue,
    /// medidas de seguridad, póliza) serializadas como JSON.</summary>
    public string? CondicionesServicio { get; set; }

    /// <summary>Fecha límite de respuesta (SLA). Insumo de la urgencia del score.</summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>Probabilidad de cierre (0–1 o 0–100). Insumo del score.</summary>
    [Range(0, 100)]
    public decimal? ProbabilidadCierre { get; set; }

    /// <summary>Valor económico estimado en COP. Insumo del score.</summary>
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? ValorEstimado { get; set; }

    /// <summary>Score de prioridad calculado y persistido (0–100). Solo lectura para el API.</summary>
    [Range(0, 100)]
    public decimal ScorePrioridad { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [Required]
    public Guid CreadoPor { get; set; }

    [JsonIgnore]
    public Usuario? Creador { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Token de concurrencia optimista (rowversion).</summary>
    public byte[]? RowVersion { get; set; }

    [JsonIgnore]
    public ICollection<LineaSolicitud> Lineas { get; set; } = new List<LineaSolicitud>();

    [JsonIgnore]
    public ICollection<Cotizacion> Cotizaciones { get; set; } = new List<Cotizacion>();
}
