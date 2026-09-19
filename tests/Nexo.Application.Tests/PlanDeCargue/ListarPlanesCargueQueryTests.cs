using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>Issue #166: GET /api/planes-cargue reemplaza el tope fijo de 100
/// por page/pageSize real.</summary>
public class ListarPlanesCargueQueryTests
{
    private readonly Mock<IPlanCargueRepository> _repo = new();

    private ListarPlanesCargueQueryHandler Handler() => new(_repo.Object);

    [Fact]
    public async Task Reenvia_page_y_pageSize_y_expone_el_total_real()
    {
        var plan = new PlanCargue { Nombre = "Plan 1" };
        _repo.Setup(r => r.ListarRecientesAsync(2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { plan }, 137));

        var pagina = await Handler().Handle(new ListarPlanesCargueQuery(Page: 2, PageSize: 20));

        Assert.Equal(137, pagina.Total);
        Assert.Single(pagina.Items);
        Assert.Equal("Plan 1", pagina.Items[0].Nombre);
        _repo.Verify(r => r.ListarRecientesAsync(2, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_parametros_pide_la_primera_pagina_con_el_default()
    {
        _repo.Setup(r => r.ListarRecientesAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<PlanCargue>(), 0));

        await Handler().Handle(new ListarPlanesCargueQuery());

        _repo.Verify(r => r.ListarRecientesAsync(1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }
}
