using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// S5-HU-18.4/#276: ResolverVehiculo ya no crea filas "oficiales" en silencio por
/// coincidencia de texto — toda fila nueva nace en cuarentena (EsCatalogoOficial =
/// false), hasta que alguien la mapee a un código canónico del Cubicador o la descarte
/// (bandeja de normalización, S5-HU-19.3, fuera de este repo).
/// </summary>
public class clsResolucionDeCatalogosTests
{
    [Fact]
    public void Tipo_no_reconocido_crea_una_fila_en_cuarentena()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>();

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo("Tractomula", "Furgon", porClave, catalogo.Object);

        Assert.True(creado);
        Assert.False(vehiculo!.EsCatalogoOficial);
        catalogo.Verify(c => c.Agregar(It.Is<clsConfiguracionVehiculo>(v => !v.EsCatalogoOficial)), Times.Once);
    }

    [Fact]
    public void Tipo_que_calza_textualmente_con_un_codigo_canonico_tambien_nace_en_cuarentena()
    {
        // La comparación es de texto normalizado, no de significado: no hay forma de saber
        // aquí si "3S3" realmente corresponde al código canónico del Cubicador sin
        // consultarlo. Oficializar solo pasa por la bandeja de normalización.
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>();

        var (vehiculo, _) = clsResolucionDeCatalogos.ResolverVehiculo("3S3", null, porClave, catalogo.Object);

        Assert.False(vehiculo!.EsCatalogoOficial);
    }

    [Fact]
    public void Tipo_ya_existente_se_reutiliza_sin_tocar_su_EsCatalogoOficial()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var existente = new clsConfiguracionVehiculo { TipoVehiculo = "C3", Carroceria = "Estacas", EsCatalogoOficial = true };
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>
        {
            [clsResolucionDeCatalogos.ClaveVehiculo("C3", "Estacas")] = existente,
        };

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo("C3", "Estacas", porClave, catalogo.Object);

        Assert.False(creado);
        Assert.Same(existente, vehiculo);
        Assert.True(vehiculo!.EsCatalogoOficial);
        catalogo.Verify(c => c.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Never);
    }

    [Fact]
    public void Sin_tipo_de_vehiculo_no_resuelve_ni_crea_nada()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>();

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo(null, "Furgon", porClave, catalogo.Object);

        Assert.Null(vehiculo);
        Assert.False(creado);
        catalogo.Verify(c => c.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Never);
    }
}
