using System.Security.Claims;
using System.Text.Encodings.Web;
using Nexo.Api.Common.Errors;
using Nexo.Api.Common.Middleware;
using Nexo.Application.Common.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Nexo.Api.Common.Auth;

/// <summary>
/// Valida en cada petición el encabezado X-Portal-clsUsuario-Id y resuelve la identidad real con <see cref="IResolverIdentidadPortalPort"/>.
/// Solo decide si la identidad es válida (401 si no): el rol de Nexo lo exige la FallbackPolicy, así que quien no tiene permiso se autentica sin claim de rol y cae en Forbid (403), nunca en Challenge.
/// </summary>
public class clsIdentidadPortalAuthenticationHandler(
    IResolverIdentidadPortalPort resolver,
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "clsIdentidadPortal";
    public const string HeaderIdUsuario = "X-Portal-clsUsuario-Id";

    /// <summary>Claim con el id crudo del portal (el del encabezado), distinto del NameIdentifier interno. Lo expone /api/sesion/actual.</summary>
    public const string ClaimIdPortal = "idPortal";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderIdUsuario, out var valor) ||
            !long.TryParse(valor.ToString(), out var idPortal))
        {
            return AuthenticateResult.NoResult();
        }

        var identidad = await resolver.ResolverAsync(idPortal);
        if (identidad is null) return AuthenticateResult.NoResult();

        // Sin rol de Nexo válido sí se autentica (la FallbackPolicy decide Forbid, no Challenge), pero sin claim de rol.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identidad.UsuarioId?.ToString() ?? idPortal.ToString()),
            new(ClaimTypes.Name, identidad.Nombre),
            new(ClaimIdPortal, idPortal.ToString()),
        };
        if (identidad.Rol is not null) claims.Add(new Claim(ClaimTypes.Role, identidad.Rol.Value.ToString()));

        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>401: sin identidad utilizable. Siempre el mismo mensaje genérico, para no revelar si el id existe en la tabla del portal.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        clsErrorHandlingMiddleware.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            "SESION_INVALIDA", "La sesión no es válida. Ingrese nuevamente desde el portal.");

    /// <summary>403: identidad válida sin permiso sobre Nexo o con rol insuficiente para la operación.</summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        clsErrorHandlingMiddleware.WriteAsync(Context, StatusCodes.Status403Forbidden,
            "SIN_PERMISO_CABINA_FLETES", "Su usuario no tiene permiso para esta operación en Nexo.");
}
