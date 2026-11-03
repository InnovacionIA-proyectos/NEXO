using Nexo.Infrastructure.Biblioteca.Adaptadores;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// Elección de la fila SICE-TAC de un viaje vacío ("Sin carga"). Cada ruta trae una fila por unidad de transporte con valores
/// distintos y varias rutas alternas (vía estándar o no): se elige la de la unidad que corresponde a la carrocería de la línea,
/// nunca la más barata. Es una prueba de lógica pura con filas armadas a mano, sin base de datos: los valores no cambian con la
/// tarifa vigente de cada mes.
/// </summary>
public class clsSeleccionSiceVacioTests
{
    private static clsRutaSicetacDw Fila(string unidad, decimal valor, string via = "SI", decimal horas = 8.58m, decimal? valorHora = 0m) => new()
    {
        NombreRuta = "BOGOTA BOGOTA D. C. - MEDELLIN ANTIOQUIA", Configuracion = "2S2", NombreTipoCarga = "Sin carga",
        NombreUnidadTransporte = unidad, Valor = valor, ValorHora = valorHora, ViaEstandar = via, HorasRecorrido = horas,
        CondicionCarga = "VACIO",
    };

    /// <summary>Filas con la forma de una ruta real de tramo vacío: una por unidad de transporte, vía estándar y alterna.</summary>
    private static List<clsRutaSicetacDw> RutaConUnidades() =>
    [
        Fila("PORTACONTENEDORES", 1_974_623m), Fila("ESTACAS", 1_983_574m), Fila("PLATAFORMA", 1_983_574m),
        Fila("FURGON", 2_110_711m), Fila("FURGON REFRIGERADO", 2_266_768m), Fila("VOLCO", 2_028_084m),
        Fila("PORTACONTENEDORES", 2_568_414m, via: "NO", horas: 11.62m), Fila("FURGON", 2_752_600m, via: "NO", horas: 11.62m),
    ];

    [Fact]
    public void Elige_la_fila_de_la_unidad_de_la_carroceria_y_no_la_mas_barata()
    {
        var fila = clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), "Furgón", esContenedor: false);

        Assert.Equal("FURGON", fila!.NombreUnidadTransporte);
        Assert.Equal(2_110_711m, fila.Valor);
    }

    [Theory]
    [InlineData("Furgon", "FURGON")]
    [InlineData("FURGÓN", "FURGON")]
    [InlineData("furgon_refrigerado", "FURGON REFRIGERADO")]
    [InlineData("Furgon-Refrigerado", "FURGON REFRIGERADO")]
    [InlineData("estacas", "ESTACAS")]
    public void La_carroceria_se_compara_sin_tildes_ni_mayusculas_y_con_guiones_como_espacio(string carroceria, string unidad)
    {
        var fila = clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), carroceria, esContenedor: false);

        Assert.Equal(unidad, fila!.NombreUnidadTransporte);
    }

    [Fact]
    public void Entre_las_filas_de_la_unidad_prefiere_la_via_estandar()
    {
        var fila = clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), "Furgón", esContenedor: false);

        Assert.Equal("SI", fila!.ViaEstandar);
        Assert.True(fila.Valor < 2_752_600m);
    }

    [Fact]
    public void Una_linea_de_contenedor_sin_carroceria_usa_la_unidad_portacontenedores()
    {
        var fila = clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), null, esContenedor: true);

        Assert.Equal("PORTACONTENEDORES", fila!.NombreUnidadTransporte);
        Assert.Equal(1_974_623m, fila.Valor);
    }

    [Fact]
    public void Una_linea_que_no_es_de_contenedor_sin_carroceria_no_elige_ninguna_fila()
    {
        Assert.Null(clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), "  ", esContenedor: false));
    }

    [Fact]
    public void Una_carroceria_que_no_existe_como_unidad_no_elige_ninguna_fila()
    {
        Assert.Null(clsPropuestaTarifaAdapter.SeleccionarSiceVacio(RutaConUnidades(), "Planchón", esContenedor: false));
    }

    [Theory]
    [InlineData("Furgón")]
    [InlineData("FURGON")]
    [InlineData("furgon")]
    public void El_piso_del_viaje_cargado_encuentra_la_unidad_aunque_la_carroceria_traiga_tildes(string carroceria)
    {
        var filas = new List<clsRutaSicetacDw>
        {
            Fila("ESTACAS", 4_510_558m, valorHora: 91_763m), Fila("FURGON", 4_687_597m, valorHora: 107_530m),
        };
        foreach (var f in filas) { f.NombreTipoCarga = "General"; f.CondicionCarga = "CARGADO"; }

        var fila = clsPropuestaTarifaAdapter.SeleccionarSice(filas, tipologia: null, carroceria);

        Assert.Equal("FURGON", fila!.NombreUnidadTransporte);
        Assert.Equal(4_687_597m, fila.Valor);
    }

    [Fact]
    public void Descarta_las_filas_sin_valor()
    {
        var filas = new List<clsRutaSicetacDw> { Fila("FURGON", 0m), Fila("FURGON", 2_110_711m, via: "NO") };

        var fila = clsPropuestaTarifaAdapter.SeleccionarSiceVacio(filas, "Furgón", esContenedor: false);

        Assert.Equal(2_110_711m, fila!.Valor);
    }
}
