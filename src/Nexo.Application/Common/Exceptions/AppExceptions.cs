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

// No son sealed a propósito: el middleware de Api traduce por TIPO, así que las excepciones de un contexto
// deben heredar de una de estas para conservar su código HTTP (heredar de clsAppException daría 500).

/// <summary>El recurso no existe (Api la traduce a 404).</summary>
public class clsNotFoundException(string code, string message) : clsAppException(code, message);

/// <summary>Input inválido por una regla de negocio (Api la traduce a 400).</summary>
public class clsValidationFailedException(string code, string message, object? details = null)
    : clsAppException(code, message, details);

/// <summary>Conflicto con el estado actual del recurso (Api la traduce a 409).</summary>
public class clsConflictException(string code, string message) : clsAppException(code, message);

/// <summary>Una dependencia externa no está configurada o no responde (Api la traduce a 503).</summary>
public class clsServiceUnavailableException(string code, string message) : clsAppException(code, message);

/// <summary>Se superó un tope de uso configurado; reintentar después sí funciona (Api la traduce a 429).</summary>
public class clsLimiteExcedidoException(string code, string message, object? details = null) : clsAppException(code, message, details);

/// <summary>El usuario autenticado no es dueño del recurso (Api la traduce a 403).</summary>
public class clsForbiddenException(string code, string message) : clsAppException(code, message);
