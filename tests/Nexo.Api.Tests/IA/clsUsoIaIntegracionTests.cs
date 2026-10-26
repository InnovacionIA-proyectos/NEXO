using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.IA.Entidades;
using Nexo.Infrastructure.IA;

namespace Nexo.Api.Tests.IA;

/// <summary>
/// El registro y el tope de uso de la IA contra la base REAL (tabla <c>tbl_ia_uso</c>) y la API real, con un proveedor simulado:
/// ninguna prueba llama a Anthropic. Lo que escriben se identifica por un modelo o una función con marca única y se borra al
/// terminar, así que no queda nada ni se toca el uso de otras personas.
/// </summary>
[Collection("api")]
public class clsUsoIaIntegracionTests(clsApiFixture fx)
{
    private sealed class clsProveedorSimulado(string modelo) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            var json = JsonSerializer.Serialize(new
            {
                id = "msg_prueba", type = "message", role = "assistant", model = modelo,
                content = new[] { new { type = "text", text = "listo" } },
                stop_reason = "end_turn", stop_sequence = (string?)null,
                usage = new { input_tokens = 499, output_tokens = 174 },
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private Guid UsuarioExistente() => fx.WithDb(db => db.Usuarios.First().Id);

    private void BorrarPor(string modelo) =>
        fx.WithDb(db => db.UsosIa.Where(u => u.Modelo == modelo).ExecuteDelete());

    private void BorrarPorFuncion(string funcion) =>
        fx.WithDb(db => db.UsosIa.Where(u => u.Funcion == funcion).ExecuteDelete());

    private static clsRegistroUsoIa Registro(string funcion, Guid? usuario, string modelo, DateTimeOffset fecha,
        long entrada = 100, long salida = 50, decimal? costo = 0.001m, string resultado = "ok", int latencia = 1000) =>
        new(funcion, usuario, modelo, entrada, salida, 0, 0, costo, latencia, resultado, fecha);

    // ------------------------------------------------------------ repositorio

    [Fact]
    public async Task Un_registro_se_guarda_sin_contenido_y_suma_al_consumo_del_usuario_desde_la_fecha()
    {
        var modelo = $"prueba-uso-{Guid.NewGuid():N}";
        var usuario = UsuarioExistente();
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        try
        {
            using var scope = fx.Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUsoIaRepository>();
            var antes = await repo.TokensConsumidosDesdeAsync(usuario, inicio);

            await repo.RegistrarAsync(Registro("ImportacionExcel", usuario, modelo, DateTimeOffset.UtcNow, entrada: 400, salida: 100));

            Assert.Equal(antes + 500, await repo.TokensConsumidosDesdeAsync(usuario, inicio));
            // Una fecha posterior al registro no lo cuenta.
            Assert.Equal(0, await repo.TokensConsumidosDesdeAsync(usuario, DateTimeOffset.UtcNow.AddMinutes(5)));
            var fila = fx.WithDb(db => db.UsosIa.Single(u => u.Modelo == modelo));
            Assert.Equal((usuario, "ok", 400, 100), (fila.UsuarioId, fila.Resultado, fila.TokensEntrada, fila.TokensSalida));
        }
        finally { BorrarPor(modelo); }
    }

    [Fact]
    public async Task El_gasto_se_agrupa_por_mes_de_Colombia_y_por_funcion()
    {
        var funcion = $"PRUEBA_{Guid.NewGuid():N}"[..30];
        var modelo = $"prueba-uso-{Guid.NewGuid():N}";
        try
        {
            using var scope = fx.Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUsoIaRepository>();
            // 22:00 del 31 de octubre en Colombia ya es 1 de noviembre en UTC: pertenece a octubre.
            await repo.RegistrarAsync(Registro(funcion, null, modelo, DateTimeOffset.Parse("2026-11-01T03:00:00+00:00"), costo: 0.010m, latencia: 2000));
            await repo.RegistrarAsync(Registro(funcion, null, modelo, DateTimeOffset.Parse("2026-10-15T12:00:00+00:00"), costo: 0.020m, latencia: 1000));
            await repo.RegistrarAsync(Registro(funcion, null, modelo, DateTimeOffset.Parse("2026-10-16T12:00:00+00:00"), costo: null, resultado: "IA_RECHAZADA", latencia: 3000));
            // Medianoche del 1 de noviembre en Colombia (05:00 UTC): ya es noviembre.
            await repo.RegistrarAsync(Registro(funcion, null, modelo, DateTimeOffset.Parse("2026-11-01T05:00:00+00:00"), costo: 0.040m));

            var gasto = await repo.GastoPorFuncionYMesAsync(
                DateTimeOffset.Parse("2026-10-01T00:00:00+00:00"), DateTimeOffset.Parse("2026-12-01T00:00:00+00:00"), funcion);

            Assert.Equal(["2026-11", "2026-10"], gasto.Select(g => g.Mes));
            var oct = gasto.Single(g => g.Mes == "2026-10");
            Assert.Equal((3, 1, 300, 150), (oct.Llamadas, oct.Errores, oct.TokensEntrada, oct.TokensSalida));
            Assert.Equal(0.030m, oct.CostoEstimadoUsd);
            Assert.Equal(1, oct.LlamadasSinCosto);
            Assert.Equal(2000, oct.LatenciaPromedioMs);
            var nov = gasto.Single(g => g.Mes == "2026-11");
            Assert.Equal((1, 0, 0.040m), (nov.Llamadas, nov.Errores, nov.CostoEstimadoUsd));
        }
        finally { BorrarPorFuncion(funcion); }
    }

    // ------------------------------------------------------------ de punta a punta: decorador + adaptador + base

    [Fact]
    public async Task Cada_llamada_se_registra_en_la_base_y_al_pasar_el_tope_responde_429_sin_llamar_al_proveedor()
    {
        var modelo = $"prueba-uso-{Guid.NewGuid():N}";
        var usuario = UsuarioExistente();
        var proveedor = new clsProveedorSimulado(modelo);
        long consumidoHoy;
        using (var scope = fx.Factory.Services.CreateScope())
            consumidoHoy = await scope.ServiceProvider.GetRequiredService<IUsoIaRepository>()
                .TokensConsumidosDesdeAsync(usuario, clsAsistenteIaConRegistro.InicioDelDia(DateTimeOffset.UtcNow));

        // El tope deja pasar la primera y la segunda llamada (673 tokens cada una) y corta la tercera.
        var limite = consumidoHoy + 1000;
        using var fabrica = fx.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "sk-ant-llave-falsa-de-prueba",
                ["Anthropic:Model"] = modelo,
                ["Anthropic:Funciones:ImportacionExcel"] = "true",
                ["Anthropic:LimiteTokensPorUsuarioDia"] = limite.ToString(),
            }));
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<clsAnthropicAdapter>();
                s.AddSingleton(sp => new clsAnthropicAdapter(
                    sp.GetRequiredService<IOptions<clsOpcionesAnthropic>>(), NullLogger<clsAnthropicAdapter>.Instance,
                    new HttpClient(proveedor), "http://proveedor.prueba"));
            });
        });
        try
        {
            var peticion = new clsPeticionIa(
                clsFuncionIa.ImportacionExcel, "Instrucciones.", [new clsMensajeIa(clsMensajeIa.RolUsuario, "Hola")], UsuarioId: usuario);

            for (var i = 0; i < 2; i++)
            {
                using var scope = fabrica.Services.CreateScope();
                var r = await scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>().CompletarAsync(peticion);
                Assert.Equal(modelo, r.Modelo);
            }
            Assert.Equal(2, proveedor.Llamadas);

            using (var scope = fabrica.Services.CreateScope())
            {
                var ex = await Assert.ThrowsAsync<clsLimiteExcedidoException>(
                    () => scope.ServiceProvider.GetRequiredService<IAsistenteIaPort>().CompletarAsync(peticion));
                Assert.Equal("IA_TOPE_DIARIO", ex.Code);
            }
            Assert.Equal(2, proveedor.Llamadas);

            var filas = fx.WithDb(db => db.UsosIa.Where(u => u.Modelo == modelo).ToList());
            Assert.Equal(2, filas.Count);
            Assert.All(filas, f => Assert.Equal(
                ("ImportacionExcel", usuario, "ok", 499, 174), (f.Funcion, f.UsuarioId, f.Resultado, f.TokensEntrada, f.TokensSalida)));
        }
        finally { BorrarPor(modelo); }
    }

    // ------------------------------------------------------------ endpoint de gasto

    [Fact]
    public async Task El_gasto_solo_lo_consulta_el_Admin()
    {
        using var admin = fx.Client("Admin");
        var resp = await admin.GetAsync("/api/ia/uso/gasto");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await resp.Json()).ValueKind);

        foreach (var rol in new[] { "Comercial", "AdminTarifas", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            using var otro = fx.Client(rol);
            Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync("/api/ia/uso/gasto")).StatusCode);
        }
    }

    [Fact]
    public async Task El_gasto_rechaza_una_funcion_inexistente_y_un_rango_invertido()
    {
        using var admin = fx.Client("Admin");

        var funcion = await admin.GetAsync("/api/ia/uso/gasto?funcion=Inventada");
        Assert.Equal(HttpStatusCode.BadRequest, funcion.StatusCode);
        Assert.Equal("FUNCION_IA_INVALIDA", await funcion.ErrorCode());

        var rango = await admin.GetAsync("/api/ia/uso/gasto?desde=2026-10-02T00:00:00Z&hasta=2026-10-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, rango.StatusCode);
        Assert.Equal("RANGO_INVALIDO", await rango.ErrorCode());
    }
}
