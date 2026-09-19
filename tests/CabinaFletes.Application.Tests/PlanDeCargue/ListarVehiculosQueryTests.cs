using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.PlanDeCargue;

/// <summary>Issue #166: GET /api/vehiculos pagina (page/pageSize) aunque el
/// catálogo hoy sea chico - contrato consistente con el resto de los listados.</summary>
public class ListarVehiculosQueryTests
{
    private readonly Mock<IVehiculoCubicacionRepository> _repo = new();

    private ListarVehiculosQueryHandler Handler() => new(_repo.Object);

    [Fact]
    public async Task Reenvia_page_y_pageSize_al_repositorio_y_expone_el_total()
    {
        var vehiculo = new VehiculoCubicacion { Nombre = "Furgón" };
        _repo.Setup(r => r.ListarAsync(2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { vehiculo }, 25));

        var pagina = await Handler().Handle(new ListarVehiculosQuery(Page: 2, PageSize: 10));

        Assert.Equal(25, pagina.Total);
        Assert.Single(pagina.Items);
        Assert.Equal("Furgón", pagina.Items[0].Nombre);
        _repo.Verify(r => r.ListarAsync(2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_parametros_pide_la_primera_pagina_con_el_default()
    {
        _repo.Setup(r => r.ListarAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<VehiculoCubicacion>(), 0));

        await Handler().Handle(new ListarVehiculosQuery());

        _repo.Verify(r => r.ListarAsync(1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }
}
