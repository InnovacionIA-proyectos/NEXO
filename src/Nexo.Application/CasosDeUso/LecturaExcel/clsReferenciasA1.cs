using System.Text.RegularExpressions;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>Columnas y celdas con la notación de Excel: <c>C</c>, <c>C:F</c>, <c>E12</c>.</summary>
public static class clsReferenciasA1
{
    public const int MaxColumnas = 200;

    private static readonly Regex Columna = new(@"^[A-Za-z]{1,3}$", RegexOptions.Compiled);
    private static readonly Regex RangoDeColumnas = new(@"^([A-Za-z]{1,3})(?::([A-Za-z]{1,3}))?$", RegexOptions.Compiled);
    private static readonly Regex Celda = new(@"^([A-Za-z]{1,3})([1-9]\d{0,6})$", RegexOptions.Compiled);

    public static int? ColumnaANumero(string? letras)
    {
        if (string.IsNullOrWhiteSpace(letras) || !Columna.IsMatch(letras.Trim())) return null;
        var n = 0;
        foreach (var ch in letras.Trim().ToUpperInvariant()) n = n * 26 + (ch - 'A' + 1);
        return n <= MaxColumnas ? n : null;
    }

    public static string NumeroAColumna(int numero)
    {
        var s = string.Empty;
        while (numero > 0)
        {
            numero--;
            s = (char)('A' + numero % 26) + s;
            numero /= 26;
        }
        return s;
    }

    /// <summary>Las columnas que nombra una referencia (<c>C</c> o <c>C:F</c>); null si no es válida.</summary>
    public static IReadOnlyList<int>? Columnas(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;
        var m = RangoDeColumnas.Match(referencia.Trim());
        if (!m.Success) return null;
        var desde = ColumnaANumero(m.Groups[1].Value);
        var hasta = m.Groups[2].Success ? ColumnaANumero(m.Groups[2].Value) : desde;
        if (desde is null || hasta is null || hasta < desde) return null;
        return Enumerable.Range(desde.Value, hasta.Value - desde.Value + 1).ToList();
    }

    /// <summary>Las columnas de una lista de referencias, sin repetir y en el orden en que se nombran; null si alguna no es válida.</summary>
    public static IReadOnlyList<int>? Columnas(IEnumerable<string> referencias)
    {
        var resultado = new List<int>();
        foreach (var r in referencias)
        {
            var cols = Columnas(r);
            if (cols is null) return null;
            foreach (var c in cols) if (!resultado.Contains(c)) resultado.Add(c);
        }
        return resultado;
    }

    public static (int Fila, int Col)? ACelda(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;
        var m = Celda.Match(referencia.Trim());
        if (!m.Success) return null;
        var col = ColumnaANumero(m.Groups[1].Value);
        return col is null ? null : (int.Parse(m.Groups[2].Value), col.Value);
    }
}
