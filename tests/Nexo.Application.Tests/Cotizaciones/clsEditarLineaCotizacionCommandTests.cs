using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286: editar el flete MANUAL de una línea modifica LA MISMA fila (no suma otra,
/// no infla el total), solo en Borrador, solo si la fuente es Manual y nunca bajo el piso.
/// </summary>
public class clsEditarLineaCotizacionCommandTests
{
    private const decimal Piso = 2_000_000m;

    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsEditarLineaCotizacionCommandHandler Handler() =>
        new(_cotizaciones.Object, _propuestas.Object, _auditoria.Object, _usuario.Object, _uow.Object);

    private (clsCotizacion C, Guid LineaSolicitudId, clsCotizacionLinea Fila) Preparar(
        FuenteFlete fuente = FuenteFlete.Manual, EstadoCotizacion estado = EstadoCotizacion.Borrador, decimal? piso = Piso)
    {
        var solicitudId = Guid.NewGuid();
        var lineaSolicitudId = Guid.NewGuid();
        var c = new clsCotizacion { Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado };
        var fila = new clsCotizacionLinea
        {
            Id = Guid.NewGuid(), CotizacionId = c.Id, LineaSolicitudId = lineaSolicitudId, ValorFlete = 3_000_000m, Fuente = fuente,
            ValorRetornoContenedor = 400_000m,
        };
        c.Lineas.Add(fila);
        c.Lineas.Add(new clsCotizacionLinea { Id = Guid.NewGuid(), CotizacionId = c.Id, LineaSolicitudId = Guid.NewGuid(), ValorFlete = 500_000m, Fuente = FuenteFlete.Sicetac });
        c.Total = 3_900_000m;
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(lineaSolicitudId, "A → B", "C2", 1000,
                    piso is null ? null : new clsCandidatoTarifaDto("sicetac", piso.Value, "", "", 1),
                    null, null, null, null, ""),
            ], []));
        return (c, lineaSolicitudId, fila);
    }

    [Fact]
    public async Task Edita_la_misma_linea_sin_duplicar_y_recalcula_el_total()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        var dto = await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        Assert.Equal(2, dto.Lineas.Count);                      // sigue habiendo 2, no 3
        Assert.Equal(3_500_000m, fila.ValorFlete);
        Assert.Equal(FuenteFlete.Manual, fila.Fuente);          // la fuente no cambia
        Assert.Equal(3_500_000m + 400_000m + 500_000m, dto.Total); // 3.5M + retorno + la otra línea
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_valor_de_retorno_no_toca_el_que_tenia_y_con_valor_lo_reemplaza()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_100_000m, null));
        Assert.Equal(400_000m, fila.ValorRetornoContenedor);

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_100_000m, 250_000m));
        Assert.Equal(250_000m, fila.ValorRetornoContenedor);
    }

    [Fact]
    public async Task Cotizacion_entregada_da_COTIZACION_NO_EDITABLE()
    {
        var (c, lineaSolicitudId, _) = Preparar(estado: EstadoCotizacion.Entregada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null)));

        Assert.Equal("COTIZACION_NO_EDITABLE", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Linea_inexistente_en_la_cotizacion_da_LINEA_NO_ENCONTRADA()
    {
        var (c, _, _) = Preparar();

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, Guid.NewGuid(), 3_500_000m, null)));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Cotizacion_inexistente_da_COTIZACION_NO_ENCONTRADA()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(Guid.NewGuid(), Guid.NewGuid(), 1m, null)));

        Assert.Equal("COTIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Theory]
    [InlineData(FuenteFlete.Sicetac)]
    [InlineData(FuenteFlete.Historica)]
    [InlineData(FuenteFlete.Tarifario)]
    public async Task Un_flete_que_no_es_Manual_da_FLETE_NO_EDITABLE_y_no_cambia(FuenteFlete fuente)
    {
        var (c, lineaSolicitudId, fila) = Preparar(fuente);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null)));

        Assert.Equal("FLETE_NO_EDITABLE", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
    }

    [Fact]
    public async Task Un_valor_bajo_el_piso_da_FLETE_BAJO_PISO_SICETAC_y_no_cambia()
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, Piso - 1, null)));

        Assert.Equal("FLETE_BAJO_PISO_SICETAC", ex.Code);
        Assert.Equal(3_000_000m, fila.ValorFlete);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2_000_000)]   // igual al piso
    [InlineData(2_000_001)]
    public async Task Un_valor_igual_o_mayor_al_piso_se_guarda(int valor)
    {
        var (c, lineaSolicitudId, fila) = Preparar();

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, valor, null));

        Assert.Equal(valor, fila.ValorFlete);
    }

    [Fact]
    public async Task Sin_piso_para_la_linea_se_guarda_el_valor()
    {
        var (c, lineaSolicitudId, fila) = Preparar(piso: null);

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 100_000m, null));

        Assert.Equal(100_000m, fila.ValorFlete);
    }

    [Fact]
    public async Task Audita_el_valor_anterior_y_el_nuevo()
    {
        var (c, lineaSolicitudId, _) = Preparar();

        await Handler().Handle(new clsEditarLineaCotizacionCommand(c.Id, lineaSolicitudId, 3_500_000m, null));

        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid>(), "cotizaciones", c.Id, "editar_flete",
            It.Is<string>(d => d.Contains("\"valorAnterior\":3000000") && d.Contains("\"valorNuevo\":3500000"))), Times.Once);
    }
}
