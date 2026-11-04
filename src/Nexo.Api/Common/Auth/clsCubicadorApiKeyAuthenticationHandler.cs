using System.Security.Claims;
using System.Text.Encodings.Web;
using Nexo.Api.Common.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Nexo.Api.Common.Auth;

/// <summary>
/// Esquema servicio-a-servicio para el Cubicador (Python): valida "Authorization: Bearer &lt;Cubicador3D:InboundApiKey&gt;" (mismo secreto que NEXO_CALLBACK_API_KEY del Cubicador).
/// No identifica una persona ni un rol. Fail-closed: sin key configurada, sin header o con valor distinto responde 401 y nunca cae al esquema del portal.
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
