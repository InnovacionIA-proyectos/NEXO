using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] GET /api/planes-cargue/por-linea/{lineaId} — sondeo de
/// <see cref="clsCrearPlanDesdeLineaCommand"/>, contra la proyección local.
/// </summary>
public class clsObtenerPlanesPorLineaQueryTests
{
    private readonly Mock<IPlanCargueRepository> _planes = new();

    private static readonly Guid LineaId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private clsObtenerPlanesPorLineaQueryHandler Handler() => new(_planes.Object);

    [Fact]
    public async Task Mientras_no_llega_ningun_plan_devuelve_lista_vacia_no_null()
    {
        _planes.Setup(p => p.ObtenerPorLineaAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<clsPlanCargue>());

        var r = await Handler().Handle(new clsObtenerPlanesPorLineaQuery(LineaId));

        Assert.NotNull(r);
        Assert.Empty(r);
    }

    [Fact]
    public async Task Con_una_flota_de_dos_vehiculos_calcula_totalvehiculos_y_conteos()
    {
        var vehiculo1 = new clsPlanCargue
        {
            Id = Guid.NewGuid(), LineaSolicitudId = LineaId, NumeroVehiculo = 1,
            Colocaciones = new List<clsColocacionCargue>
            {
                new() { Cargada = true }, new() { Cargada = true }, new() { Cargada = false },
            },
        };
        var vehiculo2 = new clsPlanCargue
        {
            Id = Guid.NewGuid(), LineaSolicitudId = LineaId, NumeroVehiculo = 2,
            Colocaciones = new List<clsColocacionCargue> { new() { Cargada = true } },
        };
        _planes.Setup(p => p.ObtenerPorLineaAsync(LineaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { vehiculo1, vehiculo2 });

        var r = await Handler().Handle(new clsObtenerPlanesPorLineaQuery(LineaId));

        Assert.Equal(2, r.Count);
        Assert.All(r, item => Assert.Equal(2, item.TotalVehiculos));
        Assert.Equal(2, r[0].UnidadesCargadas);
        Assert.Equal(3, r[0].UnidadesTotales);
        Assert.Equal(1, r[1].UnidadesCargadas);
        Assert.Equal(1, r[1].UnidadesTotales);
    }
}
