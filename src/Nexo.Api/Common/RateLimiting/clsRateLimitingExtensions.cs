using System.Security.Claims;
using System.Threading.RateLimiting;
using Nexo.Api.Common.Middleware;
using Nexo.Domain.Enums;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Common.RateLimiting;

/// <summary>
/// Rate limiting básico (por rol, con IP como respaldo para quien no tiene
/// identidad resuelta) en los endpoints de listado más pesados. Usa el
/// middleware nativo de ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`, parte
/// del framework desde .NET 7 — no agrega ningún paquete NuGet nuevo).
///
/// Dos políticas, no una sola para todos los endpoints:
/// - <see cref="PoliticaListadosPesados"/>: biblioteca-sicetac (~2.15M filas,
///   Data Warehouse externo) y biblioteca-sap-plaza (~22.8K filas, datacenter
///   externo) — el riesgo no es solo la carga de NEXO, es saturar una base
///   corporativa COMPARTIDA con otros consumidores (ver
///   docs/decisiones/paginacion-endpoints-listado.md). Límite estricto,
///   ventana deslizante para evitar el efecto ráfaga en el borde de la ventana.
/// - <see cref="PoliticaListadosEstandar"/>: el resto de los listados
///   (solicitudes, planes-cargue, tarifas-historicas, vehiculos,
///   reglas-solo-sicetac, biblioteca-sap-historico) — tablas propias de NEXO,
///   volumen bajo medido, límite más permisivo.
/// </summary>
public static class clsRateLimitingExtensions
{
    public const string PoliticaListadosPesados = "listados-pesados";
    public const string PoliticaListadosEstandar = "listados-estandar";

    public static IServiceCollection AddRateLimitingNexo(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Mismo shape de error que el resto de la API (clsApiError, ver
            // clsErrorHandlingMiddleware) - el rate limiter corre DESPUÉS de ese
            // middleware en el pipeline, así que un 429 emitido acá no pasa por
            // él: hay que escribir la respuesta explícitamente.
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
