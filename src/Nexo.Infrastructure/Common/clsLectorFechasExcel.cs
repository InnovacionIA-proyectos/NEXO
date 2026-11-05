using System.Globalization;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.Common;

/// <summary>
/// Lee una fecha de una celda de Excel sin depender de la cultura del servidor: fecha nativa, serial (solo 1970-2099), texto con formatos explícitos (día/mes primero) y es-CO al final.
/// Devuelve null si la celda está vacía o no es una fecha reconocible.
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
