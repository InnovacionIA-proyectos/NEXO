using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// El tope diario de tokens por persona, común a toda función de IA. El día es el de Colombia (medianoche en UTC-5) y se mide con lo ya
/// registrado en <c>tbl_ia_uso</c>, así que es blando: dos llamadas simultáneas pueden pasarlo por una. Un tope de 0 lo desactiva y una
/// llamada sin usuario (proceso interno) no tiene tope por persona.
/// </summary>
internal static class clsTopeDiarioIa
{
    private static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);

    public static async Task VerificarAsync(
        IUsoIaRepository uso, long limite, Guid? usuarioId, TimeProvider reloj, CancellationToken ct)
    {
        if (limite <= 0 || usuarioId is not { } id) return;

        var inicioDelDia = InicioDelDia(reloj.GetUtcNow());
        var consumidos = await uso.TokensConsumidosDesdeAsync(id, inicioDelDia, ct);
        if (consumidos < limite) return;

        throw new clsLimiteExcedidoException(
            "IA_TOPE_DIARIO",
            $"Alcanzaste el tope diario de uso de la IA ({limite:N0} tokens). Se reinicia a la medianoche, hora de Colombia.",
            new { limiteTokensPorDia = limite, tokensConsumidosHoy = consumidos, reiniciaEn = inicioDelDia.AddDays(1) });
    }

    /// <summary>Medianoche de Colombia (UTC-5) del día al que pertenece <paramref name="ahora"/>.</summary>
    public static DateTimeOffset InicioDelDia(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(DesfaseColombia);
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, DesfaseColombia);
    }
}
