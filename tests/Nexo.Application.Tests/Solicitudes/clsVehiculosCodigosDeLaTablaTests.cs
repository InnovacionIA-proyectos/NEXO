using Moq;
using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// Los códigos de la tabla de traducción (2L3, 3S2…) son oficiales: una configuración nueva con ellos nace oficial, o se promueve, siempre que
/// la carrocería esté vacía o sea del catálogo; con otra carrocería o con texto libre sigue naciendo en cuarentena.
/// </summary>
public class clsVehiculosCodigosDeLaTablaTests
{
    private readonly Mock<IConfiguracionesVehiculoPort> _catalogo = new();
    private readonly Mock<IVehiculosNexoPort> _nexo = new();
    private readonly Dictionary<string, clsConfiguracionVehiculo> _porClave = [];

    public clsVehiculosCodigosDeLaTablaTests()
    {
        _nexo.Setup(n => n.ListarCodigosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new clsCodigoVehiculoNexo("2L3", "Liviano 2L3"), new clsCodigoVehiculoNexo("3S2", "Tractocamión 3S2"), new clsCodigoVehiculoNexo("2S2", "Minimula")]);
        _nexo.Setup(n => n.ListarCarroceriasOficialesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["PORTACONTENEDORES", "Portacontenedores", "FURGON", "Furgón"]);
    }

    private async Task<(clsConfiguracionVehiculo? V, bool Creado)> Resolver(string? tipo, string? carroceria)
    {
        var oficialidad = await clsOficialidadDeVehiculos.CargarAsync(_nexo.Object, default);
        return clsResolucionDeCatalogos.ResolverVehiculo(tipo, carroceria, _porClave, _catalogo.Object, oficialidad: oficialidad);
    }

    [Theory]
    [InlineData("2L3", null)]
    [InlineData("2l3", "")]
    [InlineData("3S2", "Furgón")]
    [InlineData(" 3s2 ", "FURGON")]
    [InlineData("2S2", "PORTACONTENEDORES")]
    public async Task Un_codigo_de_la_tabla_con_carroceria_vacia_o_del_catalogo_nace_oficial(string tipo, string? carroceria)
    {
        var (v, creado) = await Resolver(tipo, carroceria);

        Assert.True(creado);
        Assert.True(v!.EsCatalogoOficial);
        Assert.Equal(tipo.Trim().ToUpperInvariant(), v.TipoVehiculo);
        _catalogo.Verify(c => c.Agregar(v), Times.Once);
    }

    [Fact]
    public async Task Un_codigo_de_la_tabla_con_una_carroceria_que_no_es_del_catalogo_nace_en_cuarentena()
    {
        var (v, creado) = await Resolver("2L3", "Carroceria inventada");

        Assert.True(creado);
        Assert.False(v!.EsCatalogoOficial);
        Assert.Equal("2L3", v.TipoVehiculo);
    }

    [Fact]
    public async Task El_texto_libre_nace_en_cuarentena_como_siempre()
    {
        var (v, _) = await Resolver("Camion grande", null);

        Assert.False(v!.EsCatalogoOficial);
    }

    [Fact]
    public async Task Una_configuracion_en_cuarentena_con_un_codigo_de_la_tabla_se_promueve_pero_no_si_fue_descartada()
    {
        var enCuarentena = new clsConfiguracionVehiculo { TipoVehiculo = "2L3", Carroceria = "FURGON" };
        var descartada = new clsConfiguracionVehiculo { TipoVehiculo = "3S2", Carroceria = "FURGON", Descartada = true };
        _porClave[clsResolucionDeCatalogos.ClaveVehiculo("2L3", "FURGON")] = enCuarentena;
        _porClave[clsResolucionDeCatalogos.ClaveVehiculo("3S2", "FURGON")] = descartada;

        var (promovida, creada) = await Resolver("2L3", "FURGON");
        var (sigue, _) = await Resolver("3S2", "FURGON");

        Assert.False(creada);
        Assert.Same(enCuarentena, promovida);
        Assert.True(enCuarentena.EsCatalogoOficial);
        Assert.False(sigue!.EsCatalogoOficial);
    }

    [Fact]
    public async Task Sin_la_tabla_disponible_no_cambia_nada()
    {
        _nexo.Setup(n => n.ListarCodigosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var (v, _) = await Resolver("2L3", null);

        Assert.False(v!.EsCatalogoOficial);
    }
}
