using System.Security.Claims;
using System.Text.Encodings.Web;
using Nexo.Api.Common.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Nexo.Api.Common.Auth;

/// <summary>
/// Esquema de servicio-a-servicio para el microservicio Cubicador (Python): valida
/// "Authorization: Bearer &lt;clsCubicador3D:InboundApiKey&gt;" en vez de la sesión del
/// portal — no identifica una persona ni un rol de negocio, solo autoriza "quién puede
/// llamar este endpoint" (ver cubicador/CONTRATO.md sección 7). Mismo valor que
/// NEXO_CALLBACK_API_KEY en el .env del Cubicador — es el mismo secreto compartido,
/// visto desde cada lado.
///
/// Fail-closed: sin key configurada, sin header, o con un valor que no coincide, la
/// petición NO se autentica (401) — nunca cae al esquema de identidad del portal, que
/// no tiene sentido para una llamada de servicio a servicio.
/// </summary>
public class clsCubicadorApiKeyAuthenticationHandler(
    IConfiguration configuration,
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CubicadorApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var keyEsperada = configuration["Cubicador3D:InboundApiKey"];
        if (string.IsNullOrEmpty(keyEsperada))
            return Task.FromResult(AuthenticateResult.NoResult());

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || header["Bearer ".Length..] != keyEsperada)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[] { new Claim(ClaimTypes.Name, "Cubicador") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        clsErrorHandlingMiddleware.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            "NO_AUTORIZADO", "Bearer inválido o ausente.");
}
