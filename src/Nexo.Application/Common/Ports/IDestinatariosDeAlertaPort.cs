using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

/// <summary>
/// A quién avisar cuando pasa algo que un rol necesita saber (tarifa vencida, línea que
/// no se pudo costear, cotización ganada) — no una solicitud puntual con un creador
/// identificable, sino una alerta de negocio para todo el rol. Vive en Common porque lo
/// usan varias notificaciones nuevas (issue #131, sección 9 del documento de
/// entendimiento), ninguna atada a un módulo en particular.
/// </summary>
public interface IDestinatariosDeAlertaPort
{
    /// <summary>Correos de los usuarios activos con ese rol. Vacío si no hay ninguno.</summary>
    Task<IReadOnlyList<string>> EmailsPorRolAsync(RolUsuario rol, CancellationToken ct = default);
}
