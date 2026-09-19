using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
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
public class ObtenerBandejaQueryTests
{
    private readonly Mock<ISolicitudesRepository> _repo = new();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    private ObtenerBandejaQueryHandler Handler() => new(_repo.Object, Reloj);

    private static BandejaFiltro FiltroTodas() => new(null, "todas", null, null, null);

    private void ConSolicitudes(int cantidad)
    {
        _repo.Setup(r => r.ParametrosScoreAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ParametrosScore());

        var filas = Enumerable.Range(0, cantidad).Select(i => new BandejaFilaRaw(
            Guid.NewGuid(), $"Cliente {i}", TipoSolicitud.Spot, EstadoSolicitud.Pendiente,
            DateTimeOffset.UtcNow.AddDays(1 + i), 1_000_000m * (i + 1), 0.5m,
            DateTimeOffset.UtcNow.AddHours(-i), 1, null, 1, 0, 0)).ToList();

        _repo.Setup(r => r.BandejaFilasAsync(
                "todas", null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filas);
    }

    [Fact]
    public async Task Devuelve_el_total_real_aunque_la_pagina_sea_mas_chica()
    {
        ConSolicitudes(5);

        var pagina = await Handler().Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 2));

        Assert.Equal(5, pagina.Total);
        Assert.Equal(2, pagina.Items.Count);
    }

    [Fact]
    public async Task Recorrer_todas_las_paginas_reconstruye_el_total_sin_duplicados_ni_huecos()
    {
        ConSolicitudes(5);
        var handler = Handler();

        var p1 = await handler.Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 2));
        var p2 = await handler.Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 2, PageSize: 2));
        var p3 = await handler.Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 3, PageSize: 2));

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

        var p1 = await handler.Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 1, PageSize: 3));
        var p2 = await handler.Handle(new ObtenerBandejaQuery(FiltroTodas(), Page: 2, PageSize: 3));

        var scoresEnOrden = p1.Items.Concat(p2.Items).Select(i => i.ScorePrioridad).ToList();
        var esperado = scoresEnOrden.OrderByDescending(s => s).ToList();
        Assert.Equal(esperado, scoresEnOrden);
    }
}
