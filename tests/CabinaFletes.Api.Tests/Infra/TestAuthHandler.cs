using System.Security.Claims;
using System.Text.Encodings.Web;
using CabinaFletes.Api.Common.Auth;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CabinaFletes.Api.Tests.Infra;

/// <summary>
/// Auth de prueba: autentica solo si llega el header X-Test-Role (simulando un token).
/// Sin header -> NoResult -> 401. Con header -> usuario de sistema con el rol indicado
/// (permite probar 403 pasando un rol sin permiso para el endpoint).
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role) || string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, CabinaFletesDbContext.UsuarioSistemaId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, role.ToString()),
            // Id "crudo del portal" simulado, para que SesionController (S2-HU-03.1) sea
            // ejercitable en este harness igual que con el esquema real.
            new Claim(IdentidadPortalAuthenticationHandler.ClaimIdPortal, "1"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
