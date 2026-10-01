using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

// S4-HU-15.4/#250: un conexo ya no puede quedar "general" (sin línea) — debe
// anclarse a una línea puntual de la cotización.
public class clsAgregarConexosCotizacionCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IServiciosConexosPort> _servicios = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAgregarConexosCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _servicios.Object, _auditoria.Object, _usuarioActual.Object, _uow.Object);

    private static clsServicioConexo ServicioActivo(Guid id) => new()
    {
        Id = id, CodigoServicio = "CARGUE", TipoServicio = "Cargue", CostoUnitario = 50_000m,
        Unidad = "Por unidad", Vigencia = DateOnly.FromDateTime(DateTime.UtcNow), Activo = true,
    };

    [Fact]
    public async Task Linea_que_no_pertenece_a_la_cotizacion_lanza_ValidationFailedException()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaDeOtraCotizacion = Guid.NewGuid();
        var servicioId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = Guid.NewGuid(), CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaDeOtraCotizacion, servicioId, 1, null)])));

        Assert.Equal("LINEA_INVALIDA", ex.Code);
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<clsCotizacion>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Con_linea_valida_el_conexo_queda_ligado_a_esa_linea()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicioId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);
        _servicios.Setup(s => s.ObtenerPorIdAsync(servicioId, default)).ReturnsAsync(ServicioActivo(servicioId));

        var dto = await Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, servicioId, 2, null)]));

        var conexo = Assert.Single(dto.Conexos);
        Assert.Equal(lineaId, conexo.CotizacionLineaId);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Cotizacion_no_editable_lanza_ConflictException_sin_tocar_el_repositorio()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Entregada };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);

        await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, Guid.NewGuid(), 1, null)])));

        _servicios.Verify(s => s.ObtenerPorIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}
