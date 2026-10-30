using System.Globalization;
using ClosedXML.Excel;
using Nexo.Infrastructure.Common;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-22.1/#288: el lector compartido de fechas de Excel entiende fecha nativa, serial de
/// Excel (celda en formato General) y texto en formatos explícitos, y NO depende de la cultura
/// del servidor. Caso real que motivó esto (2026-10-05): serial 46315 (= 2026-10-20) en una celda
/// General, rechazado como "no es una fecha de vigencia válida".
/// </summary>
public class clsLectorFechasExcelTests
{
    private static readonly DateOnly Veinte = new(2026, 10, 20);

    /// <summary>Corre <paramref name="prueba"/> con la cultura ambiente indicada y la restaura.</summary>
    private static void ConCultura(string cultura, Action prueba)
    {
        var anterior = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
            prueba();
        }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = anterior; }
    }

    public static IEnumerable<object[]> Culturas() =>
        new[] { "en-US", "es-CO", "de-DE", "ja-JP", "tr-TR", "ar-SA" }.Select(c => new object[] { c });

    private static IXLCell Celda(Action<IXLCell> configurar)
    {
        var celda = new XLWorkbook().AddWorksheet().Cell(1, 1);
        configurar(celda);
        return celda;
    }

    // ------------------------------------------------------------------ las 3 clases de celda

    [Theory, MemberData(nameof(Culturas))]
    public void Fecha_nativa_de_Excel(string cultura) => ConCultura(cultura, () =>
        Assert.Equal(Veinte, clsLectorFechasExcel.Leer(Celda(c => c.Value = new DateTime(2026, 10, 20)))));

    [Theory, MemberData(nameof(Culturas))]
    public void Serial_de_Excel_en_una_celda_con_formato_General_el_caso_real_46315(string cultura) => ConCultura(cultura, () =>
    {
        var celda = Celda(c => { c.Value = 46315; c.Style.NumberFormat.Format = "General"; });

        Assert.Equal(XLDataType.Number, celda.DataType);   // no es DateTime: por eso fallaba
        Assert.Equal(Veinte, clsLectorFechasExcel.Leer(celda));
    });

    [Theory, MemberData(nameof(Culturas))]
    public void Serial_en_una_celda_de_texto(string cultura) => ConCultura(cultura, () =>
        Assert.Equal(Veinte, clsLectorFechasExcel.Leer(Celda(c => c.Value = "46315"))));

    [Fact]
    public void Serial_con_parte_decimal_toma_el_dia()
    {
        Assert.Equal(Veinte, clsLectorFechasExcel.Leer(Celda(c => c.Value = 46315.75)));
    }

    // ------------------------------------------------------------------ texto, 4 formatos + es-CO

    [Theory, MemberData(nameof(Culturas))]
    public void Los_cuatro_formatos_de_texto_se_leen_igual_con_cualquier_cultura_del_servidor(string cultura) => ConCultura(cultura, () =>
    {
        Assert.Equal(Veinte, clsLectorFechasExcel.LeerTexto("20/10/2026"));    // dd/MM/yyyy
        Assert.Equal(Veinte, clsLectorFechasExcel.LeerTexto("2026-10-20"));    // yyyy-MM-dd
        Assert.Equal(Veinte, clsLectorFechasExcel.LeerTexto("10/20/2026"));    // MM/dd/yyyy (el día 20 no cabe como mes)
        Assert.Equal(new DateOnly(2026, 3, 5), clsLectorFechasExcel.LeerTexto("5/3/2026"));   // d/M/yyyy
    });

    [Theory, MemberData(nameof(Culturas))]
    public void Ante_dia_y_mes_ambiguos_gana_dia_mes_como_en_Colombia(string cultura) => ConCultura(cultura, () =>
        // "05/10/2026" es 5 de octubre, NO 10 de mayo, aunque el servidor corra en en-US.
        Assert.Equal(new DateOnly(2026, 10, 5), clsLectorFechasExcel.LeerTexto("05/10/2026")));

    [Fact]
    public void Texto_con_espacios_alrededor()
    {
        Assert.Equal(Veinte, clsLectorFechasExcel.LeerTexto("  20/10/2026 "));
    }

    [Fact]
    public void El_ultimo_recurso_es_es_CO_no_la_cultura_ambiente()
    {
        // "20 de octubre de 2026" solo se entiende con es-CO; con en-US ambiente no.
        ConCultura("en-US", () => Assert.Equal(Veinte, clsLectorFechasExcel.LeerTexto("20 de octubre de 2026")));
    }

    // ------------------------------------------------------------------ lo que NO es una fecha

    [Theory]
    [InlineData("mañana")]
    [InlineData("31/02/2026")]
    [InlineData("2026-13-45")]
    [InlineData("abc123")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Lo_que_no_es_una_fecha_da_null(string? texto) =>
        Assert.Null(clsLectorFechasExcel.LeerTexto(texto));

    [Theory]
    [InlineData(5)]            // un 5 en la columna de fechas es un dato mal puesto, no el 1900-01-05
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(400000)]
    [InlineData(99999999)]
    public void Un_numero_fuera_del_rango_plausible_de_fechas_se_rechaza(double numero) =>
        Assert.Null(clsLectorFechasExcel.Leer(Celda(c => c.Value = numero)));

    [Theory]
    [InlineData("5")]
    [InlineData("400000")]
    public void Idem_en_texto_con_solo_digitos(string texto) =>
        Assert.Null(clsLectorFechasExcel.LeerTexto(texto));

    [Fact]
    public void Celda_vacia_da_null()
    {
        Assert.Null(clsLectorFechasExcel.Leer(Celda(_ => { })));
    }

    [Theory]
    [InlineData(25569, 1970, 1, 1)]    // límite inferior
    [InlineData(73050, 2099, 12, 31)]  // límite superior
    public void Los_limites_del_rango_plausible_se_aceptan(double serial, int a, int m, int d) =>
        Assert.Equal(new DateOnly(a, m, d), clsLectorFechasExcel.Leer(Celda(c => c.Value = serial)));
}
