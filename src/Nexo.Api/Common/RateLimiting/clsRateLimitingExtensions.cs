using System.Security.Claims;
using System.Threading.RateLimiting;
using Nexo.Api.Common.Middleware;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Common.RateLimiting;

/// <summary>
/// Rate limiting (por rol, con IP de respaldo) en los listados más pesados, con el middleware nativo de ASP.NET Core. Dos políticas:
/// <see cref="PoliticaListadosPesados"/>: biblioteca-sicetac y biblioteca-sap-plaza (bases corporativas compartidas); límite estricto con ventana deslizante.
/// <see cref="PoliticaListadosEstandar"/>: el resto de los listados (tablas propias de NEXO); límite más permisivo.
/// </summary>
public static class clsRateLimitingExtensions
{
    public const string PoliticaListadosPesados = "listados-pesados";
    public const string PoliticaListadosEstandar = "listados-estandar";

    public static IServiceCollection AddRateLimitingNexo(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Mismo shape de error que el resto de la API: el rate limiter corre después de clsErrorHandlingMiddleware, así que el 429 se escribe explícitamente.
            o.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "30";
                await clsErrorHandlingMiddleware.WriteAsync(
                    context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "DEMASIADAS_SOLICITUDES",
                    "Ha alcanzado el límite de solicitudes para este recurso. Intente de nuevo en unos segundos.");
            };

            o.AddPolicy(PoliticaListadosPesados, httpContext =>
            {
                var rol = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
                var clave = rol ?? Clave(httpContext);
                // Admin/AdminTarifas necesitan explorar la matriz completa con más libertad.
                var esAdmin = rol == nameof(RolUsuario.Admin) || rol == nameof(RolUsuario.AdminTarifas);
                return RateLimitPartition.GetSlidingWindowLimiter(clave, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = esAdmin ? 60 : 20,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0, // rechazar de inmediato - no tiene sentido encolar un GET de listado
                });
            });

            o.AddPolicy(PoliticaListadosEstandar, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(Clave(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

    private static string Clave(HttpContext httpContext) =>
        httpContext.User.FindFirst(ClaimTypes.Role)?.Value
        ?? httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "anonimo";
}
