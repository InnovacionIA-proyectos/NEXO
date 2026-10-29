using System.Globalization;
using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.MapeoExcel;

/// <summary>
/// Las conversiones de una celda del Excel de un cliente al valor que usa NEXO. Todas son deterministas y puras: la misma celda da siempre el mismo
/// resultado, sin base de datos ni IA, y una celda que no se entiende se rechaza con un motivo en vez de adivinarse.
/// </summary>
public static class clsConversoresDeCelda
{
    /// <summary>Cómo se leen los separadores de un número.</summary>
    public enum ModoNumero
    {
        /// <summary>Punto decimal. La coma solo se acepta como separador de miles (<c>1,234.56</c>).</summary>
        Invariante,

        /// <summary>Coma decimal y punto de miles (<c>1.234,56</c>).</summary>
        ComaDecimal,

        /// <summary>
        /// Para toneladas, que casi siempre llevan decimales (<c>4,5</c> o <c>4.5</c>): una sola coma o un solo punto es el decimal; con los dos, el
        /// último es el decimal y el otro separa miles.
        /// </summary>
        Toneladas,
    }

    private static readonly Regex Miles = new(@"^-?\d{1,3}(,\d{3})+(\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex NumeroSimple = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex Moneda = new(@"^(\$|COP)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ---------------------------------------------------------------- números

    /// <summary>Un número, o false si la celda no es un número. Acepta <c>$</c> o <c>COP</c> delante y espacios; ninguna otra letra.</summary>
    public static bool TryNumero(string? texto, ModoNumero modo, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var s = Moneda.Replace(texto.Replace(' ', ' ').Trim(), string.Empty).Replace(" ", string.Empty);
        if (s.Length == 0) return false;

        switch (modo)
        {
            case ModoNumero.ComaDecimal:
                s = s.Replace(".", string.Empty).Replace(',', '.');
                break;
            case ModoNumero.Toneladas:
                s = NormalizarToneladas(s);
                break;
            default:
                if (Miles.IsMatch(s)) s = s.Replace(",", string.Empty);
                break;
        }

        return NumeroSimple.IsMatch(s) && decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);
    }

    private static string NormalizarToneladas(string s)
    {
        var punto = s.LastIndexOf('.');
        var coma = s.LastIndexOf(',');
        if (punto >= 0 && coma >= 0)
            return punto > coma ? s.Replace(",", string.Empty) : s.Replace(".", string.Empty).Replace(',', '.');
        return s.Replace(',', '.');
    }

    // ---------------------------------------------------------------- sí / no

    private static readonly HashSet<string> Si = ["SI", "S", "X", "TRUE", "VERDADERO", "1", "YES", "Y"];
    private static readonly HashSet<string> No = ["NO", "N", "FALSE", "FALSO", "0"];

    /// <summary>Un sí o un no (<c>sí</c>, <c>x</c>, <c>1</c>, <c>true</c>… / <c>no</c>, <c>0</c>, <c>false</c>…). False si no es ninguno de los dos.</summary>
    public static bool TryBooleano(string? texto, out bool valor)
    {
        valor = false;
        var n = clsResolucionDeCatalogos.Normalizar(texto);
        if (Si.Contains(n)) { valor = true; return true; }
        return No.Contains(n);
    }

    // ---------------------------------------------------------------- fechas

    private static readonly string[] FormatosDeFecha =
    [
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy/MM/dd",
        "d/M/yyyy", "dd/MM/yyyy", "d/M/yyyy H:mm", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm:ss", "dd/MM/yyyy HH:mm:ss",
        "d-M-yyyy", "dd-MM-yyyy", "d-M-yy", "d/M/yy",
    ];

    /// <summary>
    /// Una fecha (día primero: <c>31/10/2026</c>, como se escribe en Colombia; o ISO <c>2026-10-31</c>). Una fecha sin hora vale hasta el final de ese día
    /// en Colombia (23:59:59, UTC-5): un «fecha límite 31/10» se entiende como que el 31 todavía cuenta.
    /// </summary>
    public static bool TryFecha(string? texto, out DateTimeOffset valor)
    {
        valor = default;
        var s = texto?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (!DateTime.TryParseExact(s, FormatosDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) return false;

        var sinHora = fecha.TimeOfDay == TimeSpan.Zero;
        var local = sinHora ? fecha.Date.AddDays(1).AddSeconds(-1) : fecha;
        valor = new DateTimeOffset(local, TimeSpan.FromHours(-5));
        return true;
    }

    // ---------------------------------------------------------------- origen y destino en una sola celda

    private static readonly string[] SeparadoresConEspacios = [" - ", " – ", " — ", " / ", " > ", " → ", " | "];

    /// <summary>
    /// Parte «ORIGEN - DESTINO» en sus dos ciudades. Primero busca un separador rodeado de espacios (<c> - </c>, <c> / </c>, <c> → </c>…); si no hay,
    /// acepta un único guion sin espacios. Si hay más de un separador del mismo tipo o alguna parte queda vacía, no se puede partir sin adivinar
    /// y devuelve false.
    /// </summary>
    public static bool TryDividirOrigenDestino(string? texto, out string origen, out string destino)
    {
        origen = destino = string.Empty;
        var s = texto?.Trim();
        if (string.IsNullOrEmpty(s)) return false;

        foreach (var separador in SeparadoresConEspacios)
        {
            var partes = s.Split(separador, StringSplitOptions.None);
            if (partes.Length == 1) continue;
            return Asignar(partes, out origen, out destino);
        }

        var simple = s.Split('-');
        return simple.Length == 2 && Asignar(simple, out origen, out destino);
    }

    private static bool Asignar(string[] partes, out string origen, out string destino)
    {
        origen = destino = string.Empty;
        if (partes.Length != 2) return false;
        origen = partes[0].Trim();
        destino = partes[1].Trim();
        return origen.Length > 0 && destino.Length > 0;
    }
}
