using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Solicitudes.Entidades;

/// <summary>
/// Una línea que el servicio no pudo costear, con el porqué. Un solo sitio para las dos razones
/// (<see cref="Tipo"/>): la técnica se reintenta y se resuelve sola; la manual espera a una persona y no lleva reloj.
/// </summary>
public class clsLineaPendienteDeCosteo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Trabajo de auto-costeo en el que se detectó.</summary>
    public Guid TrabajoId { get; set; }

    public Guid SolicitudId { get; set; }
    public Guid LineaSolicitudId { get; set; }

    public TipoPendienteDeCosteo Tipo { get; set; }

    /// <summary>Qué pasó: para Manual, lo que debe leer el Administrador de Tarifas; para Tecnica, el diagnóstico de la fuente.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Nulo mientras sigue pendiente; se marca al resolverse (reintento o intervención humana).</summary>
    public DateTimeOffset? ResueltaEn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
