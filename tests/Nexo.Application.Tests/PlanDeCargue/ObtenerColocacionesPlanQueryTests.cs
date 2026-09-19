using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.PlanDeCargue.Entidades;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] GET /api/planes-cargue/{id}/colocaciones — reemplazo 1:1 de
/// <c>colocaciones()</c> (cubicador-api.service.ts). A diferencia de
/// ObtenerNoCargadasPlanQuery, NO filtra por <c>Cargada</c>: trae todas.
/// </summary>
public class ObtenerColocacionesPlanQueryTests
{
    private readonly Mock<IPlanCargueRepository> _planes = new();

    private static readonly Guid PlanId = Guid.Parse("eeee0000-0000-0000-0000-000000000001");

    private ObtenerColocacionesPlanQueryHandler Handler() => new(_planes.Object);

    [Fact]
    public async Task Con_el_plan_inexistente_lanza_PLAN_CARGUE_NO_ENCONTRADO()
    {
        _planes.Setup(p => p.ObtenerAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync((PlanCargue?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(new ObtenerColocacionesPlanQuery(PlanId)));

        Assert.Equal("PLAN_CARGUE_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Devuelve_TODAS_las_colocaciones_cargadas_y_no_cargadas()
    {
        var plan = new PlanCargue
        {
            Id = PlanId,
            Colocaciones = new List<ColocacionCargue>
            {
                new() { Id = Guid.NewGuid(), Cargada = true },
                new() { Id = Guid.NewGuid(), Cargada = false, CodigoRechazo = "NO_CABE" },
            },
        };
        _planes.Setup(p => p.ObtenerAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var r = await Handler().Handle(new ObtenerColocacionesPlanQuery(PlanId));

        Assert.Equal(2, r.Count);
        Assert.Contains(r, c => c.Cargada);
        Assert.Contains(r, c => !c.Cargada && c.CodigoRechazo == "NO_CABE");
    }
}
