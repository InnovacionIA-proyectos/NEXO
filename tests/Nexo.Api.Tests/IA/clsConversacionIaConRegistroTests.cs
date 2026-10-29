using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.IA;

namespace Nexo.Api.Tests.IA;

/// <summary>
/// El decorador de la conversación: aplica el tope diario antes de empezar y deja UNA fila de uso por intercambio con la suma de lo que
/// gastó, sea cual sea el final (normal, error del proveedor, rechazo o conexión cerrada). Sin red y sin base.
/// </summary>
public class clsConversacionIaConRegistroTests
{
    private static readonly Guid Usuario = Guid.NewGuid();

    private sealed class clsRelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class clsRepositorioEnMemoria : IUsoIaRepository
    {
        public List<clsRegistroUsoIa> Registros { get; } = [];
        public long Consumidos { get; set; }
        public int ConsultasDeConsumo { get; private set; }
        public bool FallaAlRegistrar { get; set; }

        public Task RegistrarAsync(clsRegistroUsoIa registro, CancellationToken ct = default)
        {
            if (FallaAlRegistrar) throw new InvalidOperationException("la base no responde");
            Registros.Add(registro);
            return Task.CompletedTask;
        }

        public Task<long> TokensConsumidosDesdeAsync(Guid usuarioId, DateTimeOffset desde, CancellationToken ct = default)
        {
            ConsultasDeConsumo++;
            return Task.FromResult(Consumidos);
        }

        public Task<IReadOnlyList<clsGastoIaDto>> GastoPorFuncionYMesAsync(
            DateTimeOffset desde, DateTimeOffset hasta, string? funcion, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<clsGastoIaDto>>([]);
    }

    private sealed class clsInterno(Func<IAsyncEnumerable<clsEventoConversacionIa>> eventos) : IConversacionIaPort
    {
        public int Verificaciones { get; private set; }

        public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default)
        {
            Verificaciones++;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
            clsPeticionConversacionIa peticion, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
            CancellationToken ct = default) => eventos();
    }

    private static clsUsoDeTokens Tokens(long entrada, long salida, long cacheLectura = 0) =>
        new("claude-sonnet-5-5", entrada, salida, cacheLectura, 0);

    private static async IAsyncEnumerable<clsEventoConversacionIa> Dos()
    {
        yield return new clsEventoConversacionIa.Texto("Consulto. ");
        yield return new clsEventoConversacionIa.Uso(Tokens(300, 40));
        yield return new clsEventoConversacionIa.Texto("El piso es alto.");
        yield return new clsEventoConversacionIa.Uso(Tokens(450, 25, cacheLectura: 100));
        yield return new clsEventoConversacionIa.Fin("end_turn");
        await Task.CompletedTask;
    }

    private static clsPeticionConversacionIa Peticion(Guid? usuario = null) => new(
        clsFuncionIa.AsistenteCotizacion, "Instrucciones del asistente.", [new clsMensajeIa(clsMensajeIa.RolUsuario, "NIT 900123456, ¿piso?")],
        [], 4, null, usuario ?? Usuario);

    private static clsConversacionIaConRegistro Crear(IConversacionIaPort interno, clsRepositorioEnMemoria repo, long limite = 1000) =>
        new(interno, repo,
            Options.Create(new clsOpcionesAnthropic { LimiteTokensPorUsuarioDia = limite, Model = "claude-sonnet-5-5" }),
            new clsRelojFijo(new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.Zero)),
            NullLogger<clsConversacionIaConRegistro>.Instance);

    private static Task<clsResultadoHerramientaIa> SinHerramienta(clsLlamadaHerramientaIa _, CancellationToken __) =>
        Task.FromResult(new clsResultadoHerramientaIa("{}", false));

    private static async Task<List<clsEventoConversacionIa>> Correr(clsConversacionIaConRegistro d)
    {
        List<clsEventoConversacionIa> lista = [];
        await foreach (var e in d.ConversarAsync(Peticion(), SinHerramienta)) lista.Add(e);
        return lista;
    }

    // ------------------------------------------------------------ tope diario

    [Fact]
    public async Task Verificar_pasa_primero_por_el_interno_y_corta_con_429_si_la_persona_ya_llego_al_tope()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 1000 };
        var interno = new clsInterno(Dos);

        var ex = await Assert.ThrowsAsync<clsLimiteExcedidoException>(() =>
            Crear(interno, repo).VerificarAsync(clsFuncionIa.AsistenteCotizacion, Usuario));

        Assert.Equal("IA_TOPE_DIARIO", ex.Code);
        Assert.Equal(1, interno.Verificaciones);
        Assert.Empty(repo.Registros);
    }

    [Fact]
    public async Task Verificar_deja_pasar_a_quien_esta_por_debajo_del_tope_y_no_consulta_si_no_hay_tope_o_usuario()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 999 };

        await Crear(new clsInterno(Dos), repo).VerificarAsync(clsFuncionIa.AsistenteCotizacion, Usuario);
        Assert.Equal(1, repo.ConsultasDeConsumo);

        await Crear(new clsInterno(Dos), repo, limite: 0).VerificarAsync(clsFuncionIa.AsistenteCotizacion, Usuario);
        await Crear(new clsInterno(Dos), repo).VerificarAsync(clsFuncionIa.AsistenteCotizacion, null);
        Assert.Equal(1, repo.ConsultasDeConsumo);
    }

    // ------------------------------------------------------------ registro

    [Fact]
    public async Task Un_intercambio_con_varias_peticiones_deja_una_fila_con_la_suma_de_tokens_y_los_eventos_pasan_intactos()
    {
        var repo = new clsRepositorioEnMemoria();

        var eventos = await Correr(Crear(new clsInterno(Dos), repo));

        Assert.Equal(5, eventos.Count);
        var r = Assert.Single(repo.Registros);
        Assert.Equal("AsistenteCotizacion", r.Funcion);
        Assert.Equal(Usuario, r.UsuarioId);
        Assert.Equal("claude-sonnet-5-5", r.Modelo);
        Assert.Equal((750, 65, 100), (r.TokensEntrada, r.TokensSalida, r.TokensCacheLectura));
        Assert.NotNull(r.CostoEstimadoUsd);
        Assert.Equal("ok", r.Resultado);
    }

    [Fact]
    public async Task El_registro_no_guarda_lo_que_se_le_pregunto_ni_lo_que_respondio()
    {
        var repo = new clsRepositorioEnMemoria();

        await Correr(Crear(new clsInterno(Dos), repo));

        var json = JsonSerializer.Serialize(Assert.Single(repo.Registros));
        Assert.DoesNotContain("900123456", json);
        Assert.DoesNotContain("piso", json);
        Assert.DoesNotContain("Instrucciones", json);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> FallaDespuesDeUnaPeticion()
    {
        yield return new clsEventoConversacionIa.Texto("Consulto. ");
        yield return new clsEventoConversacionIa.Uso(Tokens(300, 40));
        await Task.Yield();
        throw new clsServiceUnavailableException("IA_NO_DISPONIBLE", "El proveedor de IA no está disponible en este momento.");
    }

    [Fact]
    public async Task Si_el_proveedor_falla_a_mitad_queda_anotado_con_lo_gastado_hasta_ahi_y_el_codigo_y_el_error_se_propaga()
    {
        var repo = new clsRepositorioEnMemoria();

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Correr(Crear(new clsInterno(FallaDespuesDeUnaPeticion), repo)));

        Assert.Equal("IA_NO_DISPONIBLE", ex.Code);
        var r = Assert.Single(repo.Registros);
        Assert.Equal("IA_NO_DISPONIBLE", r.Resultado);
        Assert.Equal((300, 40), (r.TokensEntrada, r.TokensSalida));
    }

    [Theory]
    [InlineData("refusal", "IA_RECHAZADA")]
    [InlineData("max_tokens", "IA_RESPUESTA_TRUNCADA")]
    [InlineData("max_iteraciones", "max_iteraciones")]
    public async Task Un_final_que_no_es_normal_queda_anotado_como_resultado(string motivo, string esperado)
    {
        var repo = new clsRepositorioEnMemoria();
        IAsyncEnumerable<clsEventoConversacionIa> Termina() => Eventos(new clsEventoConversacionIa.Uso(Tokens(10, 5)), new clsEventoConversacionIa.Fin(motivo));

        await Correr(Crear(new clsInterno(Termina), repo));

        Assert.Equal(esperado, Assert.Single(repo.Registros).Resultado);
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> Eventos(params clsEventoConversacionIa[] eventos)
    {
        foreach (var e in eventos) yield return e;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Si_el_cliente_cierra_la_conexion_a_mitad_se_anota_como_cancelada_con_lo_gastado()
    {
        var repo = new clsRepositorioEnMemoria();
        var d = Crear(new clsInterno(Dos), repo);

        await using (var e = d.ConversarAsync(Peticion(), SinHerramienta).GetAsyncEnumerator())
        {
            await e.MoveNextAsync(); // texto
            await e.MoveNextAsync(); // tokens de la primera petición
        }

        var r = Assert.Single(repo.Registros);
        Assert.Equal("cancelada", r.Resultado);
        Assert.Equal((300, 40), (r.TokensEntrada, r.TokensSalida));
    }

    [Fact]
    public async Task Si_guardar_el_uso_falla_la_respuesta_no_se_pierde()
    {
        var repo = new clsRepositorioEnMemoria { FallaAlRegistrar = true };

        var eventos = await Correr(Crear(new clsInterno(Dos), repo));

        Assert.Equal(5, eventos.Count);
    }
}
