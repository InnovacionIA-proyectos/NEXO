using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.ServiciosConexos;

/// <summary>El precio de venta de un servicio de la biblioteca: opcional al crear, con PATCH que lo cambia, no lo toca si no viene y lo puede borrar.</summary>
public class clsPrecioVentaServicioConexoTests
{
    private readonly Mock<IServiciosConexosRepository> _repo = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public clsPrecioVentaServicioConexoTests()
    {
        _repo.Setup(r => r.ExisteDimensionesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), default))
            .ReturnsAsync(false);
    }

    private clsServicioConexo Existente(decimal? precioVenta)
    {
        var s = new clsServicioConexo
        {
            Id = Guid.NewGuid(), CodigoServicio = "ALM_CALI", TipoServicio = "Almacenamiento", Origen = "CALI",
            CostoUnitario = 1000m, PrecioVenta = precioVenta, Unidad = "Por estiba", Vigencia = new DateOnly(2026, 1, 1),
        };
        _repo.Setup(r => r.ObtenerAsync(s.Id, default)).ReturnsAsync(s);
        return s;
    }

    private Task<Nexo.Application.CasosDeUso.ServiciosConexos.DTOs.clsServicioConexoDto> Editar(
        Guid id, decimal? precioVenta = null, bool limpiar = false, decimal? costo = null) =>
        new clsEditarServicioConexoCommandHandler(_repo.Object, _usuarioActual.Object, _uow.Object)
            .Handle(new clsEditarServicioConexoCommand(id, null, null, null, null, null, null, null, costo, null, null, null, null, precioVenta, limpiar));

    [Fact]
    public async Task Crear_con_precio_de_venta_lo_guarda_redondeado_a_dos_decimales_y_lo_devuelve()
    {
        var dto = await new clsCrearServicioConexoCommandHandler(_repo.Object, _usuarioActual.Object, _uow.Object).Handle(
            new clsCrearServicioConexoCommand("PRE_BSAT", "Precinto", null, null, null, "BSAT", null, 5000m, "Por unidad",
                new DateOnly(2026, 12, 31), "adicional", true, 7500.555m));

        Assert.Equal(7500.56m, dto.PrecioVenta);
        _repo.Verify(r => r.Agregar(It.Is<clsServicioConexo>(s => s.PrecioVenta == 7500.56m)), Times.Once);
    }

    [Fact]
    public async Task Crear_sin_precio_de_venta_lo_deja_vacio()
    {
        var dto = await new clsCrearServicioConexoCommandHandler(_repo.Object, _usuarioActual.Object, _uow.Object).Handle(
            new clsCrearServicioConexoCommand("PRE_BSAT", "Precinto", null, null, null, "BSAT", null, 5000m, "Por unidad",
                new DateOnly(2026, 12, 31), "adicional", true));

        Assert.Null(dto.PrecioVenta);
    }

    [Fact]
    public async Task Editar_cambia_el_precio_de_venta_y_lo_deja_en_la_auditoria()
    {
        var s = Existente(precioVenta: 1200m);

        var dto = await Editar(s.Id, precioVenta: 1500m);

        Assert.Equal(1500m, dto.PrecioVenta);
        _repo.Verify(r => r.RegistrarCambioDePrecioVenta(s, 1200m), Times.Once);
    }

    [Fact]
    public async Task Un_PATCH_sin_precio_de_venta_no_lo_toca_ni_audita()
    {
        var s = Existente(precioVenta: 1200m);

        var dto = await Editar(s.Id, costo: 1100m);

        Assert.Equal((1100m, 1200m), (dto.CostoUnitario, dto.PrecioVenta));
        _repo.Verify(r => r.RegistrarCambioDePrecioVenta(It.IsAny<clsServicioConexo>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Fact]
    public async Task Limpiar_el_precio_de_venta_lo_vuelve_vacio()
    {
        var s = Existente(precioVenta: 1200m);

        var dto = await Editar(s.Id, limpiar: true);

        Assert.Null(dto.PrecioVenta);
        _repo.Verify(r => r.RegistrarCambioDePrecioVenta(s, 1200m), Times.Once);
    }

    [Fact]
    public async Task Pedir_un_precio_y_limpiarlo_a_la_vez_es_un_error()
    {
        var s = Existente(precioVenta: 1200m);

        var error = await Assert.ThrowsAsync<clsValidationFailedException>(() => Editar(s.Id, precioVenta: 900m, limpiar: true));

        Assert.Equal("PRECIO_VENTA_AMBIGUO", error.Code);
        Assert.Equal(1200m, s.PrecioVenta);
    }
}
