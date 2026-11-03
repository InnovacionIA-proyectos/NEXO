using Nexo.Application.CasosDeUso.TarifasGanadas;
using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;
using Moq;

namespace Nexo.Application.Tests.TarifasGanadas;

/// <summary>Issue #166: la búsqueda de la Biblioteca de Tarifas Ganadas
/// reemplaza el Take(50) fijo (sin total ni forma de pedir más) por page/pageSize real.</summary>
public class clsBuscarBibliotecaTarifasQueryTests
{
    private readonly Mock<IHistoricoTarifasPort> _historico = new();

    private clsBuscarBibliotecaTarifasQueryHandler Handler() => new(_historico.Object);

    [Fact]
    public async Task Reenvia_page_y_pageSize_y_expone_el_total_real()
    {
        var item = new clsBibliotecaItemDto(Guid.NewGuid(), "Bogotá → Medellín", "Furgón", null, 1000m, DateOnly.FromDateTime(DateTime.UtcNow), "vigente", true);
        _historico.Setup(h => h.BuscarAsync("bogota", null, null, null, 2, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { item }, 84));

        var pagina = await Handler().Handle(new clsBuscarBibliotecaTarifasQuery("bogota", null, null, null, Page: 2, PageSize: 25));

        Assert.Equal(84, pagina.Total);
        Assert.Single(pagina.Items);
        _historico.Verify(h => h.BuscarAsync("bogota", null, null, null, 2, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_parametros_pide_la_primera_pagina_con_el_default()
    {
        _historico.Setup(h => h.BuscarAsync(null, null, null, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<clsBibliotecaItemDto>(), 0));

        await Handler().Handle(new clsBuscarBibliotecaTarifasQuery(null, null, null, null));

        _historico.Verify(h => h.BuscarAsync(null, null, null, null, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }
}
