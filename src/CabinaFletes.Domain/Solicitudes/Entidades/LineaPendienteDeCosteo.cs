using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Domain.Solicitudes.Entidades;

/// <summary>
/// Una línea que el servicio no pudo costear, con el porqué.
///
/// Un solo sitio para las dos razones. La alternativa era un campo de motivo en la
/// línea para el fallo técnico y esta tabla para la espera humana, y dos sitios
/// contando lo mismo acaban contradiciéndose.
///
/// Es una entidad y no un contador porque el contador no sirve para actuar. "3 líneas
/// pendientes" no le dice a nadie qué hacer; "la línea Bogotá→Cali de tracto no tiene
/// tarifa de plaza vigente" sí, y le dice a quién ir a buscar.
///
/// Las dos razones NO se manejan igual, y de ahí que el tipo sea parte de la entidad:
/// una se reintenta y se resuelve sola, la otra espera a una persona y no lleva reloj.
/// </summary>
public class LineaPendienteDeCosteo : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Trabajo de auto-costeo en el que se detectó.</summary>
    public Guid TrabajoId { get; set; }

    public Guid SolicitudId { get; set; }
    public Guid LineaSolicitudId { get; set; }

    public TipoPendienteDeCosteo Tipo { get; set; }

    /// <summary>
    /// Qué pasó, en palabras. Para el tipo Manual es lo que el Administrador de Tarifas
    /// necesita leer; para Tecnica es el diagnóstico de la fuente que falló.
    /// </summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Nulo mientras sigue pendiente. Se marca cuando la línea se resuelve, así queda
    /// el rastro de cuánto tardó — sea un reintento o una intervención humana.
    /// </summary>
    public DateTimeOffset? ResueltaEn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
