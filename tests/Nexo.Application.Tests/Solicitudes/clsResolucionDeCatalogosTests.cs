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

    // ---------------------------------------------------------------- S6-HU-27.2/#321: diccionario de alias

    private static readonly Dictionary<string, string> AliasVehiculos = new()
    {
        ["TURBO"] = "2L2", ["TB"] = "2L2", ["T.B"] = "2L2", ["2L2"] = "2L2", ["TMULA"] = "3S3",
    };

    [Theory]
    [InlineData("TB")]
    [InlineData("T.B")]
    [InlineData("Turbo")]
    [InlineData("  turbo ")]
    [InlineData("2L2")]
    public void Las_variantes_de_un_alias_resuelven_a_la_misma_configuracion(string texto)
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>();

        var (primera, _) = clsResolucionDeCatalogos.ResolverVehiculo("Turbo", "Furgon", porClave, catalogo.Object, AliasVehiculos);
        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo(texto, "Furgon", porClave, catalogo.Object, AliasVehiculos);

        Assert.Same(primera, vehiculo);
        Assert.False(creado);
        Assert.Equal("2L2", vehiculo!.TipoVehiculo);                 // se guarda el CÓDIGO, no el texto que llegó
        catalogo.Verify(c => c.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Once);   // solo la primera crea la fila
    }

    [Fact]
    public void Un_alias_crea_la_fila_con_el_codigo_como_oficial_y_la_carroceria_intacta()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo(
            "Tmula", "Furgon  Refrigerado", new Dictionary<string, clsConfiguracionVehiculo>(), catalogo.Object, AliasVehiculos);

        Assert.True(creado);
        Assert.Equal("3S3", vehiculo!.TipoVehiculo);
        Assert.Equal("Furgon Refrigerado", vehiculo.Carroceria);     // la carrocería no pasa por el diccionario
        Assert.True(vehiculo.EsCatalogoOficial);
    }

    [Fact]
    public void Un_alias_reutiliza_una_fila_oficial_ya_existente_de_ese_codigo_y_carroceria()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var existente = new clsConfiguracionVehiculo { TipoVehiculo = "2L2", Carroceria = "Estacas", EsCatalogoOficial = true };
        var porClave = new Dictionary<string, clsConfiguracionVehiculo> { [clsResolucionDeCatalogos.ClaveVehiculo("2L2", "Estacas")] = existente };

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo("TB", "Estacas", porClave, catalogo.Object, AliasVehiculos);

        Assert.False(creado);
        Assert.Same(existente, vehiculo);
        catalogo.Verify(c => c.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Never);
    }

    [Fact]
    public void Un_alias_promueve_a_oficial_una_fila_que_estaba_en_cuarentena_pero_no_una_descartada()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();
        var enCuarentena = new clsConfiguracionVehiculo { TipoVehiculo = "2L2", Carroceria = null };
        var descartada = new clsConfiguracionVehiculo { TipoVehiculo = "2L2", Carroceria = "Basura", Descartada = true };
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>
        {
            [clsResolucionDeCatalogos.ClaveVehiculo("2L2", null)] = enCuarentena,
            [clsResolucionDeCatalogos.ClaveVehiculo("2L2", "Basura")] = descartada,
        };

        clsResolucionDeCatalogos.ResolverVehiculo("TB", null, porClave, catalogo.Object, AliasVehiculos);
        clsResolucionDeCatalogos.ResolverVehiculo("TB", "Basura", porClave, catalogo.Object, AliasVehiculos);

        Assert.True(enCuarentena.EsCatalogoOficial);
        Assert.False(descartada.EsCatalogoOficial);
    }

    [Fact]
    public void Un_texto_que_el_diccionario_no_conoce_sigue_yendo_a_cuarentena()
    {
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo(
            "Camioneta rara", null, new Dictionary<string, clsConfiguracionVehiculo>(), catalogo.Object, AliasVehiculos);

        Assert.True(creado);
        Assert.False(vehiculo!.EsCatalogoOficial);
        Assert.Equal("Camioneta rara", vehiculo.TipoVehiculo);
    }

    private static readonly Dictionary<string, string> AliasContenedores = new()
    {
        ["40 HC"] = "CNT40.HC", ["40HC"] = "CNT40.HC", ["20 STD"] = "CNT20.ST", ["20 PIES"] = "CNT20.ST",
    };

    [Theory]
    [InlineData("40 HC", "CNT40.HC")]
    [InlineData("40hc", "CNT40.HC")]
    [InlineData("20 STD", "CNT20.ST")]
    [InlineData("20  pies", "CNT20.ST")]
    public void La_clase_de_contenedor_se_resuelve_por_el_diccionario(string texto, string esperado) =>
        Assert.Equal(esperado, clsResolucionDeCatalogos.ResolverClaseContenedor(texto, AliasContenedores));

    [Theory]
    [InlineData("CNT40.HC", "CNT40.HC")]        // un código real de SAP dentro del texto, como antes
    [InlineData("cnt 20 st", "CNT20.ST")]
    [InlineData("Granel", "Granel")]            // desconocido: el texto tal cual, como antes
    public void Sin_alias_la_clase_de_contenedor_se_comporta_como_siempre(string texto, string esperado) =>
        Assert.Equal(esperado, clsResolucionDeCatalogos.ResolverClaseContenedor(texto, AliasContenedores));

    [Fact]
    public void La_clase_de_contenedor_vacia_es_null_y_el_texto_largo_se_recorta_a_20()
    {
        Assert.Null(clsResolucionDeCatalogos.ResolverClaseContenedor("  ", AliasContenedores));
        Assert.Equal(20, clsResolucionDeCatalogos.ResolverClaseContenedor(new string('x', 40), null)!.Length);
    }
}
