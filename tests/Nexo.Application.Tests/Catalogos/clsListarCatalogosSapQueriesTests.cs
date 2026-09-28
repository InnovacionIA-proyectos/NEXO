using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Moq;

namespace Nexo.Application.Tests.Catalogos;

public class clsListarCatalogosSapQueriesTests
{
    [Fact]
    public async Task Lineas_de_negocio_devuelve_el_catalogo_tal_como_lo_entrega_el_port()
    {
        var port = new Mock<ILineasNegocioPort>();
        port.Setup(p => p.ListarAsync(default)).ReturnsAsync([new clsLineaNegocioDto(1, "Oferta de Expedición Nacional")]);
        var handler = new clsListarLineasNegocioQueryHandler(port.Object);

        var result = await handler.Handle(new clsListarLineasNegocioQuery());

        Assert.Single(result);
        Assert.Equal("Oferta de Expedición Nacional", result[0].Descripcion);
    }

    [Fact]
    public async Task Clases_de_viaje_devuelve_el_catalogo_tal_como_lo_entrega_el_port()
    {
        var port = new Mock<IClasesViajePort>();
        port.Setup(p => p.ListarAsync(default)).ReturnsAsync([new clsClaseViajeDto(2, "Contenedor")]);
        var handler = new clsListarClasesViajeQueryHandler(port.Object);

        var result = await handler.Handle(new clsListarClasesViajeQuery());

        Assert.Single(result);
        Assert.Equal("Contenedor", result[0].Descripcion);
    }

    [Fact]
    public async Task Clases_de_contenedor_incluye_tamano_y_tara()
    {
        var port = new Mock<IClasesContenedorPort>();
        port.Setup(p => p.ListarAsync(default)).ReturnsAsync([new clsClaseContenedorDto(3, "CNT40.HC", 40, 3800, 4200)]);
        var handler = new clsListarClasesContenedorQueryHandler(port.Object);

        var result = await handler.Handle(new clsListarClasesContenedorQuery());

        Assert.Single(result);
        Assert.Equal(40, result[0].TamanoContenedor);
        Assert.Equal(3800, result[0].PesoInfCnt);
        Assert.Equal(4200, result[0].PesoSupCnt);
    }
}
