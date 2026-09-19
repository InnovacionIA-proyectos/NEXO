using System.Net.Http.Json;
using System.Text.Json;

namespace CabinaFletes.Api.Tests.Infra;

public static class Http
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static Task<HttpResponseMessage> PostJson(this HttpClient c, string url, object body)
        => c.PostAsJsonAsync(url, body, JsonOpts);

    public static Task<HttpResponseMessage> PatchJson(this HttpClient c, string url, object body)
        => c.PatchAsJsonAsync(url, body, JsonOpts);

    /// <summary>Lee el campo "code" de una respuesta de error estándar del API.</summary>
    public static async Task<string?> ErrorCode(this HttpResponseMessage r)
    {
        var el = await r.Content.ReadFromJsonAsync<JsonElement>();
        return el.TryGetProperty("code", out var c) ? c.GetString() : null;
    }

    public static async Task<JsonElement> Json(this HttpResponseMessage r)
        => await r.Content.ReadFromJsonAsync<JsonElement>();
}
