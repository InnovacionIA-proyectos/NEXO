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
/// El decorador que aplica el tope diario y registra cada llamada, con un adaptador interno y un repositorio en memoria: sin
/// red y sin base. Verifica qué se registra (y qué no), cuándo responde 429 y que un fallo al registrar no pierde la respuesta.
/// </summary>
public class clsAsistenteIaConRegistroTests
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
        public DateTimeOffset? DesdeConsultado { get; private set; }
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
            DesdeConsultado = desde;
            return Task.FromResult(Consumidos);
        }

        public Task<IReadOnlyList<clsGastoIaDto>> GastoPorFuncionYMesAsync(
            DateTimeOffset desde, DateTimeOffset hasta, string? funcion, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<clsGastoIaDto>>([]);
    }

    private sealed class clsInterno(bool habilitada, Func<clsPeticionIa, clsRespuestaIa> responder) : IAsistenteIaPort
    {
        public int Llamadas { get; private set; }
        public bool Habilitada(clsFuncionIa funcion) => habilitada;

        public Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
        {
            Llamadas++;
            return Task.FromResult(responder(peticion));
        }
    }

    private static clsRespuestaIa Respuesta(long entrada = 499, long salida = 174, long cacheLectura = 0, long cacheEscritura = 0) =>
        new("texto", null, "claude-sonnet-5-5", entrada, salida, TimeSpan.FromMilliseconds(5), cacheLectura, cacheEscritura);

    private static clsPeticionIa Peticion(Guid? usuario = null, string contenido = "Mapea esto: NIT 900123456") => new(
        clsFuncionIa.ImportacionExcel, "Instrucciones.", [new clsMensajeIa(clsMensajeIa.RolUsuario, contenido)], UsuarioId: usuario);

    private static clsAsistenteIaConRegistro Crear(
        IAsistenteIaPort interno, clsRepositorioEnMemoria repo, long limite = 1000, DateTimeOffset? ahora = null) =>
        new(interno, repo,
            Options.Create(new clsOpcionesAnthropic { LimiteTokensPorUsuarioDia = limite, Model = "claude-sonnet-5-5" }),
            new clsRelojFijo(ahora ?? new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.Zero)),
            NullLogger<clsAsistenteIaConRegistro>.Instance);

    // ------------------------------------------------------------ registro

    [Fact]
    public async Task Una_llamada_exitosa_queda_registrada_con_tokens_costo_usuario_y_resultado_ok()
    {
        var repo = new clsRepositorioEnMemoria();

        await Crear(new clsInterno(true, _ => Respuesta()), repo).CompletarAsync(Peticion(Usuario));

        var r = Assert.Single(repo.Registros);
        Assert.Equal("ImportacionExcel", r.Funcion);
        Assert.Equal(Usuario, r.UsuarioId);
        Assert.Equal("claude-sonnet-5-5", r.Modelo);
        Assert.Equal((499, 174), (r.TokensEntrada, r.TokensSalida));
        Assert.Equal(0.002738m, r.CostoEstimadoUsd);
        Assert.Equal("ok", r.Resultado);
        Assert.True(r.LatenciaMs >= 0);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 15, 0, 0, TimeSpan.Zero), r.Fecha);
    }

    [Fact]
    public async Task El_registro_no_contiene_el_contenido_enviado_ni_la_respuesta()
    {
        var repo = new clsRepositorioEnMemoria();

        await Crear(new clsInterno(true, _ => Respuesta()), repo).CompletarAsync(Peticion(Usuario, "Mapea esto: NIT 900123456"));

        var json = JsonSerializer.Serialize(Assert.Single(repo.Registros));
        Assert.DoesNotContain("900123456", json);
        Assert.DoesNotContain("Mapea esto", json);
        Assert.DoesNotContain("Instrucciones", json);
        Assert.DoesNotContain("texto", json.Replace("TokensEntrada", string.Empty));
    }

    [Fact]
    public async Task Los_tokens_de_cache_se_registran_y_entran_al_costo()
    {
        var repo = new clsRepositorioEnMemoria();

        await Crear(new clsInterno(true, _ => Respuesta(entrada: 100, salida: 0, cacheLectura: 1000, cacheEscritura: 200)), repo)
            .CompletarAsync(Peticion(Usuario));

        var r = Assert.Single(repo.Registros);
        Assert.Equal((1000, 200), (r.TokensCacheLectura, r.TokensCacheEscritura));
        // (100*2 + 1000*0.20 + 200*2*1.25) / 1e6
        Assert.Equal(0.000900m, r.CostoEstimadoUsd);
    }

    [Fact]
    public async Task Un_error_del_proveedor_queda_registrado_con_su_codigo_y_se_relanza()
    {
        var repo = new clsRepositorioEnMemoria();
        var interno = new clsInterno(true, _ => throw new clsServiceUnavailableException("IA_LIMITE_PROVEEDOR", "x"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Crear(interno, repo).CompletarAsync(Peticion(Usuario)));

        Assert.Equal("IA_LIMITE_PROVEEDOR", ex.Code);
        var r = Assert.Single(repo.Registros);
        Assert.Equal("IA_LIMITE_PROVEEDOR", r.Resultado);
        Assert.Equal((0, 0), (r.TokensEntrada, r.TokensSalida));
        Assert.Equal("claude-sonnet-5-5", r.Modelo);
    }

    [Fact]
    public async Task Una_respuesta_que_no_sirve_registra_igual_los_tokens_que_costo()
    {
        var repo = new clsRepositorioEnMemoria();
        var interno = new clsInterno(true, _ => throw new clsIaRespuestaNoUtilException(
            "IA_RESPUESTA_INVALIDA", "x", new clsUsoDeTokens("claude-sonnet-5-5", 300, 80, 0, 0)));

        await Assert.ThrowsAsync<clsIaRespuestaNoUtilException>(() => Crear(interno, repo).CompletarAsync(Peticion(Usuario)));

        var r = Assert.Single(repo.Registros);
        Assert.Equal("IA_RESPUESTA_INVALIDA", r.Resultado);
        Assert.Equal((300, 80), (r.TokensEntrada, r.TokensSalida));
        Assert.NotNull(r.CostoEstimadoUsd);
    }

    [Fact]
    public async Task Un_modelo_sin_precio_conocido_se_registra_sin_costo()
    {
        var repo = new clsRepositorioEnMemoria();
        var interno = new clsInterno(true, _ => Respuesta() with { Modelo = "un-modelo-nuevo" });

        await Crear(interno, repo).CompletarAsync(Peticion(Usuario));

        Assert.Null(Assert.Single(repo.Registros).CostoEstimadoUsd);
    }

    [Fact]
    public async Task Si_guardar_el_registro_falla_la_respuesta_no_se_pierde()
    {
        var repo = new clsRepositorioEnMemoria { FallaAlRegistrar = true };

        var r = await Crear(new clsInterno(true, _ => Respuesta()), repo).CompletarAsync(Peticion(Usuario));

        Assert.Equal("texto", r.Texto);
    }

    // ------------------------------------------------------------ lo que no se registra

    [Fact]
    public async Task Con_la_funcion_apagada_delega_sin_consultar_el_tope_ni_registrar()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 99_999 };
        var interno = new clsInterno(false, _ => throw new clsServiceUnavailableException("IA_FUNCION_DESHABILITADA", "x"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Crear(interno, repo).CompletarAsync(Peticion(Usuario)));

        Assert.Equal("IA_FUNCION_DESHABILITADA", ex.Code);
        Assert.Empty(repo.Registros);
        Assert.Equal(0, repo.ConsultasDeConsumo);
    }

    // ------------------------------------------------------------ tope diario

    [Fact]
    public async Task Al_superar_el_tope_responde_429_con_el_consumo_y_no_llama_al_proveedor_ni_registra()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 1000 };
        var interno = new clsInterno(true, _ => Respuesta());

        var ex = await Assert.ThrowsAsync<clsLimiteExcedidoException>(() => Crear(interno, repo, limite: 1000).CompletarAsync(Peticion(Usuario)));

        Assert.Equal("IA_TOPE_DIARIO", ex.Code);
        Assert.Contains("tope diario", ex.Message);
        Assert.Equal(0, interno.Llamadas);
        Assert.Empty(repo.Registros);
        var detalle = JsonSerializer.SerializeToElement(ex.Details);
        Assert.Equal(1000, detalle.GetProperty("limiteTokensPorDia").GetInt64());
        Assert.Equal(1000, detalle.GetProperty("tokensConsumidosHoy").GetInt64());
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.FromHours(-5)), detalle.GetProperty("reiniciaEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task Por_debajo_del_tope_la_llamada_pasa()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 999 };

        await Crear(new clsInterno(true, _ => Respuesta()), repo, limite: 1000).CompletarAsync(Peticion(Usuario));

        Assert.Single(repo.Registros);
    }

    [Fact]
    public async Task Sin_usuario_no_hay_tope_por_persona_y_ni_se_consulta_el_consumo()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 999_999 };

        await Crear(new clsInterno(true, _ => Respuesta()), repo, limite: 1000).CompletarAsync(Peticion(usuario: null));

        Assert.Equal(0, repo.ConsultasDeConsumo);
        Assert.Null(Assert.Single(repo.Registros).UsuarioId);
    }

    [Fact]
    public async Task Un_tope_de_cero_lo_desactiva()
    {
        var repo = new clsRepositorioEnMemoria { Consumidos = 999_999 };

        await Crear(new clsInterno(true, _ => Respuesta()), repo, limite: 0).CompletarAsync(Peticion(Usuario));

        Assert.Equal(0, repo.ConsultasDeConsumo);
        Assert.Single(repo.Registros);
    }

    [Theory]
    [InlineData("2026-10-27T15:00:00+00:00", "2026-10-27T00:00:00-05:00")] // 10 a. m. en Colombia: el mismo día
    [InlineData("2026-10-27T03:00:00+00:00", "2026-10-26T00:00:00-05:00")] // 10 p. m. del 26 en Colombia: el día anterior
    [InlineData("2026-10-27T05:00:00+00:00", "2026-10-27T00:00:00-05:00")] // medianoche en Colombia: ya es el 27
    public void El_dia_del_tope_empieza_a_la_medianoche_de_Colombia(string ahoraUtc, string inicioEsperado)
    {
        Assert.Equal(DateTimeOffset.Parse(inicioEsperado), clsAsistenteIaConRegistro.InicioDelDia(DateTimeOffset.Parse(ahoraUtc)));
    }

    [Fact]
    public async Task El_consumo_se_cuenta_desde_el_inicio_del_dia_de_Colombia()
    {
        var repo = new clsRepositorioEnMemoria();
        var ahora = DateTimeOffset.Parse("2026-10-27T03:00:00+00:00");

        await Crear(new clsInterno(true, _ => Respuesta()), repo, ahora: ahora).CompletarAsync(Peticion(Usuario));

        Assert.Equal(DateTimeOffset.Parse("2026-10-26T00:00:00-05:00"), repo.DesdeConsultado);
    }

    // ------------------------------------------------------------ precios

    [Theory]
    [InlineData("claude-sonnet-5-5", 499, 174, 0.002738)]
    [InlineData("claude-sonnet-4-6", 1_000_000, 1_000_000, 18.0)]
    [InlineData("CLAUDE-SONNET-5", 1_000_000, 0, 2.0)]
    public void El_costo_estimado_sale_de_los_precios_de_lista(string modelo, long entrada, long salida, double esperado)
    {
        Assert.Equal((decimal)esperado, clsPreciosIa.Estimar(modelo, entrada, salida, 0, 0));
    }

    [Fact]
    public void Un_modelo_desconocido_no_tiene_costo_estimado()
    {
        Assert.Null(clsPreciosIa.Estimar("modelo-que-no-existe", 1000, 1000, 0, 0));
    }
}
