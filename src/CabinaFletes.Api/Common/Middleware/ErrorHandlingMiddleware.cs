using CabinaFletes.Api.Common.Errors;
using CabinaFletes.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Api.Common.Middleware;

/// <summary>
/// Middleware global de manejo de errores. Traduce las excepciones de Application
/// (que no conocen HTTP) al código de estado correspondiente, y captura cualquier
/// excepción no controlada. Responde SIEMPRE en JSON con { code, message, timestamp }.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "{Code} en {Method} {Path}", ex.Code, context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.Code, ex.Message, ex.Details);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogWarning(ex, "{Code} en {Method} {Path}", ex.Code, context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Code, ex.Message, ex.Details);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning(ex, "{Code} en {Method} {Path}", ex.Code, context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status409Conflict, ex.Code, ex.Message, ex.Details);
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "{Code} en {Method} {Path}", ex.Code, context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status503ServiceUnavailable, ex.Code, ex.Message, ex.Details);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Concurrencia optimista (rowversion): otro usuario modificó el registro primero.
            _logger.LogWarning(ex, "Conflicto de concurrencia en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status409Conflict, "CONFLICTO_CONCURRENCIA",
                "El registro fue modificado por otro usuario. Recargue y vuelva a intentar.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(
                context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                _env.IsDevelopment() ? ex.Message : "Ocurrió un error inesperado.");
        }
    }

    public static async Task WriteAsync(HttpContext context, int statusCode, string code, string message, object? details = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiError(code, message, DateTimeOffset.UtcNow, details));
    }
}
