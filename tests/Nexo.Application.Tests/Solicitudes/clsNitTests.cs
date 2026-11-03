using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.Tests.Solicitudes;

public class clsNitTests
{
    [Theory]
    [InlineData("900123456", "900123456")]
    [InlineData("900.123.456-7", "900123456")]
    [InlineData(" 900 123 456 ", "900123456")]
    [InlineData("900123456-7", "900123456")]
    [InlineData("900.123.456", "900123456")]
    public void Los_formatos_del_mismo_NIT_dan_la_misma_llave(string entrada, string esperado) =>
        Assert.Equal(esperado, clsNit.Normalizar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("-7")]
    [InlineData(". .")]
    public void Un_NIT_sin_nada_que_comparar_es_null(string? entrada) => Assert.Null(clsNit.Normalizar(entrada));
}
