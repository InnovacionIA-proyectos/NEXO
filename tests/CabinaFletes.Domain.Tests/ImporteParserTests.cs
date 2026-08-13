using CabinaFletes.Domain.Common;

namespace CabinaFletes.Domain.Tests;

/// <summary>
/// Cubre el defecto LOG-05: el parseo con InvariantCulture inflaba "1500000,50" ×100
/// y descartaba en silencio "1.500.000". El parser debe interpretar formato colombiano
/// e invariante sin ambigüedad.
/// </summary>
public class ImporteParserTests
{
    [Theory]
    [InlineData("1500000,50", 1500000.50)]
    [InlineData("1.500.000", 1500000)]
    [InlineData("1.500.000,50", 1500000.50)]
    [InlineData("2.600.000", 2600000)]
    [InlineData("1.250", 1250)]
    [InlineData("1500000.50", 1500000.50)]
    [InlineData("1,500,000", 1500000)]
    [InlineData("1,500,000.50", 1500000.50)]
    [InlineData("1500000.00", 1500000)]
    [InlineData("2600000", 2600000)]
    [InlineData("$ 2.600.000", 2600000)]
    [InlineData("  995457  ", 995457)]
    [InlineData("12,5", 12.5)]
    [InlineData("999.99", 999.99)]
    public void Parsea_formatos_heterogeneos(string entrada, double esperado)
    {
        var v = ImporteParser.Parse(entrada);
        Assert.NotNull(v);
        Assert.Equal((decimal)esperado, v!.Value);
    }

    [Fact]
    public void No_infla_x100_el_decimal_colombiano()
    {
        Assert.Equal(1500000.50m, ImporteParser.Parse("1500000,50"));
    }

    [Fact]
    public void No_descarta_el_formato_con_puntos_de_miles()
    {
        Assert.Equal(1500000m, ImporteParser.Parse("1.500.000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("N/A")]
    [InlineData("-")]
    public void Devuelve_null_para_entradas_no_numericas(string? entrada)
    {
        Assert.Null(ImporteParser.Parse(entrada));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5000")]
    public void ParsePositivo_rechaza_cero_y_negativos(string entrada)
    {
        Assert.Null(ImporteParser.ParsePositivo(entrada));
    }
}
