namespace CabinaFletes.Application.Common.Exceptions;

/// <summary>
/// Excepción de un caso de uso. Application no conoce HTTP: no lleva StatusCode.
/// Es el middleware de Api quien traduce el TIPO de excepción a un código HTTP.
/// </summary>
public abstract class AppException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    protected AppException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }
}

/// <summary>El recurso no existe (Api la traduce a 404).</summary>
public sealed class NotFoundException(string code, string message) : AppException(code, message);

/// <summary>Input inválido por una regla de negocio (Api la traduce a 400).</summary>
public sealed class ValidationFailedException(string code, string message, object? details = null)
    : AppException(code, message, details);

/// <summary>Conflicto con el estado actual del recurso (Api la traduce a 409).</summary>
public sealed class ConflictException(string code, string message) : AppException(code, message);

/// <summary>Una dependencia externa no está configurada o no responde (Api la traduce a 503).</summary>
public sealed class ServiceUnavailableException(string code, string message) : AppException(code, message);
