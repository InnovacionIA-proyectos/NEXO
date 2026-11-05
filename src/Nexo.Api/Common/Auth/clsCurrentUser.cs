using System.Security.Claims;
using Nexo.Application.Common;
using Nexo.Domain.Enums;

namespace Nexo.Api.Common.Auth;

/// <summary>Usuario autenticado de la petición actual, leído de los claims que deja clsIdentidadPortalAuthenticationHandler. Sin fallback a un usuario de sistema.</summary>
public interface ICurrentUser : ICurrentUserPort
{
}

public class clsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public clsCurrentUser(IHttpContextAccessor http) => _http = http;

    public Guid Id
    {
        get
        {
            var sub = _http.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException(
                    "ICurrentUser.Id se consultó sin una identidad válida en el contexto de la petición.");
        }
    }

    public string? Nombre => _http.HttpContext?.User?.Identity?.Name;

    public RolUsuario? Rol =>
        Enum.TryParse<RolUsuario>(_http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value, out var r) ? r : null;
}
