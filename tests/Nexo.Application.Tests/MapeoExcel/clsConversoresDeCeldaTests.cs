using Nexo.Application.CasosDeUso.MapeoExcel;
using static Nexo.Application.CasosDeUso.MapeoExcel.clsConversoresDeCelda;

namespace Nexo.Application.Tests.MapeoExcel;

/// <summary>Las conversiones de celda son deterministas: lo que se entiende da siempre lo mismo y lo que no, se rechaza sin adivinar.</summary>
public class clsConversoresDeCeldaTests
{
    // ---------------------------------------------------------------- números

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("  8 ", 8)]
    [InlineData("$ 1,500,000", 1500000)]
    [InlineData("COP 5000", 5000)]
    [InlineData("-3.5", -3.5)]
    public void Un_numero_con_punto_decimal_se_lee(string texto, double esperado)
    {
        Assert.True(TryNumero(texto, ModoNumero.Invariante, out var n));
        Assert.Equal((decimal)esperado, n);
    }

    [Theory]
    [InlineData("1.234,56")]   // coma decimal sin pedirlo: no se adivina
    [InlineData("4,5")]
    [InlineData("12 kg")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Lo_que_no_es_un_numero_con_punto_decimal_se_rechaza(string? texto) =>
        Assert.False(TryNumero(texto, ModoNumero.Invariante, out _));

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("4,5", 4.5)]
    [InlineData("1234", 1234)]
    [InlineData("1.234.567,5", 1234567.5)]
    [InlineData("$ 1.500.000", 1500000)]
    public void Un_numero_con_coma_decimal_se_lee(string texto, double esperado)
    {
        Assert.True(TryNumero(texto, ModoNumero.ComaDecimal, out var n));
        Assert.Equal((decimal)esperado, n);
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("x,5")]
    public void Un_numero_con_coma_decimal_mal_escrito_se_rechaza(string texto) =>
        Assert.False(TryNumero(texto, ModoNumero.ComaDecimal, out _));

    [Theory]
    [InlineData("4,5", 4.5)]
    [InlineData("4.5", 4.5)]
    [InlineData("12", 12)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("25.000", 25)] // una sola coma o punto es el decimal: 25 toneladas, no 25 mil
    public void Las_toneladas_leen_un_unico_separador_como_decimal(string texto, double esperado)
    {
        Assert.True(TryNumero(texto, ModoNumero.Toneladas, out var n));
        Assert.Equal((decimal)esperado, n);
    }

    // ---------------------------------------------------------------- sí / no

    [Theory]
    [InlineData("SI", true)]
    [InlineData("Sí", true)]
    [InlineData("x", true)]
    [InlineData("1", true)]
    [InlineData("verdadero", true)]
    [InlineData("yes", true)]
    [InlineData("NO", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    [InlineData("Falso", false)]
    public void Un_si_o_un_no_se_lee(string texto, bool esperado)
    {
        Assert.True(TryBooleano(texto, out var b));
        Assert.Equal(esperado, b);
    }

    [Theory]
    [InlineData("quizás")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2")]
    public void Lo_que_no_es_si_ni_no_se_rechaza(string? texto) => Assert.False(TryBooleano(texto, out _));

    // ---------------------------------------------------------------- fechas

    [Fact]
    public void Una_fecha_sin_hora_vale_hasta_el_final_del_dia_en_Colombia()
    {
        Assert.True(TryFecha("31/10/2026", out var f));
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(-5)), f);
    }

    [Theory]
    [InlineData("2026-10-31")]
    [InlineData("1/11/2026")]
    [InlineData("01-11-2026")]
    public void Se_leen_las_fechas_con_el_dia_primero_y_la_ISO(string texto) => Assert.True(TryFecha(texto, out _));

    [Fact]
    public void Una_fecha_con_hora_conserva_la_hora()
    {
        Assert.True(TryFecha("31/10/2026 14:30", out var f));
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 14, 30, 0, TimeSpan.FromHours(-5)), f);
    }

    [Fact]
    public void El_dia_va_primero_asi_que_un_mes_13_no_es_fecha() => Assert.False(TryFecha("10/31/2026", out _));

    [Theory]
    [InlineData("mañana")]
    [InlineData("31/02/2026")]
    [InlineData("")]
    [InlineData(null)]
    public void Lo_que_no_es_una_fecha_se_rechaza(string? texto) => Assert.False(TryFecha(texto, out _));

    // ---------------------------------------------------------------- origen y destino

    [Theory]
    [InlineData("BOGOTA - CALI", "BOGOTA", "CALI")]
    [InlineData("Bogotá / Cali", "Bogotá", "Cali")]
    [InlineData("Bogotá → Cali", "Bogotá", "Cali")]
    [InlineData("Bogotá > Cali", "Bogotá", "Cali")]
    [InlineData("Bogotá – Cali", "Bogotá", "Cali")]
    [InlineData("Cali-Bogotá", "Cali", "Bogotá")]
    [InlineData("  Santa Marta  -  Barranquilla ", "Santa Marta", "Barranquilla")]
    public void Origen_y_destino_se_separan(string texto, string origen, string destino)
    {
        Assert.True(TryDividirOrigenDestino(texto, out var o, out var d));
        Assert.Equal((origen, destino), (o, d));
    }

    [Theory]
    [InlineData("BOGOTA")]
    [InlineData("Bogotá - ")]
    [InlineData(" - Cali")]
    [InlineData("A - B - C")]      // dos separadores: no se sabe cuál parte
    [InlineData("Cali-Bogotá-Pasto")]
    [InlineData("")]
    [InlineData(null)]
    public void Lo_que_no_se_puede_separar_sin_adivinar_se_rechaza(string? texto) =>
        Assert.False(TryDividirOrigenDestino(texto, out _, out _));

    [Fact]
    public void Un_guion_con_espacios_gana_sobre_los_guiones_de_los_nombres()
    {
        Assert.True(TryDividirOrigenDestino("Cali - Puerto Asís-Putumayo", out var o, out var d));
        Assert.Equal(("Cali", "Puerto Asís-Putumayo"), (o, d));
    }
}
