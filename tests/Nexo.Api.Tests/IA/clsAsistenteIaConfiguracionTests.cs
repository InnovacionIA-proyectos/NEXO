using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.IA;

namespace Nexo.Api.Tests.IA;

/// <summary>
/// El puerto de IA dentro de la aplicación real: arranca sin llave, las funciones nacen apagadas y el puerto se puede reemplazar
/// en pruebas. La configuración de cada prueba se pasa en memoria y pisa lo que haya en el entorno, así que el resultado no
/// depende de que la máquina tenga (o no) una llave.
/// </summary>
[Collection("api")]
public class clsAsistenteIaConfiguracionTests(clsApiFixture fx)
{
    private static readonly clsPeticionIa Peticion = new(
        clsFuncionIa.ImportacionExcel, "Instrucciones.", [new clsMensajeIa(clsMensajeIa.RolUsuario, "Hola")]);

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<clsProgram> Fabrica(
        Dictionary<string, string?> configuracion, Action<IServiceCollection>? servicios = null) =>
        fx.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(configuracion));
            if (servicios is not null) b.ConfigureTestServices(servicios);
        });

    [Fact]
    public async Task Sin_llave_la_aplicacion_arranca_y_las_funciones_de_IA_responden_IA_NO_CONFIGURADA()
    {
        using var fabrica = Fabrica(new() { ["Anthropic:ApiKey"] = "" });

        // La aplicación arranca (el bus tarda unos instantes en estar listo) y responde.
        HttpResponseMessage? salud = null;
        using var cliente = fabrica.CreateClient();
        for (var intento = 0; intento < 40; intento++)
        {
            salud = await cliente.GetAsync("/health");
            if (salud.StatusCode == HttpStatusCode.OK) break;
            await Task.Delay(100);
        }
        Assert.Equal(HttpStatusCode.OK, salud!.StatusCode);

        using var scope = fabrica.Services.CreateScope();
        var ia = scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>();
        Assert.All(Enum.GetValues<clsFuncionIa>(), f => Assert.False(ia.Habilitada(f)));
        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => ia.CompletarAsync(Peticion));
        Assert.Equal("IA_NO_CONFIGURADA", ex.Code);
    }

    [Fact]
    public async Task Con_llave_las_funciones_nacen_apagadas_y_responden_IA_FUNCION_DESHABILITADA()
    {
        using var fabrica = Fabrica(new() { ["Anthropic:ApiKey"] = "sk-ant-llave-falsa-de-prueba" });

        using var scope = fabrica.Services.CreateScope();
        var ia = scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>();

        Assert.All(Enum.GetValues<clsFuncionIa>(), f => Assert.False(ia.Habilitada(f)));
        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => ia.CompletarAsync(Peticion));
        Assert.Equal("IA_FUNCION_DESHABILITADA", ex.Code);
    }

    [Fact]
    public void Cada_funcion_se_enciende_por_separado_sin_distinguir_mayusculas()
    {
        using var fabrica = Fabrica(new()
        {
            ["Anthropic:ApiKey"] = "sk-ant-llave-falsa-de-prueba",
            ["Anthropic:Funciones:importacionexcel"] = "true",
            ["Anthropic:Funciones:AsistenteCotizacion"] = "false",
        });

        using var scope = fabrica.Services.CreateScope();
        var ia = scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>();

        Assert.True(ia.Habilitada(clsFuncionIa.ImportacionExcel));
        Assert.False(ia.Habilitada(clsFuncionIa.AsistenteCotizacion));
        Assert.False(ia.Habilitada(clsFuncionIa.ApoyoCubicador));
    }

    [Fact]
    public async Task El_puerto_se_reemplaza_por_el_falso_y_las_pruebas_no_llaman_a_ningun_proveedor()
    {
        var falso = clsAsistenteIaFalso.Responde("respuesta programada");
        using var fabrica = Fabrica(new() { ["Anthropic:ApiKey"] = "" }, s =>
        {
            s.RemoveAll<IAsistenteIaPort>();
            s.AddSingleton<IAsistenteIaPort>(falso);
        });

        using var scope = fabrica.Services.CreateScope();
        var ia = scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>();
        var r = await ia.CompletarAsync(Peticion);

        Assert.Same(falso, ia);
        Assert.Equal("respuesta programada", r.Texto);
        Assert.Single(falso.Peticiones);
    }

    [Fact]
    public void Las_opciones_por_defecto_son_las_decididas_y_todo_nace_apagado()
    {
        var o = new clsOpcionesAnthropic();

        Assert.Equal("claude-sonnet-4-6", o.ModeloEfectivo);
        Assert.Equal("medium", o.Effort);
        Assert.False(o.Configurada);
        Assert.All(Enum.GetValues<clsFuncionIa>(), f => Assert.False(o.FuncionEncendida(f)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(60, 60)]
    [InlineData(10_000, 300)]
    public void El_tiempo_de_espera_queda_dentro_de_limites_razonables(int configurado, int esperado)
    {
        Assert.Equal(TimeSpan.FromSeconds(esperado), new clsOpcionesAnthropic { TimeoutSegundos = configurado }.TimeoutEfectivo);
    }

    [Fact]
    public void Los_reintentos_y_el_tope_de_tokens_quedan_dentro_de_limites()
    {
        var o = new clsOpcionesAnthropic { MaxReintentos = 99, MaxTokensSalida = 10 };

        Assert.Equal(5, o.ReintentosEfectivos);
        Assert.Equal(256, o.MaxTokensEfectivo);
        Assert.Equal("claude-sonnet-4-6", new clsOpcionesAnthropic { Model = "  " }.ModeloEfectivo);
    }
}
