namespace Nexo.Infrastructure.IA;

/// <summary>
/// Precios de lista del proveedor, en dólares por millón de tokens, para estimar el costo de una llamada al registrarla.
/// Se mantienen a mano: un modelo que no esté aquí queda sin costo estimado (null) en vez de inventar uno. Son precios de
/// lista de 2026-10; no incluyen descuentos del contrato ni la tarifa de los tramos de contexto largo.
/// </summary>
public static class clsPreciosIa
{
    private sealed record clsPrecio(decimal Entrada, decimal Salida, decimal CacheLectura);

    private static readonly Dictionary<string, clsPrecio> Precios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-sonnet-5-5"] = new(2m, 10m, 0.20m),
        ["claude-sonnet-5"] = new(2m, 10m, 0.20m),
        ["claude-sonnet-4-6"] = new(3m, 15m, 0.30m),
        ["claude-haiku-5-5"] = new(0.10m, 0.50m, 0.01m),
        ["claude-opus-5-5"] = new(4m, 20m, 0.20m),
    };

    /// <summary>Escribir en el caché de prompt (5 minutos) cuesta 1,25 veces el precio de entrada.</summary>
    private const decimal FactorEscrituraCache = 1.25m;

    private const decimal TokensPorMillon = 1_000_000m;

    /// <summary>Costo estimado en dólares, o null si el modelo no tiene precio conocido.</summary>
    public static decimal? Estimar(string modelo, long entrada, long salida, long cacheLectura, long cacheEscritura)
    {
        if (!Precios.TryGetValue(modelo.Trim(), out var p)) return null;
        var costo = (entrada * p.Entrada + salida * p.Salida + cacheLectura * p.CacheLectura
                     + cacheEscritura * p.Entrada * FactorEscrituraCache) / TokensPorMillon;
        return Math.Round(costo, 6, MidpointRounding.AwayFromZero);
    }
}
