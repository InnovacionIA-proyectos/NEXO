using Nexo.Application.Common.Ports;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Reemplazo de <see cref="IConversacionIaPort"/> para pruebas: no llama a ningún proveedor. Corre el guion que se le da (que decide qué
/// texto y qué herramientas "pide el modelo") y deja anotadas las peticiones, para verificar qué se le ofreció al modelo.
/// </summary>
public sealed class clsConversacionIaFalsa(
    Func<clsPeticionConversacionIa, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>>, CancellationToken, IAsyncEnumerable<clsEventoConversacionIa>> guion)
    : IConversacionIaPort
{
    /// <summary>Si tiene valor, <see cref="VerificarAsync"/> lo lanza (función apagada, tope superado…).</summary>
    public Exception? ErrorAlVerificar { get; set; }

    public List<clsPeticionConversacionIa> Peticiones { get; } = [];

    public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default) =>
        ErrorAlVerificar is null ? Task.CompletedTask : Task.FromException(ErrorAlVerificar);

    public IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion,
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
        CancellationToken ct = default)
    {
        Peticiones.Add(peticion);
        return guion(peticion, ejecutarHerramienta, ct);
    }
}
