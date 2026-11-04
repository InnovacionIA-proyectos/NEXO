using System.Globalization;

namespace Nexo.Domain.Common;

/// <summary>
/// Pesos colombianos para los textos mostrados al usuario. Formato fijo, independiente de la cultura ("$3.500.000"):
/// el contenedor corre con globalización invariante y <c>C0</c> saldría como "¤3,500,000".
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

    /// <summary>3500000 -> "$3.500.000". Redondea a pesos enteros.</summary>
    public static string Pesos(decimal valor) =>
        (valor < 0 ? "-$" : "$") + Math.Abs(Math.Round(valor, 0, MidpointRounding.AwayFromZero)).ToString("N0", Colombia);
}
