using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// S5-HU-20.5/#297 (parte 3): la clave de comparación no distingue tildes ni los separadores "_" y "-",
/// para que "Bogotá"/"Bogota" o "Furgon Refrigerado"/"FURGON_REFRIGERADO" no creen filas distintas.
/// Lo que se GUARDA sigue siendo el texto limpio, tal como llegó.
/// </summary>
public class clsNormalizarCatalogosTests
{
    [Theory]
    [InlineData("Bogotá", "BOGOTA")]
    [InlineData("  bogota  ", "BOGOTA")]
    [InlineData("Medellín", "MEDELLIN")]
    [InlineData("Furgon Refrigerado", "FURGON REFRIGERADO")]
    [InlineData("FURGON_REFRIGERADO", "FURGON REFRIGERADO")]
    [InlineData("furgon-refrigerado", "FURGON REFRIGERADO")]
    [InlineData("Furgón  _ Refrigerado", "FURGON REFRIGERADO")]
    [InlineData("3S3", "3S3")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalizar_quita_tildes_y_unifica_separadores(string? entrada, string esperado) =>
        Assert.Equal(esperado, clsResolucionDeCatalogos.Normalizar(entrada));

    [Fact]
    public void Las_claves_de_ruta_con_y_sin_tilde_son_la_misma()
    {
        Assert.Equal(
            clsResolucionDeCatalogos.ClaveRuta("Bogotá", "Medellín"),
            clsResolucionDeCatalogos.ClaveRuta("BOGOTA", "medellin"));
    }

    [Fact]
    public void Una_ruta_con_tilde_resuelve_a_la_fila_que_ya_existe_sin_tilde_y_no_crea_otra()
    {
        var existente = new clsRuta { Origen = "BOGOTA", Destino = "MEDELLIN" };
        var porClave = new Dictionary<string, clsRuta> { [clsResolucionDeCatalogos.ClaveRuta(existente.Origen, existente.Destino)] = existente };
        var catalogo = new Mock<IRutasCatalogoPort>();

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta("Bogotá", "Medellín", porClave, catalogo.Object);

        Assert.False(creada);
        Assert.Same(existente, ruta);
        catalogo.Verify(c => c.Agregar(It.IsAny<clsRuta>()), Times.Never);
    }

    [Fact]
    public void Un_vehiculo_con_otro_separador_resuelve_a_la_fila_que_ya_existe()
    {
        var existente = new clsConfiguracionVehiculo { TipoVehiculo = "C3", Carroceria = "Furgon Refrigerado" };
        var porClave = new Dictionary<string, clsConfiguracionVehiculo>
        {
            [clsResolucionDeCatalogos.ClaveVehiculo("C3", "Furgon Refrigerado")] = existente,
        };
        var catalogo = new Mock<IConfiguracionesVehiculoPort>();

        var (vehiculo, creado) = clsResolucionDeCatalogos.ResolverVehiculo("c3", "FURGON_REFRIGERADO", porClave, catalogo.Object);

        Assert.False(creado);
        Assert.Same(existente, vehiculo);
        catalogo.Verify(c => c.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Never);
    }

    [Fact]
    public void Lo_que_se_guarda_en_una_fila_nueva_es_el_texto_limpio_no_la_clave()
    {
        var porClave = new Dictionary<string, clsRuta>();
        var catalogo = new Mock<IRutasCatalogoPort>();

        var (ruta, creada) = clsResolucionDeCatalogos.ResolverRuta("  Bogotá  ", "Medellín", porClave, catalogo.Object);

        Assert.True(creada);
        Assert.Equal("Bogotá", ruta.Origen);      // con tilde: se conserva como lo escribió el usuario
        Assert.Equal("Medellín", ruta.Destino);
    }

    [Fact]
    public void Dos_filas_viejas_que_ahora_colapsan_en_una_clave_no_rompen_la_carga_gana_la_primera()
    {
        var a = new clsRuta { Origen = "BOGOTÁ", Destino = "CALI" };
        var b = new clsRuta { Origen = "BOGOTA", Destino = "CALI" };

        var porClave = new[] { a, b }
            .GroupBy(r => clsResolucionDeCatalogos.ClaveRuta(r.Origen, r.Destino))
            .ToDictionary(g => g.Key, g => g.First());   // exactamente lo que hacen los comandos al cargar

        Assert.Single(porClave);
        Assert.Same(a, porClave[clsResolucionDeCatalogos.ClaveRuta("Bogota", "Cali")]);
    }
}
