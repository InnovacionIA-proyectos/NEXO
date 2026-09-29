using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.ServiciosConexos;

public class clsEditarServicioConexoCommandTests
{
    private readonly Mock<IServiciosConexosRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsEditarServicioConexoCommandHandler Handler() => new(_repo.Object, _usuarioActual.Object, _uow.Object);

    [Fact]
    public async Task Editar_servicio_inexistente_lanza_NotFound()
    {
        _repo.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), default)).ReturnsAsync((clsServicioConexo?)null);

        await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsEditarServicioConexoCommand(Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, null, null)));
    }

    [Fact]
    public async Task Solo_actualiza_los_campos_enviados()
    {
        var id = Guid.NewGuid();
        var servicio = new clsServicioConexo
        {
            Id = id, CodigoServicio = "ALM_CALI", TipoServicio = "Almacenamiento", Origen = "CALI",
            CostoUnitario = 1000m, Unidad = "Por estiba", Vigencia = new DateOnly(2026, 1, 1),
        };
        _repo.Setup(r => r.ObtenerAsync(id, default)).ReturnsAsync(servicio);
        _repo.Setup(r => r.ExisteDimensionesAsync(
                "ALM_CALI", "CALI", clsServicioConexo.NoAplica, clsServicioConexo.NoAplica,
                clsServicioConexo.NoAplica, clsServicioConexo.NoAplica, id, default))
            .ReturnsAsync(false);

        var dto = await Handler().Handle(new clsEditarServicioConexoCommand(
            id, null, null, null, null, null, null, null, 1500m, null, null, null, null));

        Assert.Equal(1500m, dto.CostoUnitario);
        Assert.Equal("Almacenamiento", dto.TipoServicio); // sin cambios
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Cambiar_una_dimension_hacia_una_combinacion_ya_existente_lanza_Conflict()
    {
        var id = Guid.NewGuid();
        var servicio = new clsServicioConexo { Id = id, CodigoServicio = "ALM_CALI", TipoServicio = "Almacenamiento", Origen = "CALI", CostoUnitario = 1000m, Unidad = "Por estiba" };
        _repo.Setup(r => r.ObtenerAsync(id, default)).ReturnsAsync(servicio);
        _repo.Setup(r => r.ExisteDimensionesAsync(
                "ALM_CALI", "MEDELLIN", clsServicioConexo.NoAplica, clsServicioConexo.NoAplica,
                clsServicioConexo.NoAplica, clsServicioConexo.NoAplica, id, default))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarServicioConexoCommand(id, null, null, "MEDELLIN", null, null, null, null, null, null, null, null, null)));
    }
}
