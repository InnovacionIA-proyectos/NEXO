using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// S4-HU-09.1: la siembra de municipios es de una sola vez — el handler solo debe
/// orquestar guards + guardado; la transformación real vive en el adapter de
/// Infrastructure (verificada contra datos reales de dbSolucionesTI, no aquí).
/// </summary>
public class clsSembrarMunicipiosCommandTests
{
    private readonly Mock<IMunicipiosSiembraPort> _fuente = new();
    private readonly Mock<IMunicipiosRepository> _municipios = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsSembrarMunicipiosCommandHandler Handler() => new(_fuente.Object, _municipios.Object, _uow.Object);

    [Fact]
    public async Task Sin_DbSolucionesTi_configurada_lanza_ServiceUnavailable()
    {
        _fuente.SetupGet(f => f.Configurado).Returns(false);

        await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Handler().Handle(new clsSembrarMunicipiosCommand()));

        _municipios.Verify(m => m.ExisteAlgunoAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_ya_hay_municipios_sembrados_lanza_Conflict_y_no_vuelve_a_construir_el_catalogo()
    {
        _fuente.SetupGet(f => f.Configurado).Returns(true);
        _municipios.Setup(m => m.ExisteAlgunoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsSembrarMunicipiosCommand()));

        _fuente.Verify(f => f.ConstruirCatalogoAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Camino_feliz_guarda_el_catalogo_y_resume_los_conteos_correctamente()
    {
        _fuente.SetupGet(f => f.Configurado).Returns(true);
        _municipios.Setup(m => m.ExisteAlgunoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var catalogo = new List<clsMunicipio>
        {
            new() { CodigoDane = "05001000", Municipio = "MEDELLIN", Departamento = "ANTIOQUIA", Pais = "CO", NombreTarifasPlaza = "ZC_MEDELLIN", ZonaTarificacion = "ZTC_VABURRA" },
            new() { CodigoDane = "76001000", Municipio = "CALI", Departamento = "VALLE DEL CAUCA", Pais = "CO", NombreTarifasPlaza = "ZC_CALI", ZonaTarificacion = null },
            new() { CodigoDane = "99999000", Municipio = "SIN COSTEO", Departamento = "X", Pais = "CO", NombreTarifasPlaza = null, ZonaTarificacion = null },
            new() { CodigoDane = "010100", Municipio = "CARACAS", Departamento = "DISTRITO FEDERAL", Pais = "VE", NombreTarifasPlaza = null, ZonaTarificacion = null },
        };
        _fuente.Setup(f => f.ConstruirCatalogoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalogo);

        var resultado = await Handler().Handle(new clsSembrarMunicipiosCommand());

        Assert.Equal(4, resultado.TotalInsertados);
        Assert.Equal(3, resultado.Colombia);
        Assert.Equal(1, resultado.Venezuela);
        Assert.Equal(0, resultado.Ecuador);
        Assert.Equal(2, resultado.ConNombreTarifasPlaza);
        Assert.Equal(1, resultado.ConZonaTarificacion);
        Assert.Equal(1, resultado.ZcSinRelacionOficial); // Cali: tiene ZC pero no ZTC
        _municipios.Verify(m => m.AgregarRango(catalogo), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
