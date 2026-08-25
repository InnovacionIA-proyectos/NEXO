using CabinaFletes.Domain.Common;

namespace CabinaFletes.Domain.Solicitudes.Entidades;

/// <summary>
/// Una línea que el servicio no pudo costear porque falta una tarifa, y espera a que el
/// Administrador de Tarifas la agregue.
///
/// Es una entidad y no un contador porque el contador no sirve para actuar. "3 líneas
/// esperando" no le dice a nadie qué hacer; "la línea Bogotá→Cali de tracto no tiene
/// tarifa de plaza vigente" sí, y le dice a quién ir a buscar.
///
/// No lleva reloj a propósito. Esta espera depende de una persona que puede tener otros
/// pliegos delante, y ponerle un vencimiento solo serviría para marcar como fallido algo
/// que estaba esperando bien.
/// </summary>
public class LineaEsperandoTarifa : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Trabajo de auto-costeo en el que se detectó la falta.</summary>
    public Guid TrabajoId { get; set; }

    public Guid SolicitudId { get; set; }
    public Guid LineaSolicitudId { get; set; }

    /// <summary>Qué falta, en palabras. Es lo que el Administrador de Tarifas necesita leer.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Nulo mientras sigue esperando. Se marca cuando la tarifa aparece y la línea se
    /// vuelve a costear, así queda el rastro de cuánto tardó la intervención humana.
    /// </summary>
    public DateTimeOffset? ResueltaEn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
