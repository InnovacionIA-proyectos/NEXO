using System.Security.Claims;
using CabinaFletes.Application.Common;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Usuarios.Entidades;
using CabinaFletes.Infrastructure.Persistence;

namespace CabinaFletes.Api.Auth;

/// <summary>
/// Usuario autenticado de la petición actual. Extiende el puerto de Application
/// (ICurrentUserPort) — así los Handlers lo consumen sin conocer HttpContext.
/// </summary>
public interface ICurrentUser : ICurrentUserPort
{
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public Guid Id
    {
        get
        {
            var principal = _http.HttpContext?.User;
            // 'oid' es el claim de object id de Azure AD; NameIdentifier en el esquema Dev.
            var sub = principal?.FindFirst("oid")?.Value
                      ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : CabinaFletesDbContext.UsuarioSistemaId;
        }
    }

    public string? Nombre => _http.HttpContext?.User?.Identity?.Name;

    public RolUsuario? Rol =>
        Enum.TryParse<RolUsuario>(_http.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value, out var r) ? r : null;
}
