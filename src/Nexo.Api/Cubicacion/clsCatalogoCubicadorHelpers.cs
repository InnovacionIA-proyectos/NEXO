using System.Text.Json.Nodes;
using Nexo.Application.Common;

namespace Nexo.Api.Cubicacion;

/// <summary>Lo común a los passthrough de catálogo: estampa el <c>usuarioId</c> del usuario autenticado (nunca el que mande el cliente) antes de reenviar al Cubicador, que lo usa como <c>CreadoPor</c>.</summary>
internal static class clsCatalogoCubicadorHelpers
{
    public static JsonObject ConUsuario(this JsonObject cuerpo, ICurrentUserPort usuarioActual)
    {
        cuerpo["usuarioId"] = usuarioActual.Id.ToString();
        return cuerpo;
    }
}
