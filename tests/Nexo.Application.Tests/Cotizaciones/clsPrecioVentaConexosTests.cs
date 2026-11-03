using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Notificaciones;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// Un conexo se cotiza a su precio de venta (el de la biblioteca o, si no tiene, su costo), que queda fijo al agregarlo. El costo
/// (<c>ValorAplicado</c>) sigue siendo el costo y puede venir en el request sin cambiar lo que se cotiza.
/// </summary>
public class clsPrecioVentaConexosTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IServiciosConexosPort> _servicios = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Guid _actor = Guid.NewGuid();

    public clsPrecioVentaConexosTests() => _usuario.Setup(u => u.Id).Returns(_actor);

    private clsAgregarConexosCotizacionCommandHandler Agregar() => new(
        _cotizaciones.Object, _servicios.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object,
        _usuario.Object, TimeProvider.System, _uow.Object);

    private clsEditarConexoCotizacionCommandHandler Editar() => new(_cotizaciones.Object, _auditoria.Object, _usuario.Object, _uow.Object);

    private clsEliminarConexoCotizacionCommandHandler Quitar() => new(
        _cotizaciones.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private static clsServicioConexo Servicio(decimal costo, decimal? precioVenta) => new()
    {
        Id = Guid.NewGuid(), CodigoServicio = "CARGUE", TipoServicio = "Cargue", CostoUnitario = costo, PrecioVenta = precioVenta,
        Unidad = "Por unidad", Vigencia = DateOnly.FromDateTime(DateTime.UtcNow), Activo = true,
    };

    private (clsCotizacion C, Guid LineaId) Cotizacion(EstadoCotizacion estado = EstadoCotizacion.Borrador, Guid? creadorSolicitud = null)
    {
        var solicitudId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var c = new clsCotizacion { Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado, Resultado = ResultadoCotizacion.SinDefinir };
        c.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = c.Id, ValorFlete = 1_000_000m });
        c.Total = 1_000_000m;
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, CreadoPor = creadorSolicitud ?? Guid.NewGuid() });
        return (c, lineaId);
    }

    private void Existe(clsServicioConexo s) =>
        _servicios.Setup(x => x.ObtenerPorIdAsync(s.Id, It.IsAny<CancellationToken>())).ReturnsAsync(s);

    [Fact]
    public async Task Un_servicio_con_precio_de_venta_se_cotiza_a_ese_precio_y_el_costo_queda_aparte()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 50_000m, precioVenta: 80_000m);
        Existe(servicio);

        var dto = await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 3, null)]));

        var conexo = Assert.Single(dto.Conexos);
        Assert.Equal((80_000m, 240_000m, 150_000m), (conexo.PrecioVentaUnitario, conexo.ValorVenta, conexo.ValorAplicado));
        Assert.Equal(1_240_000m, dto.Total);
    }

    [Fact]
    public async Task Un_servicio_sin_precio_de_venta_se_cotiza_a_su_costo_y_nunca_queda_en_cero()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 50_000m, precioVenta: null);
        Existe(servicio);

        var dto = await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 2, null)]));

        var conexo = Assert.Single(dto.Conexos);
        Assert.Equal((50_000m, 100_000m, 100_000m), (conexo.PrecioVentaUnitario, conexo.ValorVenta, conexo.ValorAplicado));
        Assert.Equal(1_100_000m, dto.Total);
    }

    [Fact]
    public async Task Un_valorAplicado_en_el_request_cambia_el_costo_pero_no_lo_que_se_cotiza()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 50_000m, precioVenta: 80_000m);
        Existe(servicio);

        var dto = await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 3, 999_999m)]));

        var conexo = Assert.Single(dto.Conexos);
        Assert.Equal((999_999m, 240_000m), (conexo.ValorAplicado, conexo.ValorVenta));
        Assert.Equal(1_240_000m, dto.Total);
    }

    [Fact]
    public async Task El_valor_de_venta_se_redondea_a_dos_decimales()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 100m, precioVenta: 100.01m);
        Existe(servicio);

        var dto = await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 0.33m, null)]));

        Assert.Equal(33.00m, Assert.Single(dto.Conexos).ValorVenta); // 100.01 × 0.33 = 33.0033
    }

    [Fact]
    public async Task Cambiar_el_precio_de_la_biblioteca_despues_no_cambia_un_conexo_ya_agregado()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 50_000m, precioVenta: 80_000m);
        Existe(servicio);
        await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 1, null)]));

        servicio.PrecioVenta = 200_000m;
        servicio.CostoUnitario = 150_000m;
        var conexo = Assert.Single(c.Conexos);
        var dto = await Editar().Handle(new clsEditarConexoCotizacionCommand(c.Id, conexo.Id, 4, null));

        var editado = Assert.Single(dto.Conexos);
        // El precio de venta quedó fijo en 80.000; el costo sí se recalcula con el de la biblioteca (comportamiento de siempre).
        Assert.Equal((80_000m, 320_000m, 600_000m), (editado.PrecioVentaUnitario, editado.ValorVenta, editado.ValorAplicado));
        Assert.Equal(1_320_000m, dto.Total);
    }

    [Fact]
    public async Task Editar_la_cantidad_con_un_valorAplicado_no_cambia_el_valor_de_venta()
    {
        var (c, lineaId) = Cotizacion();
        var servicio = Servicio(costo: 50_000m, precioVenta: 80_000m);
        Existe(servicio);
        await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 1, null)]));

        var dto = await Editar().Handle(new clsEditarConexoCotizacionCommand(c.Id, c.Conexos.Single().Id, 2, 777m));

        var editado = Assert.Single(dto.Conexos);
        Assert.Equal((777m, 160_000m), (editado.ValorAplicado, editado.ValorVenta));
    }

    [Fact]
    public async Task Los_avisos_tras_la_entrega_llevan_el_valor_de_venta_al_agregar_y_al_quitar()
    {
        var (c, lineaId) = Cotizacion(EstadoCotizacion.Entregada);
        c.EntregadaAt = DateTimeOffset.UtcNow;
        var servicio = Servicio(costo: 50_000m, precioVenta: 80_000m);
        Existe(servicio);
        var avisos = new List<clsConexosCotizacionCambiados>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()))
            .Callback<clsConexosCotizacionCambiados, CancellationToken>((m, _) => avisos.Add(m)).Returns(Task.CompletedTask);

        await Agregar().Handle(new clsAgregarConexosCotizacionCommand(c.Id, [new clsConexoItem(lineaId, servicio.Id, 3, null)]));
        await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, c.Conexos.Single().Id));

        Assert.Equal(2, avisos.Count);
        Assert.All(avisos, a => Assert.Equal(240_000m, Assert.Single(a.Items).Valor));
        Assert.Equal(1_240_000m, avisos[0].NuevoTotal);
        Assert.Equal(1_000_000m, avisos[1].NuevoTotal);
    }
}
