using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

/// <summary>A quién avisar en alertas de negocio dirigidas a todo un rol (tarifa vencida, línea sin costear, cotización ganada).</summary>
public interface IDestinatariosDeAlertaPort
{
    /// <summary>Correos de los usuarios activos con ese rol. Vacío si no hay ninguno.</summary>
    Task<IReadOnlyList<string>> EmailsPorRolAsync(RolUsuario rol, CancellationToken ct = default);
}
