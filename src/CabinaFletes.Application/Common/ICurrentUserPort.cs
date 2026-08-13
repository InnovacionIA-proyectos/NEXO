using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Common;

/// <summary>
/// Usuario autenticado de la operación actual, visto desde Application (sin depender
/// de HttpContext ni de ningún tipo de ASP.NET Core). El adapter concreto vive en Api.
/// </summary>
public interface ICurrentUserPort
{
    Guid Id { get; }
    string? Nombre { get; }
    RolUsuario? Rol { get; }
}
