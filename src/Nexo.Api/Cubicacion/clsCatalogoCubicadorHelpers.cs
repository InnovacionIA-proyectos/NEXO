using System.Text.Json.Nodes;
using Nexo.Application.Common;

namespace Nexo.Api.Cubicacion;

/// <summary>
/// S5-HU-18.6/#278: lo común a los seis passthrough de catálogo — estampar el
/// <c>usuarioId</c> del usuario AUTENTICADO (nunca el que mande el cliente) antes de
/// reenviar al Cubicador, que lo usa como <c>CreadoPor</c> sin comprobar identidad.
/// </summary>
internal static class clsCatalogoCubicadorHelpers
{
    public static JsonObject ConUsuario(this JsonObject cuerpo, ICurrentUserPort usuarioActual)
    {
        cuerpo["usuarioId"] = usuarioActual.Id.ToString();
        return cuerpo;
    }
}
