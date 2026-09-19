using System.Security.Claims;
using System.Text.Encodings.Web;
using CabinaFletes.Api.Common.Errors;
using CabinaFletes.Api.Common.Middleware;
using CabinaFletes.Application.Common.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CabinaFletes.Api.Common.Auth;

/// <summary>
/// S2-HU-03.1/03.2: valida en CADA petición el artefacto de sesión que define el
/// contrato de integración con el portal (docs/contrato-integracion, D6) — el
/// encabezado X-Portal-Usuario-Id — y resuelve la identidad real contra
/// <see cref="IResolverIdentidadPortalPort"/>. Reemplaza a DevAuthenticationHandler y al
/// esquema JwtBearer de Azure AD, retirados en S2-HU-03.3: no hay más rol declarado por
/// el cliente (X-Rol) ni credenciales de ningún tipo pasando por Nexo.
///
/// Autenticación vs. autorización, a propósito separadas: esta clase decide si la
/// IDENTIDAD es válida (401 si no). Que tenga un rol de Nexo válido lo exige la
/// FallbackPolicy en Program.cs (RequireClaim de rol) — por eso una persona sin permiso
/// sobre Cabina de Fletes sí se autentica aquí (sin Claim de rol) pero cae en Forbid,
/// nunca en Challenge: así el 403 aplica a TODO endpoint, no solo a los que declaran
/// [Authorize(Roles=...)].
/// </summary>
public class IdentidadPortalAuthenticationHandler(
    IResolverIdentidadPortalPort resolver,
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IdentidadPortal";
    public const string HeaderIdUsuario = "X-Portal-Usuario-Id";

    /// <summary>Claim con el id CRUDO del portal (el del encabezado), distinto del
    /// NameIdentifier interno (Usuario.Id, Guid usado para la FK de auditoría). Lo
    /// expone /api/sesion/actual: el contrato define ese campo como el id del portal,
    /// no un id interno de Nexo que el frontend no conoce.</summary>
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

        // Sin rol de Nexo válido: SÍ se autentica (para que la FallbackPolicy decida
        // Forbid, no Challenge — ver el comentario de la clase), pero sin Claim de rol.
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

    /// <summary>401: sin identidad utilizable. Nunca dice si el id existe o no en la
    /// tabla del portal (docs/contrato-integracion, sección 6) — mismo mensaje genérico
    /// siempre, para no filtrar información del sistema (S2-HU-03.1).</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ErrorHandlingMiddleware.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            "SESION_INVALIDA", "La sesión no es válida. Ingrese nuevamente desde el portal.");

    /// <summary>403: identidad válida, sin permiso sobre Cabina de Fletes o rol
    /// insuficiente para la operación.</summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ErrorHandlingMiddleware.WriteAsync(Context, StatusCodes.Status403Forbidden,
            "SIN_PERMISO_CABINA_FLETES", "Su usuario no tiene permiso para esta operación en Cabina de Fletes.");
}
