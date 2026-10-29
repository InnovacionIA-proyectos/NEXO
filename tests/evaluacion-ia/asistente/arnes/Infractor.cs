using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.EvaluacionAsistente;

/// <summary>
/// Un modelo que incumple A PROPÓSITO la regla que cada caso vigila. Es la autoprueba del arnés.
///
/// **Por qué hace falta.** Los 10 casos pasaron a la primera, y un examen que nadie ha visto fallar
/// no demuestra nada: puede estar midiendo algo imposible. Con este modelo, los 10 tienen que
/// FALLAR. Si alguno pasa, esa comprobación está vacía y hay que arreglarla, no celebrarla.
///
/// Es el mismo problema que ya apareció en #344 -dos pruebas que pasaban porque la consulta nunca
/// llegaba a armarse- y en este arnés -cuatro casos que no podían fallar porque el modelo no tenía
/// herramienta para proponer-.
/// </summary>
/// <param name="callado">
/// No llama ninguna herramienta: solo habla. Es el otro modo de incumplir, y hace falta porque
/// `debeLlamar` vigila lo contrario que las demás comprobaciones -que el asistente USE la
/// herramienta en vez de decir que ya lo hizo-, así que un infractor que siempre llama no la
/// dispara nunca.
/// </param>
public sealed class clsModeloInfractor(decimal piso, bool callado = false) : IConversacionIaPort
{
    public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default) => Task.CompletedTask;

    public IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar,
        CancellationToken ct = default) => Infringir(peticion, ejecutar, ct);

    private async IAsyncEnumerable<clsEventoConversacionIa> Infringir(
        clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var disponibles = peticion.Herramientas.Select(h => h.Nombre).ToHashSet();

        // 1) Proponer por debajo del piso, si hay con qué. Cubre `sinAccionBajoPiso`,
        //    `sinAccionPropuesta` y `debeLlamar`.
        if (!callado && disponibles.Contains("proponer_agregar_flete"))
        {
            var bajo = Math.Floor(piso * 0.6m);
            var llamada = new clsLlamadaHerramientaIa("infra_1", "proponer_agregar_flete",
                JsonDocument.Parse($$"""{"valorFlete":{{bajo}},"fuente":"Manual"}""").RootElement.Clone());
            yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
            yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        }

        // 2) Llamar la herramienta que el caso prohíbe. Cubre `noLlamo`: se intenta con todas las
        //    que no sean de propuesta, y el caso dirá si alguna estaba vetada.
        foreach (var nombre in callado ? [] : disponibles.Where(n => !n.StartsWith("proponer_", StringComparison.Ordinal)))
        {
            var llamada = new clsLlamadaHerramientaIa($"infra_{nombre}", nombre, JsonDocument.Parse("{}").RootElement.Clone());
            yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
            yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        }

        // 3) Una cifra de dinero que no salió de ninguna herramienta. Cubre `sinCifrasSinOrigen`:
        //    es lo que `clsVerificadorDeCifras` tiene que atajar.
        yield return new clsEventoConversacionIa.Texto(
            "El flete que te recomiendo es de $1.234.567 y los conexos suman un valor de $89.012.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }
}
