using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>S4-HU-09.1: expone el catálogo completo (todas las columnas) para el
/// desplegable de Origen/Destino.</summary>
public class clsListarMunicipiosQueryTests
{
    [Fact]
    public async Task Devuelve_todas_las_columnas_de_cada_fila_del_catalogo()
    {
        var municipios = new Mock<IMunicipiosRepository>();
        municipios.Setup(m => m.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new clsMunicipio { Id = 1, CodigoDane = "05001000", Municipio = "MEDELLIN", Departamento = "ANTIOQUIA", Pais = "CO", NombreTarifasPlaza = "ZC_MEDELLIN", ZonaTarificacion = "ZTC_VABURRA" },
        ]);

        var resultado = await new clsListarMunicipiosQueryHandler(municipios.Object).Handle(new clsListarMunicipiosQuery());

        var fila = Assert.Single(resultado);
        Assert.Equal(1, fila.Id);
        Assert.Equal("05001000", fila.CodigoDane);
        Assert.Equal("MEDELLIN", fila.Municipio);
        Assert.Equal("ANTIOQUIA", fila.Departamento);
        Assert.Equal("CO", fila.Pais);
        Assert.Equal("ZC_MEDELLIN", fila.NombreTarifasPlaza);
        Assert.Equal("ZTC_VABURRA", fila.ZonaTarificacion);
    }
}
