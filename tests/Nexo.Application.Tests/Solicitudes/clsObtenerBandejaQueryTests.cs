using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// Issue #166: la Bandeja Priorizada no tenía ningún tope - devolvía todo lo que
/// cumpliera el filtro en un solo request. El score se recalcula en memoria por
/// fila (depende de "ahora", no es traducible a SQL), así que la paginación se
/// aplica DESPUÉS de ordenar todo lo que trajo el repositorio.
/// </summary>
public class clsObtenerBandejaQueryTests
{
    private readonly Mock<ISolicitudesRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    private clsObtenerBandejaQueryHandler Handler() => new(_repo.Object, Reloj, _usuarioActual.Object);

    private static clsBandejaFiltro FiltroTodas() => new(null, "todas", null, null, null);

    private void ConSolicitudes(int cantidad, Guid? creadoPorEsperado = null)
    {
        _repo.Setup(r => r.ParametrosScoreAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new clsParametrosScore());

        var filas = Enumerable.Range(0, cantidad).Select(i => new clsBandejaFilaRaw(
            Guid.NewGuid(), $"clsCliente {i}", TipoSolicitud.Spot, EstadoSolicitud.Pendiente,
            DateTimeOffset.UtcNow.AddDays(1 + i), 1_000_000m * (i + 1), 0.5m,
            DateTimeOffset.UtcNow.AddHours(-i), 1, null, 1, 0, 0)).ToList();

        _repo.Setup(r => r.BandejaFilasAsync(
                "todas", null, null, null, creadoPorEsperado, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filas);
    }

    [Fact]
    public async Task Devuelve_el_total_real_aunque_la_pagina_sea_mas_chica()
    {
        ConSolicitudes(5);

        var pagina = await Handler().Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 2));

        Assert.Equal(5, pagina.Total);
        Assert.Equal(2, pagina.Items.Count);
    }

    [Fact]
    public async Task Recorrer_todas_las_paginas_reconstruye_el_total_sin_duplicados_ni_huecos()
    {
        ConSolicitudes(5);
        var handler = Handler();

        var p1 = await handler.Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 2));
        var p2 = await handler.Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 2, PageSize: 2));
        var p3 = await handler.Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 3, PageSize: 2));

        Assert.Equal(2, p1.Items.Count);
        Assert.Equal(2, p2.Items.Count);
        Assert.Single(p3.Items); // 5 filas, pageSize 2 -> ultima pagina con 1

        var idsRecorridos = p1.Items.Concat(p2.Items).Concat(p3.Items).Select(i => i.Id).ToList();
        Assert.Equal(5, idsRecorridos.Distinct().Count());
    }

    [Fact]
    public async Task Queda_ordenada_por_score_descendente_dentro_de_cada_pagina_y_entre_paginas()
    {
        ConSolicitudes(6);
        var handler = Handler();

        var p1 = await handler.Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 3));
        var p2 = await handler.Handle(new clsObtenerBandejaQuery(FiltroTodas(), Page: 2, PageSize: 3));

        var scoresEnOrden = p1.Items.Concat(p2.Items).Select(i => i.ScorePrioridad).ToList();
        var esperado = scoresEnOrden.OrderByDescending(s => s).ToList();
        Assert.Equal(esperado, scoresEnOrden);
    }

    // ------------------------------------------ aislamiento por comercial (issue #199)

    [Fact]
    public async Task Un_comercial_solo_pide_al_repositorio_sus_propias_solicitudes()
    {
        var comercialId = Guid.NewGuid();
        _usuarioActual.SetupGet(u => u.Rol).Returns(RolUsuario.Comercial);
        _usuarioActual.SetupGet(u => u.Id).Returns(comercialId);
        ConSolicitudes(3, creadoPorEsperado: comercialId);

        var pagina = await Handler().Handle(new clsObtenerBandejaQuery(FiltroTodas()));

        Assert.Equal(3, pagina.Total);
        _repo.Verify(r => r.BandejaFilasAsync(
            "todas", null, null, null, comercialId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(RolUsuario.AdminBiblioteca)] // Mesa de Costeo: no se ve afectado (criterio 3 de la HU)
    [InlineData(RolUsuario.Admin)]
    [InlineData(RolUsuario.AdminTarifas)]
    public async Task Roles_distintos_de_comercial_ven_la_bandeja_completa_sin_filtro(RolUsuario rol)
    {
        _usuarioActual.SetupGet(u => u.Rol).Returns(rol);
        _usuarioActual.SetupGet(u => u.Id).Returns(Guid.NewGuid());
        ConSolicitudes(3, creadoPorEsperado: null);

        var pagina = await Handler().Handle(new clsObtenerBandejaQuery(FiltroTodas()));

        Assert.Equal(3, pagina.Total);
        _repo.Verify(r => r.BandejaFilasAsync(
            "todas", null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
