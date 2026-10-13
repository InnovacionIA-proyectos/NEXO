using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// S5-HU-23.1/#289: la vigencia de la Bandeja es la de la FECHA LÍMITE (no la antigüedad de la
/// captura; sin Deadline = vigente), cada fila trae el Resultado de su cotización, y el filtro
/// <c>resultado</c> cambia la atención por defecto. El SQL de "pendientes" y del filtro
/// resultado se prueba contra la base real en clsBandejaPendientesYResultadoTests.
/// </summary>
public class clsBandejaVigenciaYResultadoTests
{
    private readonly Mock<ISolicitudesRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    private clsObtenerBandejaQueryHandler Handler() => new(_repo.Object, TimeProvider.System, _usuario.Object);

    private static clsBandejaFilaRaw Fila(DateTimeOffset? deadline, DateTimeOffset creada, ResultadoCotizacion? resultado = null) => new(
        Guid.NewGuid(), "Cliente", TipoSolicitud.Spot, EstadoSolicitud.Pendiente, deadline, 1_000_000m, 0.5m,
        creada, 1, null, 1, 0, 0, resultado);

    private void ConFilas(params clsBandejaFilaRaw[] filas)
    {
        _repo.Setup(r => r.ParametrosScoreAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new clsParametrosScore());
        _repo.Setup(r => r.BandejaFilasAsync(
                It.IsAny<string?>(), It.IsAny<EstadoSolicitud?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(filas);
    }

    private static clsObtenerBandejaQuery Consulta(string? vigencia = null, string? atencion = "todas", string? resultado = null) =>
        new(new clsBandejaFiltro(null, atencion, null, null, vigencia, resultado));

    // ------------------------------------------------------------------ vigencia por Fecha Límite

    [Fact]
    public async Task Vigente_es_Deadline_futuro_o_sin_Deadline_aunque_la_captura_sea_vieja()
    {
        var ahora = DateTimeOffset.UtcNow;
        var futuraVieja = Fila(ahora.AddDays(5), creada: ahora.AddDays(-30));   // antes: "vencida" por antigüedad
        var sinDeadlineVieja = Fila(null, creada: ahora.AddDays(-30));          // sin Deadline = vigente
        ConFilas(futuraVieja, sinDeadlineVieja);

        var pagina = await Handler().Handle(Consulta());

        Assert.All(pagina.Items, i => Assert.Equal("vigente", i.Vigencia));
    }

    [Fact]
    public async Task Vencida_es_Deadline_pasado_aunque_la_captura_sea_de_hoy()
    {
        var ahora = DateTimeOffset.UtcNow;
        ConFilas(Fila(ahora.AddDays(-1), creada: ahora));                       // antes: "vigente" por recién capturada

        var pagina = await Handler().Handle(Consulta());

        Assert.Equal("vencida", Assert.Single(pagina.Items).Vigencia);
    }

    [Fact]
    public async Task El_filtro_vigente_solo_deja_Deadline_futuro_y_sin_Deadline()
    {
        var ahora = DateTimeOffset.UtcNow;
        var futura = Fila(ahora.AddDays(3), ahora.AddDays(-20));
        var sinDeadline = Fila(null, ahora.AddDays(-20));
        var pasada = Fila(ahora.AddDays(-3), ahora);
        ConFilas(futura, sinDeadline, pasada);

        var pagina = await Handler().Handle(Consulta(vigencia: "vigente"));

        Assert.Equal(new[] { futura.Id, sinDeadline.Id }.Order(), pagina.Items.Select(i => i.Id).Order());
        Assert.Equal(2, pagina.Total);
    }

    [Fact]
    public async Task El_filtro_vencida_solo_deja_los_Deadline_pasados_y_nunca_los_sin_Deadline()
    {
        var ahora = DateTimeOffset.UtcNow;
        var futura = Fila(ahora.AddDays(3), ahora.AddDays(-20));
        var sinDeadline = Fila(null, ahora.AddDays(-20));
        var pasada = Fila(ahora.AddDays(-3), ahora);
        ConFilas(futura, sinDeadline, pasada);

        var pagina = await Handler().Handle(Consulta(vigencia: "vencida"));

        Assert.Equal(pasada.Id, Assert.Single(pagina.Items).Id);
    }

    [Fact]
    public async Task Un_Deadline_a_segundos_de_vencer_todavia_es_vigente()
    {
        var ahora = DateTimeOffset.UtcNow;
        ConFilas(Fila(ahora.AddSeconds(30), ahora));

        Assert.Equal("vigente", Assert.Single((await Handler().Handle(Consulta())).Items).Vigencia);
    }

    [Fact]
    public async Task Sin_filtro_de_vigencia_no_se_descarta_nada()
    {
        var ahora = DateTimeOffset.UtcNow;
        ConFilas(Fila(ahora.AddDays(3), ahora), Fila(ahora.AddDays(-3), ahora), Fila(null, ahora));

        Assert.Equal(3, (await Handler().Handle(Consulta())).Total);
    }

    // ------------------------------------------------------------------ Resultado en cada fila

    [Theory]
    [InlineData(ResultadoCotizacion.SinDefinir, "SinDefinir")]
    [InlineData(ResultadoCotizacion.Ganada, "Ganada")]
    [InlineData(ResultadoCotizacion.Perdida, "Perdida")]
    public async Task Cada_fila_trae_el_resultado_de_su_cotizacion(ResultadoCotizacion resultado, string esperado)
    {
        ConFilas(Fila(null, DateTimeOffset.UtcNow, resultado));

        Assert.Equal(esperado, Assert.Single((await Handler().Handle(Consulta())).Items).Resultado);
    }

    [Fact]
    public async Task Sin_cotizacion_el_resultado_es_null()
    {
        ConFilas(Fila(null, DateTimeOffset.UtcNow, resultado: null));

        Assert.Null(Assert.Single((await Handler().Handle(Consulta())).Items).Resultado);
    }

    // ------------------------------------------------------------------ filtro resultado y atención por defecto

    [Theory]
    [InlineData("esperando")]
    [InlineData("ganada")]
    [InlineData("perdida")]
    [InlineData("  GANADA ")]
    public async Task Con_resultado_y_sin_atencion_se_trata_la_atencion_como_todas(string resultado)
    {
        ConFilas();

        await Handler().Handle(Consulta(atencion: null, resultado: resultado));

        _repo.Verify(r => r.BandejaFilasAsync(
            "todas", null, null, null, null, resultado.Trim().ToLowerInvariant(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_resultado_y_sin_atencion_sigue_siendo_la_cola_de_pendientes(string? atencion)
    {
        ConFilas();

        await Handler().Handle(Consulta(atencion: atencion));

        _repo.Verify(r => r.BandejaFilasAsync(
            "pendientes", null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_atencion_explicita_se_respeta_aunque_haya_resultado()
    {
        ConFilas();

        await Handler().Handle(Consulta(atencion: "administrador", resultado: "ganada"));

        _repo.Verify(r => r.BandejaFilasAsync(
            "administrador", null, null, null, null, "ganada", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_resultado_desconocido_es_un_error_no_se_ignora_en_silencio()
    {
        ConFilas();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(Consulta(resultado: "tal_vez")));

        Assert.Equal("RESULTADO_INVALIDO", ex.Code);
        _repo.Verify(r => r.BandejaFilasAsync(
            It.IsAny<string?>(), It.IsAny<EstadoSolicitud?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------ rol Comercial

    [Theory]
    [InlineData("pendientes", null)]
    [InlineData(null, "esperando")]
    [InlineData(null, "ganada")]
    [InlineData(null, "perdida")]
    public async Task Con_rol_Comercial_los_filtros_nuevos_se_piden_solo_sobre_sus_solicitudes(string? atencion, string? resultado)
    {
        var yo = Guid.NewGuid();
        _usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Comercial);
        _usuario.SetupGet(u => u.Id).Returns(yo);
        ConFilas();

        await Handler().Handle(Consulta(atencion: atencion, resultado: resultado));

        _repo.Verify(r => r.BandejaFilasAsync(
            It.IsAny<string?>(), It.IsAny<EstadoSolicitud?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
            yo, resultado, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RolUsuario.AdminTarifas)]
    [InlineData(RolUsuario.CoordinadorFletes)]
    [InlineData(RolUsuario.Admin)]
    public async Task Los_demas_roles_ven_todo_con_los_filtros_nuevos(RolUsuario rol)
    {
        _usuario.SetupGet(u => u.Rol).Returns(rol);
        _usuario.SetupGet(u => u.Id).Returns(Guid.NewGuid());
        ConFilas();

        await Handler().Handle(Consulta(atencion: null, resultado: "esperando"));

        _repo.Verify(r => r.BandejaFilasAsync(
            It.IsAny<string?>(), It.IsAny<EstadoSolicitud?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(),
            null, "esperando", It.IsAny<CancellationToken>()), Times.Once);
    }
}
