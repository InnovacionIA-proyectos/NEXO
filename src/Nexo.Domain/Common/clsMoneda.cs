using System.Globalization;

namespace Nexo.Domain.Common;

/// <summary>
/// Pesos colombianos para los textos que Nexo le muestra al usuario (justificaciones del costeo, avisos de piso).
///
/// S6-HU-29.2/#312: esos textos usaban <c>{valor:C0}</c>, que formatea con la cultura AMBIENTE del proceso. El contenedor corre con
/// globalización invariante (sin LANG/ICU), cuya cultura no trae símbolo de moneda real: salía "¤3,500,000" en vez de "$3.500.000".
/// Acá el formato es fijo y NO depende de ninguna cultura: "$" delante, punto como separador de miles, sin decimales — como se
/// escribe en Colombia.
/// </summary>
public static class clsMoneda
{
    private static readonly NumberFormatInfo Colombia = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    /// <summary>3500000 -> "$3.500.000". Redondea a pesos enteros (los fletes se cotizan sin centavos).</summary>
    public static string Pesos(decimal valor) =>
        (valor < 0 ? "-$" : "$") + Math.Abs(Math.Round(valor, 0, MidpointRounding.AwayFromZero)).ToString("N0", Colombia);
}
