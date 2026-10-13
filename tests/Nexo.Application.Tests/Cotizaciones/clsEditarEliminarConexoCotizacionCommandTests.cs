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

// S5-HU-18/#262: antes solo existía POST /conexos (siempre crea una fila nueva); un error
// de cantidad o de servicio no se podía deshacer. Estas pruebas fijan PATCH/DELETE.
public class clsEditarEliminarConexoCotizacionCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsEditarConexoCotizacionCommandHandler EditarHandler() =>
        new(_cotizaciones.Object, _auditoria.Object, _usuarioActual.Object, _uow.Object);

    private clsEliminarConexoCotizacionCommandHandler EliminarHandler() =>
        new(_cotizaciones.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object, _usuarioActual.Object, TimeProvider.System, _uow.Object);

    private static (clsCotizacion Cotizacion, clsCotizacionConexo Conexo) CotizacionConConexo(
        EstadoCotizacion estado = EstadoCotizacion.Borrador, decimal costoUnitario = 50_000m)
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicio = new clsServicioConexo
        {
            Id = Guid.NewGuid(), CodigoServicio = "CARGUE", TipoServicio = "Cargue", CostoUnitario = costoUnitario,
            Unidad = "Por unidad", Vigencia = DateOnly.FromDateTime(DateTime.UtcNow), Activo = true,
        };
        var conexo = new clsCotizacionConexo
        {
            Id = Guid.NewGuid(), CotizacionId = cotizacionId, CotizacionLineaId = lineaId,
            ServicioConexoId = servicio.Id, clsServicioConexo = servicio, Cantidad = 2, ValorAplicado = 100_000m,
        };
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = estado, Total = 100_000m };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        cotizacion.Conexos.Add(conexo);
        return (cotizacion, conexo);
    }

    [Fact]
    public async Task Editar_recalcula_el_valor_cuando_no_se_envia_uno_nuevo()
    {
        var (cotizacion, conexo) = CotizacionConConexo(costoUnitario: 50_000m);
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var dto = await EditarHandler().Handle(new clsEditarConexoCotizacionCommand(cotizacion.Id, conexo.Id, 3, null));

        var actualizado = Assert.Single(dto.Conexos);
        Assert.Equal(3, actualizado.Cantidad);
        Assert.Equal(150_000m, actualizado.ValorAplicado); // 50.000 * 3
        Assert.Equal(150_000m, dto.Total);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Editar_respeta_el_valor_explicito_si_se_envia()
    {
        var (cotizacion, conexo) = CotizacionConConexo();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var dto = await EditarHandler().Handle(new clsEditarConexoCotizacionCommand(cotizacion.Id, conexo.Id, 1, 77_000m));

        Assert.Equal(77_000m, Assert.Single(dto.Conexos).ValorAplicado);
    }

    [Fact]
    public async Task Editar_cantidad_con_mas_de_2_decimales_lanza_ValidationFailedException()
    {
        var (cotizacion, conexo) = CotizacionConConexo();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            EditarHandler().Handle(new clsEditarConexoCotizacionCommand(cotizacion.Id, conexo.Id, 1.555m, null)));

        Assert.Equal("CANTIDAD_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Editar_conexo_inexistente_lanza_NotFoundException()
    {
        var (cotizacion, _) = CotizacionConConexo();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            EditarHandler().Handle(new clsEditarConexoCotizacionCommand(cotizacion.Id, Guid.NewGuid(), 1, 1000m)));

        Assert.Equal("CONEXO_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Editar_cotizacion_no_editable_lanza_ConflictException()
    {
        var (cotizacion, conexo) = CotizacionConConexo(EstadoCotizacion.Entregada);
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        await Assert.ThrowsAsync<clsConflictException>(() =>
            EditarHandler().Handle(new clsEditarConexoCotizacionCommand(cotizacion.Id, conexo.Id, 1, 1000m)));

        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Eliminar_quita_el_conexo_y_recalcula_el_total()
    {
        var (cotizacion, conexo) = CotizacionConConexo();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var dto = await EliminarHandler().Handle(new clsEliminarConexoCotizacionCommand(cotizacion.Id, conexo.Id));

        Assert.Empty(dto.Conexos);
        Assert.Equal(0, dto.Total);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Eliminar_conexo_inexistente_lanza_NotFoundException()
    {
        var (cotizacion, _) = CotizacionConConexo();
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        await Assert.ThrowsAsync<clsNotFoundException>(() =>
            EliminarHandler().Handle(new clsEliminarConexoCotizacionCommand(cotizacion.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task Eliminar_cotizacion_con_resultado_lanza_COTIZACION_CERRADA_sin_guardar()
    {
        // S5-HU-20.1/#286: Entregada ya se puede modificar; solo Ganada/Perdida cierra.
        var (cotizacion, conexo) = CotizacionConConexo(EstadoCotizacion.Entregada);
        cotizacion.Resultado = ResultadoCotizacion.Perdida;
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            EliminarHandler().Handle(new clsEliminarConexoCotizacionCommand(cotizacion.Id, conexo.Id)));

        Assert.Equal("COTIZACION_CERRADA", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
