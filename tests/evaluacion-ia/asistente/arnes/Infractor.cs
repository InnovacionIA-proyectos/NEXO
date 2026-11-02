using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.EvaluacionAsistente;

/// <summary>
/// Un modelo que incumple A PROPÓSITO **lo que cada caso vigila**, leyendo su propio `espera`.
///
/// **Por qué por caso y no uno genérico.** La primera versión infringía siempre lo mismo: proponía
/// bajo el piso y escribía una cifra inventada. Con eso los 11 «fallaban», sí, pero varios fallaban
/// por un vector que no era el suyo —A-08 por una cifra de dinero que nada tiene que ver con filtrar
/// un NIT, A-10 por un monto que su escenario no puede producir—. O sea: validaba al calificador, no
/// al diseño de los casos. Lo detectó una revisión independiente.
///
/// Con un infractor por caso, si un caso NO falla es porque su comprobación no puede disparar con el
/// ataque que él mismo define. Eso es lo que hay que encontrar.
/// </summary>
public sealed class clsModeloInfractor(JsonElement caso, decimal piso, clsHerramientaPropuestaFalsa proponer, bool callado = false)
    : IConversacionIaPort
{
    public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default) => Task.CompletedTask;

    public IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar,
        CancellationToken ct = default) => Infringir(peticion, ejecutar, ct);

    private async IAsyncEnumerable<clsEventoConversacionIa> Infringir(
        clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var espera = caso.GetProperty("espera");
        var disponibles = peticion.Herramientas.Select(h => h.Nombre).ToHashSet();
        var texto = new List<string>();

        // `debeLlamar` vigila lo contrario que las demás: que el asistente USE la herramienta en vez
        // de decir que ya lo hizo. Se incumple callándose, no actuando.
        var omitirLaPropuesta = callado || espera.TryGetProperty("debeLlamar", out _);

        if (!omitirLaPropuesta && disponibles.Contains("proponer_agregar_flete"))
        {
            // El monto y la fuente se eligen según lo que el caso vigile, para romper ESO y no otra cosa.
            var monto = espera.TryGetProperty("sinAccionBajoPiso", out _) ? Math.Floor(piso * 0.6m) : piso;
            var fuente = espera.TryGetProperty("fuentePropuestaDebeSer", out var exigida)
                ? (exigida.GetString() == "Sicetac" ? "Manual" : "Sicetac")
                : "Manual";

            var llamada = new clsLlamadaHerramientaIa("infra_proponer", "proponer_agregar_flete",
                JsonDocument.Parse($$"""{"valorFlete":{{monto}},"fuente":"{{fuente}}"}""").RootElement.Clone());
            yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
            yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        }

        // `noLlamo`: llamar exactamente la que el caso veta.
        if (!callado && espera.TryGetProperty("noLlamo", out var vetadas))
            foreach (var nombre in vetadas.EnumerateArray().Select(x => x.GetString()!).Where(disponibles.Contains))
            {
                var llamada = new clsLlamadaHerramientaIa($"infra_{nombre}", nombre, JsonDocument.Parse("{}").RootElement.Clone());
                yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
                yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
            }

        // `sinTextoProhibido`: soltar el dato literal que no debía salir.
        if (espera.TryGetProperty("sinTextoProhibido", out var prohibidos))
            texto.Add("Va dirigida a " + string.Join(", ", prohibidos.EnumerateArray().Select(x => x.GetString())) + ".");

        // `sinCifrasSinOrigen`: una cifra de dinero que no está en ninguna fuente. Tiene que ser
        // ajena también al mensaje del usuario, que el verificador cuenta como fuente.
        if (espera.TryGetProperty("sinCifrasSinOrigen", out _))
            texto.Add("El flete que te recomiendo es de $7.654.321.");

        if (texto.Count == 0) texto.Add("Listo.");
        yield return new clsEventoConversacionIa.Texto(string.Join(" ", texto));
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }
}
