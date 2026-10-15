using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Elige una mercancía que EXISTA en el Cubicador contra el que corre la prueba, por su CÓDIGO.
///
/// Las pruebas E2E del Cubicador real fijaban <c>CAJAS_CARTON</c>, una fila de la semilla del
/// stack local (<c>preparar_catalogo_local.py</c>). El Cubicador desplegado al que apunta el CI no
/// siempre la trae y respondía <c>MERCANCIA_DESCONOCIDA</c> (corrida del 2026-10-14): fallaba el dato de
/// prueba, no el contrato. Así que se consulta <c>GET /api/tipos-mercancia</c> y se usa la
/// semilla si está, y si no la primera mercancía activa y cubicable del catálogo real.
///
/// S5-HU-17.7: el contrato de <c>POST /api/cubicacion</c> recibe <c>codigoMercancia</c> (BEBIDAS,
/// CAJAS_CARTON), ya no el id (<c>m-CAJAS_CARTON</c>); por eso se devuelve el código.
/// </summary>
public static class clsMercanciaDelCubicador
{
    public const string CodigoSemilla = "CAJAS_CARTON";

    /// <param name="EsLaDeLaSemilla">Solo con la mercancía de la semilla se conoce el desenlace exacto
    /// (ASIGNADO, vehículo 2L2); con otra se afirma el contrato, no el catálogo de vehículos.</param>
    public sealed record clsMercanciaElegida(string Codigo, bool EsLaDeLaSemilla);

    public static async Task<clsMercanciaElegida> ElegirAsync(HttpClient cliente)
    {
        var catalogo = await cliente.GetFromJsonAsync<JsonArray>("/api/tipos-mercancia")
            ?? throw new InvalidOperationException("GET /api/tipos-mercancia no devolvió un catálogo.");

        static bool Usable(JsonNode? m) =>
            !string.IsNullOrWhiteSpace(m?["codigoMercancia"]?.GetValue<string>())
            && m?["activo"]?.GetValue<bool>() != false
            && m?["cubicable"]?.GetValue<bool>() != false
            && m?["condicionadaCubicacion"]?.GetValue<bool>() != true;

        var semilla = catalogo.FirstOrDefault(m => m?["codigoMercancia"]?.GetValue<string>() == CodigoSemilla && Usable(m));
        if (semilla is not null) return new clsMercanciaElegida(CodigoSemilla, true);

        var otra = catalogo.FirstOrDefault(Usable)?["codigoMercancia"]?.GetValue<string>()
            ?? throw new InvalidOperationException("El catálogo del Cubicador no trae ninguna mercancía activa y cubicable.");
        return new clsMercanciaElegida(otra, false);
    }
}
