namespace Nexo.Api.Tests.Infra;

/// <summary>
/// Marca una prueba que habla con el Cubicador REAL (docker compose, cubicador/docker-compose.yml,
/// puerto 8081): confirma que el contrato que NEXO habla hoy es el que el Cubicador entiende.
///
/// SE SALTA SI EL ENTORNO NO TRAE EL CUBICADOR — <c>Cubicador3D__OutboundBaseUrl</c> y la llave que
/// esa prueba usa (<paramref name="variableLlave"/>). Sin ellas la aplicación responde 503 por
/// diseño ("no configurado") y la prueba fallaría por algo que no es un defecto del código. Es el
/// caso del runner de GitHub Actions, que no tiene el stack del Cubicador levantado: S5-HU-18 las
/// metió en la suite y el CI quedó en rojo con 6 × <c>ServiceUnavailable</c> (corrida del 2026-10-14).
///
/// Mismo criterio que <c>clsHechoConCorreoRealAttribute</c>: el salto es explícito y dice cómo
/// correrla, no un fallo mudo. Con las variables definidas (como en <c>.env</c> local, o con los
/// secretos <c>CUBICADOR3D_*</c> en el workflow) corre normal, y si el Cubicador no responde FALLA:
/// ahí sí es una señal real.
///
/// S5-HU-17.7 — VENTANA DE DESPLIEGUE: el orden acordado es DDL → NEXO → Cubicador, pero el deploy de
/// NEXO depende de que esta suite pase. Mientras el Cubicador desplegado siga con el contrato viejo
/// responde 422 al <c>codigoMercancia</c> (exige <c>idTipoMercancia</c>) y las pruebas que piden una
/// recomendación fallarían por el orden de despliegue, no por un defecto: NEXO jamás podría salir primero.
/// Esas pruebas (las de <c>RecomendacionesApiKey</c>) sondean el Cubicador y se saltan, diciéndolo, solo en
/// ese caso exacto. Si el Cubicador no responde, o ya tiene el contrato nuevo, corren y fallan normal.
/// </summary>
public sealed class clsHechoConCubicadorRealAttribute : FactAttribute
{
    public const string VarUrl = "Cubicador3D__OutboundBaseUrl";
    private const string VarLlaveRecomendaciones = "Cubicador3D__RecomendacionesApiKey";

    // Una sola sonda por corrida: todas las pruebas de recomendaciones comparten el resultado.
    private static readonly Lazy<string?> ContratoViejo = new(SondearContratoViejo);

    public clsHechoConCubicadorRealAttribute(string variableLlave)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VarUrl)) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableLlave)))
        {
            Skip =
                $"Habla con el Cubicador REAL. Para correrla: {VarUrl}=<url del Cubicador> y " +
                $"{variableLlave}=<llave> (ver .env.example) y el stack de cubicador/docker-compose.yml arriba.";
            return;
        }

        if (variableLlave == VarLlaveRecomendaciones)
            Skip = ContratoViejo.Value;
    }

    /// <summary>Devuelve el motivo del salto si el Cubicador aún exige <c>idTipoMercancia</c>; null en cualquier
    /// otro caso (contrato nuevo, Cubicador caído, error de red): ahí la prueba debe correr y dar la señal real.
    /// La sonda manda una mercancía inexistente: un Cubicador nuevo la rechaza con 400 sin guardar nada.</summary>
    private static string? SondearContratoViejo()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.BaseAddress = new Uri(Environment.GetEnvironmentVariable(VarUrl)!.TrimEnd('/') + "/");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", Environment.GetEnvironmentVariable(VarLlaveRecomendaciones));
            var cuerpo = new StringContent(
                """{"idLineaSolicitud":"SONDA-CONTRATO","mercancias":[{"codigoMercancia":"SONDA_NO_EXISTE","cantidad":1,"largoCm":1,"anchoCm":1,"altoCm":1,"pesoKg":1}]}""",
                System.Text.Encoding.UTF8, "application/json");

            var respuesta = http.PostAsync("recomendaciones", cuerpo).GetAwaiter().GetResult();
            if ((int)respuesta.StatusCode != 422) return null;

            var detalle = respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return detalle.Contains("idTipoMercancia", StringComparison.Ordinal)
                ? "El Cubicador desplegado todavía tiene el contrato anterior a S5-HU-17.7 (exige idTipoMercancia y responde 422 " +
                  "al codigoMercancia). Orden de despliegue: DDL → NEXO → Cubicador. Se corre sola en cuanto el Cubicador se despliegue."
                : null;
        }
        catch
        {
            return null;
        }
    }
}
