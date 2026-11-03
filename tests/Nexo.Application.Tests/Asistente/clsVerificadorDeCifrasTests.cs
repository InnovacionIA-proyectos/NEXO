using Nexo.Application.CasosDeUso.Asistente.Conversacion;

namespace Nexo.Application.Tests.Asistente;

/// <summary>La compuerta contra precios inventados: una cifra de dinero sin origen en las fuentes se detecta; lo demás pasa.</summary>
public class clsVerificadorDeCifrasTests
{
    private const string Herramienta = """{"encontrado":true,"valor":{"valorPiso":2900000,"periodo":202610,"horas":8},"fuente":"SICE-TAC (DWSice), periodo 202610"}""";

    private static IReadOnlyList<string> Sin(string respuesta, params string[] fuentes) => clsVerificadorDeCifras.CifrasSinOrigen(respuesta, fuentes);

    [Theory]
    [InlineData("El piso SICE-TAC es $2.900.000.")]
    [InlineData("El piso SICE-TAC es 2.900.000 COP.")]
    [InlineData("El piso es 2,900,000 pesos.")]
    [InlineData("La tarifa: 2900000.")]
    [InlineData("Piso de 2,9 millones.")]
    [InlineData("Valor de 2.900 mil.")]
    public void Una_cifra_que_esta_en_la_herramienta_pasa_con_cualquier_formato(string respuesta)
    {
        Assert.Empty(Sin(respuesta, Herramienta));
    }

    [Theory]
    [InlineData("El piso SICE-TAC es $3.100.000.", "$3.100.000")]
    [InlineData("La tarifa sería 3.100.000 COP.", "3.100.000")]
    [InlineData("Te cobraría 3100000 de flete.", "3100000")]
    [InlineData("Sale en 3,1 millones.", "3,1 millones")]
    public void Una_cifra_de_dinero_que_no_esta_en_ninguna_fuente_se_detecta(string respuesta, string cifra)
    {
        Assert.Equal([cifra], Sin(respuesta, Herramienta));
    }

    [Fact]
    public void Un_total_calculado_por_el_modelo_se_detecta_aunque_sus_sumandos_vengan_de_herramientas()
    {
        var fuente = """{"a":{"valor":2900000},"b":{"valor":450000}}""";

        Assert.Equal(["$3.350.000"], Sin("Flete más escolta: $3.350.000.", fuente));
        Assert.Empty(Sin("Flete $2.900.000 y escolta $450.000.", fuente));
    }

    [Fact]
    public void Una_cifra_aproximada_con_unidad_se_acepta_si_esta_a_menos_de_un_uno_por_ciento()
    {
        var fuente = """{"valor":3512300}""";

        Assert.Empty(Sin("Alrededor de 3,5 millones.", fuente));
        Assert.Equal(["3,8 millones"], Sin("Alrededor de 3,8 millones.", fuente));
    }

    [Fact]
    public void Lo_que_escribio_la_persona_cuenta_como_origen()
    {
        Assert.Empty(Sin("Entonces el valor de $3.000.000 que me diste está por encima del piso.", "Quiero cotizar con $3.000.000"));
    }

    [Theory]
    [InlineData("La carga pesa 30.000 kg.")]
    [InlineData("Son 1.250 km de ruta.")]
    [InlineData("El periodo 202611 aún no está cargado.")]
    [InlineData("Se actualizó en 2026.")]
    [InlineData("Difieren un 12 % entre plaza e histórico.")]
    [InlineData("Municipio con código DANE 05001000.")]
    [InlineData("Son 8 horas de cargue y 12 estibas.")]
    public void Los_numeros_que_no_parecen_dinero_no_se_revisan(string respuesta)
    {
        Assert.Empty(Sin(respuesta));
    }

    [Fact]
    public void Un_numero_grande_sin_contexto_de_precio_ni_signo_no_se_trata_como_dinero_pero_con_signo_si()
    {
        Assert.Empty(Sin("Referencia interna 4500000."));
        Assert.Equal(["$4500000"], Sin("Referencia $4500000."));
    }

    [Fact]
    public void Un_valor_con_decimales_se_interpreta_bien()
    {
        var fuente = """{"valor":3500000.5}""";

        Assert.Empty(Sin("El valor es $3.500.000,50.", fuente));
    }

    [Fact]
    public void Un_punto_seguido_de_tres_digitos_se_lee_como_miles_a_la_colombiana()
    {
        Assert.Empty(Sin("El valor es $3.500.", """{"valor":3500}"""));
    }

    [Fact]
    public void Sin_fuentes_toda_cifra_de_dinero_es_sin_origen_y_las_repetidas_se_listan_una_vez()
    {
        Assert.Equal(["$1.000.000"], Sin("Valor $1.000.000, repito: valor $1.000.000."));
    }
}
