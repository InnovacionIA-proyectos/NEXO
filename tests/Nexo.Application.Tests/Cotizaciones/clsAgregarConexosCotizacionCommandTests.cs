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
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAgregarConexosCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _servicios.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object,
        _usuarioActual.Object, TimeProvider.System, _uow.Object);

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
    // S5-HU-20.1/#286: antes "Entregada" también bloqueaba; ahora solo un resultado cierra.
    public async Task Cotizacion_con_resultado_lanza_COTIZACION_CERRADA_sin_tocar_el_repositorio()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Entregada, Resultado = ResultadoCotizacion.Ganada };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, Guid.NewGuid(), 1, null)])));

        Assert.Equal("COTIZACION_CERRADA", ex.Code);
        _servicios.Verify(s => s.ObtenerPorIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    // S5-HU-18/#262: Origen distingue "agregado a esta línea" de "agregado a todas las
    // líneas de una vez" — antes era una deducción del frontend, ahora es un dato real.
    [Fact]
    public async Task Sin_origen_explicito_el_conexo_queda_individual_por_defecto()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicioId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);
        _servicios.Setup(s => s.ObtenerPorIdAsync(servicioId, default)).ReturnsAsync(ServicioActivo(servicioId));

        var dto = await Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, servicioId, 1, null)]));

        Assert.Equal("individual", Assert.Single(dto.Conexos).Origen);
    }

    [Fact]
    public async Task Origen_general_queda_guardado_y_devuelto()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicioId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);
        _servicios.Setup(s => s.ObtenerPorIdAsync(servicioId, default)).ReturnsAsync(ServicioActivo(servicioId));

        var dto = await Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, servicioId, 1, null, "general")]));

        Assert.Equal("general", Assert.Single(dto.Conexos).Origen);
    }

    [Fact]
    public async Task Origen_invalido_lanza_ValidationFailedException()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, Guid.NewGuid(), 1, null, "masivo")])));

        Assert.Equal("ORIGEN_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task Cantidad_con_mas_de_2_decimales_lanza_ValidationFailedException()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, Guid.NewGuid(), 1.234m, null)])));

        Assert.Equal("CANTIDAD_INVALIDA", ex.Code);
    }

    [Fact] // el servicio completo (sus dimensiones incluidas) viaja en el DTO del conexo.
    public async Task El_conexo_devuelve_las_dimensiones_del_servicio()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicioId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador };
        cotizacion.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = cotizacionId });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacionId, default)).ReturnsAsync(cotizacion);
        var servicio = ServicioActivo(servicioId);
        servicio.Origen = "Bogotá";
        servicio.Destino = "Cali";
        _servicios.Setup(s => s.ObtenerPorIdAsync(servicioId, default)).ReturnsAsync(servicio);

        var dto = await Handler().Handle(
            new clsAgregarConexosCotizacionCommand(cotizacionId, [new clsConexoItem(lineaId, servicioId, 1, null)]));

        var conexo = Assert.Single(dto.Conexos);
        Assert.NotNull(conexo.Servicio);
        Assert.Equal("Bogotá", conexo.Servicio!.Origen);
        Assert.Equal("Cali", conexo.Servicio.Destino);
    }
}
