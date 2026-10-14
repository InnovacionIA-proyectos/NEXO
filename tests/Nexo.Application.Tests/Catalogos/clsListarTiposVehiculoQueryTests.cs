using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Moq;

namespace Nexo.Application.Tests.Catalogos;

public class clsListarTiposVehiculoQueryTests
{
    [Fact]
    public async Task Devuelve_el_catalogo_tal_como_lo_entrega_el_port()
    {
        var port = new Mock<ITiposVehiculoPort>();
        port.Setup(p => p.ListarAsync(default)).ReturnsAsync(
        [
            new clsTipoVehiculoDto("3S3", "Tractomula", true),
            new clsTipoVehiculoDto("2S3", "Camión doble troque", true),
        ]);
        var handler = new clsListarTiposVehiculoQueryHandler(port.Object);

        var result = await handler.Handle(new clsListarTiposVehiculoQuery());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, v => v.Codigo == "3S3" && v.EsCatalogoOficial);
        Assert.Contains(result, v => v.Codigo == "2S3" && v.EsCatalogoOficial);
    }
}
