using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Application.Common;

/// <summary>Usuario autenticado de la operación actual, sin depender de HttpContext ni de ASP.NET Core.</summary>
public interface ICurrentUserPort
{
    Guid Id { get; }
    string? Nombre { get; }
    RolUsuario? Rol { get; }
}
