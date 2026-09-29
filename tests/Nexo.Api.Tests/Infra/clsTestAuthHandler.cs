using System.Security.Claims;
using System.Text.Encodings.Web;
using Nexo.Api.Common.Auth;
using Nexo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Auth de prueba: autentica solo si llega el header X-Test-Role (simulando un token).
/// Sin header -> NoResult -> 401. Con header -> usuario de sistema con el rol indicado
/// (permite probar 403 pasando un rol sin permiso para el endpoint).
///
/// X-Test-UserId (opcional, issue #199 S4-HU-19.1): simula un usuario Nexo distinto del
/// de sistema por defecto — necesario para probar aislamiento por dueño del recurso
/// (dos Comerciales con el mismo rol pero Id distinto). Sin el header, se comporta igual
/// que antes (compatibilidad con los tests existentes).
/// </summary>
public class clsTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";
    public const string UserIdHeader = "X-Test-UserId";

    public clsTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role) || string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = Request.Headers.TryGetValue(UserIdHeader, out var uid) && !string.IsNullOrWhiteSpace(uid)
            ? uid.ToString()
            : clsNexoDbContext.UsuarioSistemaId.ToString();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, role.ToString()),
            // Id "crudo del portal" simulado, para que clsSesionController (S2-HU-03.1) sea
            // ejercitable en este harness igual que con el esquema real.
            new Claim(clsIdentidadPortalAuthenticationHandler.ClaimIdPortal, "1"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
