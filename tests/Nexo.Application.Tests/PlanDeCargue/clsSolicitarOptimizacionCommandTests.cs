using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.PlanDeCargue.Entidades;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// S2-HU-08.1 (#29) y S2-HU-08.2 (#30): pedir la optimización no bloquea, y con la
/// compuerta de paridad cerrada NO se toca el plan ni se publica nada — "en
/// verificación" antes de cualquier efecto, sin fallback al PoC.
/// </summary>
public class clsSolicitarOptimizacionCommandTests
{
    private readonly Mock<IParidadCubicadorPort> _paridad = new();
    private readonly Mock<IPlanCargueRepository> _planes = new();
    private readonly Mock<ICubicacionPort> _cubicacion = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid PlanId = Guid.Parse("cccc0000-0000-0000-0000-000000000001");

    private clsSolicitarOptimizacionCommandHandler Handler() =>
        new(_paridad.Object, _planes.Object, _cubicacion.Object, _uow.Object);

    private static clsSolicitarOptimizacionCommand Comando() => new(PlanId, null, null, null, null);

    private clsPlanCargue PlanListo()
    {
        var plan = new clsPlanCargue
        {
            Id = PlanId, Nombre = "Plan 1", VehiculoId = Guid.NewGuid(),
            Estado = "borrador", Estrategia = "Combinada", Algoritmo = "bfd",
        };
        _paridad.SetupGet(p => p.Habilitada).Returns(true);
        _planes.Setup(r => r.ObtenerAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        return plan;
    }

    [Fact]
    public async Task Con_la_compuerta_cerrada_no_toca_el_plan_ni_publica_nada()
    {
        _paridad.SetupGet(p => p.Habilitada).Returns(false);

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Handler().Handle(Comando()));

        Assert.Equal("CUBICACION_EN_VERIFICACION", ex.Code);
        _planes.Verify(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _cubicacion.Verify(c => c.SolicitarCubicacionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_la_compuerta_abierta_deja_el_plan_procesando_y_responde_202()
    {
        PlanListo();

        var r = await Handler().Handle(Comando());

        Assert.Equal("procesando", r.Estado);
        Assert.Equal(PlanId, r.PlanCargueId);
        Assert.Equal($"/api/planes-cargue/{PlanId}", r.ConsultarEn);
    }

    [Fact]
    public async Task Usa_la_estrategia_y_algoritmo_ya_guardados_en_el_plan_si_no_los_mandan()
    {
        PlanListo();

        await Handler().Handle(Comando());

        _cubicacion.Verify(c => c.SolicitarCubicacionAsync(
            PlanId, "Combinada", "bfd", null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Los_overrides_del_request_ganan_sobre_lo_guardado()
    {
        PlanListo();

        await Handler().Handle(new clsSolicitarOptimizacionCommand(PlanId, "Volumen", "ffd", 42, true));

        _cubicacion.Verify(c => c.SolicitarCubicacionAsync(
            PlanId, "Volumen", "ffd", 42, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publica_antes_de_guardar()
    {
        // El Outbox exige este orden — confirmado en runtime (docs/mensajeria/CUBICACION.md #5):
        // si se guarda antes de publicar, el mensaje no se entrega.
        PlanListo();
        var orden = new List<string>();
        _cubicacion.Setup(c => c.SolicitarCubicacionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(Comando());

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task Un_plan_inexistente_no_publica_nada()
    {
        _paridad.SetupGet(p => p.Habilitada).Returns(true);
        _planes.Setup(r => r.ObtenerAsync(PlanId, It.IsAny<CancellationToken>())).ReturnsAsync((clsPlanCargue?)null);

        await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando()));

        _cubicacion.Verify(c => c.SolicitarCubicacionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_plan_ya_procesando_no_se_puede_pedir_dos_veces()
    {
        var plan = PlanListo();
        plan.Estado = "procesando";

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("PLAN_CARGUE_EN_PROCESO", ex.Code);
    }
}
