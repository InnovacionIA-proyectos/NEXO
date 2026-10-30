using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nexo.Api.Common.Middleware;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Api.Tests.IA;

/// <summary>Un tope de uso superado sale como 429 con su código y su detalle, no como 500 ni como 503.</summary>
public class clsLimiteExcedidoMiddlewareTests
{
    private sealed class clsAmbiente : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public async Task Un_limite_excedido_responde_429_con_codigo_mensaje_y_detalle()
    {
        var middleware = new clsErrorHandlingMiddleware(
            _ => throw new clsLimiteExcedidoException("IA_TOPE_DIARIO", "Alcanzaste el tope.", new { limiteTokensPorDia = 500 }),
            NullLogger<clsErrorHandlingMiddleware>.Instance, new clsAmbiente());
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(contexto);

        Assert.Equal(StatusCodes.Status429TooManyRequests, contexto.Response.StatusCode);
        contexto.Response.Body.Position = 0;
        using var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.Equal("IA_TOPE_DIARIO", cuerpo.RootElement.GetProperty("code").GetString());
        Assert.Equal("Alcanzaste el tope.", cuerpo.RootElement.GetProperty("message").GetString());
        Assert.Equal(500, cuerpo.RootElement.GetProperty("details").GetProperty("limiteTokensPorDia").GetInt32());
    }
}
