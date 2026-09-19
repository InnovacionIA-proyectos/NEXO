using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;
using Moq;

namespace Nexo.Application.Tests.PlanDeCargue;

/// <summary>
/// Issue #155: DELETE /api/vehiculos/{id} respondía 202 siempre, incluso cuando el
/// vehículo estaba referenciado por un plan — el rechazo de Cubicador (VEHICULO_EN_USO)
/// se perdía en el camino async y el comando quedaba "procesando" para siempre sin avisar
/// a nadie. El fix valida síncrono, ANTES de publicar, contra la fila espejo de
/// PlanCargue.
/// </summary>
public class EliminarVehiculoCommandTests
{
    private readonly Mock<ICubicacionPort> _cubicacion = new();
    private readonly Mock<IPlanCargueRepository> _planes = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid VehiculoId = Guid.Parse("dddd0000-0000-0000-0000-000000000001");

    private EliminarVehiculoCommandHandler Handler() => new(_cubicacion.Object, _planes.Object, _uow.Object);

    private static EliminarVehiculoCommand Comando() => new(VehiculoId);

    [Fact]
    public async Task Un_vehiculo_referenciado_por_un_plan_se_rechaza_sin_publicar_nada()
    {
        _planes.Setup(r => r.ExisteConVehiculoAsync(VehiculoId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("VEHICULO_EN_USO", ex.Code);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EliminarVehiculoInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_vehiculo_sin_planes_asociados_publica_y_responde_procesando()
    {
        _planes.Setup(r => r.ExisteConVehiculoAsync(VehiculoId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var r = await Handler().Handle(Comando());

        Assert.Equal("procesando", r.Estado);
        Assert.Equal(VehiculoId, r.CorrelationId);
        _cubicacion.Verify(c => c.PublicarComandoAsync(
            "eliminar_vehiculo", VehiculoId.ToString(), It.IsAny<EliminarVehiculoInput>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
