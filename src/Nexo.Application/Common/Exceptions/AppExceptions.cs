namespace Nexo.Application.Common.Exceptions;

/// <summary>
/// Excepción de un caso de uso. Application no conoce HTTP: no lleva StatusCode.
/// Es el middleware de Api quien traduce el TIPO de excepción a un código HTTP.
/// </summary>
public abstract class clsAppException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    protected clsAppException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }
}

// Las cuatro NO son sealed a propósito. El middleware de Api traduce por TIPO, así que
// una excepción propia de un contexto tiene que heredar de una de estas cuatro para
// seguir saliendo con su código HTTP correcto. Heredar de clsAppException directamente la
// dejaría caer en el catch genérico y respondería 500 en vez de 400.
// Ver CasosDeUso/<Contexto>/Excepciones/.

/// <summary>El recurso no existe (Api la traduce a 404).</summary>
public class clsNotFoundException(string code, string message) : clsAppException(code, message);

/// <summary>Input inválido por una regla de negocio (Api la traduce a 400).</summary>
public class clsValidationFailedException(string code, string message, object? details = null)
    : clsAppException(code, message, details);

/// <summary>Conflicto con el estado actual del recurso (Api la traduce a 409).</summary>
public class clsConflictException(string code, string message) : clsAppException(code, message);

/// <summary>Una dependencia externa no está configurada o no responde (Api la traduce a 503).</summary>
public class clsServiceUnavailableException(string code, string message) : clsAppException(code, message);
