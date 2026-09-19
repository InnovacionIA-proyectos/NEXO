using System.Security.Claims;
using Nexo.Application.Common;
using Nexo.Domain.Enums;

namespace Nexo.Api.Common.Auth;

/// <summary>
/// clsUsuario autenticado de la petición actual. Extiende el puerto de Application
/// (ICurrentUserPort) — así los Handlers lo consumen sin conocer HttpContext.
///
/// Lee directo los claims que deja clsIdentidadPortalAuthenticationHandler
/// (NameIdentifier/Name/Role, ya resueltos contra la tabla del portal en esta misma
/// petición — S2-HU-03.1/03.2). Sin fallback a un usuario de sistema: el fail-closed de
/// clsProgram.cs garantiza que si se llegó hasta un Handler, la petición ya tiene una
/// identidad real.
/// </summary>
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
