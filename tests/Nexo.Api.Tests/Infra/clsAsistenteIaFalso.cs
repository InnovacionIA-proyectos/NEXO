using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Reemplazo de <see cref="IAsistenteIaPort"/> para pruebas: no llama a ningún proveedor. Devuelve la respuesta programada (o
/// lanza el error programado) y deja anotadas las peticiones recibidas, para que las pruebas de las funciones de IA verifiquen
/// qué se le pidió al modelo sin gastar nada ni depender de la red.
/// </summary>
public sealed class clsAsistenteIaFalso : IAsistenteIaPort
{
    private readonly HashSet<clsFuncionIa> _habilitadas;
    private readonly Func<clsPeticionIa, clsRespuestaIa>? _responder;
    private readonly Exception? _error;

    private clsAsistenteIaFalso(IEnumerable<clsFuncionIa> habilitadas, Func<clsPeticionIa, clsRespuestaIa>? responder, Exception? error)
    {
        _habilitadas = [.. habilitadas];
        _responder = responder;
        _error = error;
    }

    /// <summary>Peticiones recibidas, en orden.</summary>
    public List<clsPeticionIa> Peticiones { get; } = [];

    /// <summary>Responde siempre con <paramref name="texto"/> (y, si la petición pidió un esquema, ese mismo texto como JSON).</summary>
    public static clsAsistenteIaFalso Responde(string texto, params clsFuncionIa[] habilitadas) =>
        new(habilitadas.Length == 0 ? Enum.GetValues<clsFuncionIa>() : habilitadas,
            p => new clsRespuestaIa(texto, p.EsquemaSalidaJson is null ? null : texto, "modelo-de-prueba", 10, 5, TimeSpan.FromMilliseconds(1)),
            error: null);

    /// <summary>Falla siempre con el código dado, como lo haría el adaptador real.</summary>
    public static clsAsistenteIaFalso Falla(string codigo, params clsFuncionIa[] habilitadas) =>
        new(habilitadas.Length == 0 ? Enum.GetValues<clsFuncionIa>() : habilitadas, responder: null,
            new clsServiceUnavailableException(codigo, "Error simulado del proveedor de IA."));

    /// <summary>Lanza siempre <paramref name="error"/> (por ejemplo un tope de uso superado), como lo haría el puerto real.</summary>
    public static clsAsistenteIaFalso Lanza(Exception error, params clsFuncionIa[] habilitadas) =>
        new(habilitadas.Length == 0 ? Enum.GetValues<clsFuncionIa>() : habilitadas, responder: null, error);

    /// <summary>Como un ambiente sin llave: nada habilitado y toda llamada responde <c>IA_NO_CONFIGURADA</c>.</summary>
    public static clsAsistenteIaFalso SinConfigurar() =>
        new([], responder: null, new clsServiceUnavailableException("IA_NO_CONFIGURADA", "La función de IA no está configurada en este ambiente."));

    public bool Habilitada(clsFuncionIa funcion) => _habilitadas.Contains(funcion);

    public Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        Peticiones.Add(peticion);
        if (_error is not null) throw _error;
        return Task.FromResult(_responder!(peticion));
    }
}
