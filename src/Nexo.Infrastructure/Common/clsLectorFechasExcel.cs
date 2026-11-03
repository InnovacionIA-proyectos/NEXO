using System.Globalization;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.Common;

/// <summary>
/// Lee una fecha de una celda de Excel sin depender de la cultura del servidor
/// (S5-HU-22.1/#288). Es LA forma de leer fechas en todo importador de Nexo: antes cada uno
/// tenía la suya, y la de Tarifas de Plaza (<c>GetString()</c> + <c>DateOnly.TryParse</c> sin
/// cultura) rechazaba una fecha guardada como valor nativo de Excel.
///
/// Orden, de más a menos confiable:
///  1. Tipo fecha nativo de la celda.
///  2. Serial de Excel: una fecha en una celda con formato General (o de texto con solo
///     dígitos) llega como el número de días desde 1899-12-30 — p. ej. 46315 = 2026-10-20.
///     Solo se acepta dentro de un rango plausible de fechas (1970-2099): un 5 o un 400000
///     no son una fecha, son un dato mal puesto en la columna, y deben rechazarse.
///  3. Texto en formatos explícitos, en InvariantCulture: dd/MM/yyyy, d/M/yyyy, yyyy-MM-dd,
///     MM/dd/yyyy (en ese orden: ante "05/10/2026" gana día/mes, como en Colombia).
///  4. Al final, es-CO a propósito (nunca la cultura ambiente del proceso).
///
/// Devuelve null si la celda está vacía o si lo que trae no es una fecha reconocible — el
/// llamador distingue ambos casos con <see cref="IXLCell.IsEmpty"/>.
/// </summary>
public static class clsLectorFechasExcel
{
    private static readonly string[] Formatos = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"];
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    // 1970-01-01 y 2099-12-31 como seriales de Excel.
    private const double SerialMinimo = 25569;
    private const double SerialMaximo = 73050;

    public static DateOnly? Leer(IXLCell celda)
    {
        if (celda.IsEmpty()) return null;

        if (celda.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(celda.GetDateTime());
        if (celda.DataType == XLDataType.Number) return DesdeSerial(celda.GetDouble());

        return LeerTexto(celda.GetString());
    }

    /// <summary>La parte de texto, expuesta para probarla sin armar un Excel.</summary>
    public static DateOnly? LeerTexto(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        // Celda de texto con un serial adentro ("46315"): mismo caso que el numérico.
        if (t.All(char.IsAsciiDigit) && double.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var serial))
            return DesdeSerial(serial);

        foreach (var formato in Formatos)
            if (DateTime.TryParseExact(t, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                return DateOnly.FromDateTime(f);

        return DateTime.TryParse(t, EsCo, DateTimeStyles.None, out var g) ? DateOnly.FromDateTime(g) : null;
    }

    private static DateOnly? DesdeSerial(double serial) =>
        serial is >= SerialMinimo and <= SerialMaximo ? DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serial))) : null;
}
