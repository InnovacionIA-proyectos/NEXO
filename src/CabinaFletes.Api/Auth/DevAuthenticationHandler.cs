using System.Security.Claims;
using System.Text.Encodings.Web;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CabinaFletes.Api.Auth;

/// <summary>
/// Esquema de autenticación de desarrollo: autentica cada petición como el usuario de
/// sistema, mientras no se cablee Azure AD (OIDC/JWT). La arquitectura define autenticación
/// SAML 2.0 / OIDC contra Azure AD: en producción se activa el esquema JwtBearer
/// (ver Program.cs) cuando hay configuración 'AzureAd'.
/// </summary>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Dev";

    /// <summary>
    /// Header con el rol simulado en desarrollo (lo envía el frontend según el perfil elegido
    /// en el login). En producción el rol vendrá de los grupos de Azure AD. Fail-closed
    /// (QA-SEC-001): sin header o con un rol NO válido, la petición queda SIN autenticar (401),
    /// en vez de otorgar el máximo privilegio (Admin) por defecto.
    /// </summary>
    public const string RolHeader = "X-Rol";

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Fail-closed: solo se autentica si llega un rol VÁLIDO. Header ausente/vacío o valor
        // que no corresponde a un RolUsuario => no autenticado (el pipeline responde 401).
        if (!Request.Headers.TryGetValue(RolHeader, out var valor) ||
            !Enum.TryParse<CabinaFletes.Domain.Enums.RolUsuario>(valor.ToString(), ignoreCase: true, out var parseado))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var rol = parseado.ToString();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, CabinaFletesDbContext.UsuarioSistemaId.ToString()),
            new Claim(ClaimTypes.Name, $"Sistema ({rol})"),
            new Claim(ClaimTypes.Role, rol),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
