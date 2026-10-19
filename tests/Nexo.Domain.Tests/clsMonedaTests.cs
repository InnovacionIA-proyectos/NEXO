using System.Globalization;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

/// <summary>
/// S6-HU-29.2/#312: las justificaciones y avisos de piso mostraban "¤3,500,000" (el contenedor corre con globalización invariante y
/// <c>{valor:C0}</c> usa la cultura ambiente). Ahora "$3.500.000", sin depender de ninguna cultura.
/// </summary>
public class clsMonedaTests
{
    [Theory]
    [InlineData(3_500_000, "$3.500.000")]
    [InlineData(950, "$950")]
    [InlineData(0, "$0")]
    [InlineData(1_000, "$1.000")]
    [InlineData(48_982_983, "$48.982.983")]
    public void Formatea_pesos_con_signo_y_punto_de_miles(long valor, string esperado)
    {
        Assert.Equal(esperado, clsMoneda.Pesos(valor));
    }

    [Fact]
    public void Redondea_a_pesos_enteros()
    {
        Assert.Equal("$1.235", clsMoneda.Pesos(1234.5m));
    }

    [Fact]
    public void Un_negativo_lleva_el_signo_delante_del_peso()
    {
        Assert.Equal("-$2.000", clsMoneda.Pesos(-2000m));
    }

    // El formato no depende de la cultura del proceso: se prueba con una cultura que usa otros separadores.
    [Fact]
    public void No_depende_de_la_cultura_ambiente()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("$3.500.000", clsMoneda.Pesos(3_500_000m));
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("$3.500.000", clsMoneda.Pesos(3_500_000m));
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    // Las justificaciones del costeo ya no llevan el símbolo "¤" y usan el formato colombiano.
    [Fact]
    public void Las_justificaciones_del_costeo_muestran_pesos_y_no_el_simbolo_de_moneda_generico()
    {
        var plaza = new clsCandidatoDeTarifa("plaza", 3_500_000m);
        var sicetac = new clsCandidatoDeTarifa("sicetac", 3_000_000m);

        var r = clsReglaDeCosteo.Decidir(sicetac, plaza, historico: null);

        Assert.DoesNotContain("¤", r.Justificacion);
        Assert.Contains("$3.500.000", r.Justificacion);
        Assert.Contains("$3.000.000", r.Justificacion);
    }

    [Fact]
    public void El_texto_de_Solo_SICETAC_empieza_con_La_ruta_y_no_con_el_nombre_de_la_clase()
    {
        var sicetac = new clsCandidatoDeTarifa("sicetac", 3_000_000m);

        var d = clsReglaDeCosteo.DecidirFlujo(hayReglaSoloSicetac: true, sicetac, plazaVigente: null, plazaVencida: null, historico: null);

        Assert.StartsWith("La ruta", d.Resultado.Justificacion);
        Assert.DoesNotContain("clsRuta", d.Resultado.Justificacion);
        Assert.Contains("$3.000.000", d.Resultado.Justificacion);
    }
}
