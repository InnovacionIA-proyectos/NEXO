namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>Rango de celdas combinadas, con filas y columnas desde 1 (como en Excel).</summary>
public sealed record clsRangoCombinado(int FilaDesde, int ColDesde, int FilaHasta, int ColHasta);

/// <summary>
/// Una hoja tal como está en el archivo: el texto de cada celda y sus celdas combinadas, sin interpretar. Todas las posiciones son
/// desde 1 (fila 1 = primera fila, columna 1 = A).
/// </summary>
public sealed class clsHojaCruda
{
    private const int MaxCeldasPorCombinada = 50_000;

    private readonly string[][] _celdas;
    private readonly Dictionary<(int Fila, int Col), clsRangoCombinado> _combinadaDe = [];

    public clsHojaCruda(string nombre, bool oculta, string[][] celdas, IReadOnlyList<clsRangoCombinado>? combinadas = null)
    {
        Nombre = nombre;
        Oculta = oculta;
        _celdas = celdas;
        Columnas = celdas.Length == 0 ? 0 : celdas.Max(f => f.Length);
        Combinadas = combinadas ?? [];
        foreach (var r in Combinadas)
        {
            if ((long)(r.FilaHasta - r.FilaDesde + 1) * (r.ColHasta - r.ColDesde + 1) > MaxCeldasPorCombinada) continue;
            for (var f = r.FilaDesde; f <= r.FilaHasta; f++)
                for (var c = r.ColDesde; c <= r.ColHasta; c++)
                    _combinadaDe[(f, c)] = r;
        }
    }

    public string Nombre { get; }
    public bool Oculta { get; }
    public int Filas => _celdas.Length;
    public int Columnas { get; }
    public IReadOnlyList<clsRangoCombinado> Combinadas { get; }

    /// <summary>El texto de la celda; vacío si está fuera de la hoja o no tiene nada.</summary>
    public string Valor(int fila, int col)
    {
        if (fila < 1 || fila > _celdas.Length || col < 1) return string.Empty;
        var f = _celdas[fila - 1];
        return col <= f.Length ? f[col - 1] ?? string.Empty : string.Empty;
    }

    /// <summary>Como <see cref="Valor"/>, pero una celda combinada devuelve el texto de su celda superior izquierda (los títulos de grupo).</summary>
    public string ValorEfectivo(int fila, int col)
    {
        var v = Valor(fila, col);
        if (v.Length > 0 || !_combinadaDe.TryGetValue((fila, col), out var r)) return v;
        return Valor(r.FilaDesde, r.ColDesde);
    }

    /// <summary>
    /// El texto de una celda de datos. Una celda vacía dentro de una combinación VERTICAL toma el valor de arriba (la misma etiqueta repartida en varias filas);
    /// una combinación horizontal no se repite en cada columna.
    /// </summary>
    public string ValorDeDato(int fila, int col)
    {
        var v = Valor(fila, col);
        if (v.Length > 0 || !_combinadaDe.TryGetValue((fila, col), out var r) || r.FilaHasta == r.FilaDesde) return v;
        return col == r.ColDesde ? Valor(r.FilaDesde, r.ColDesde) : string.Empty;
    }

    /// <summary>True si la celda es parte de una combinación pero no su primera columna (el resto de un título que ocupa varias columnas).</summary>
    public bool EsContinuacionDeCombinada(int fila, int col) =>
        _combinadaDe.TryGetValue((fila, col), out var r) && col > r.ColDesde;

    public bool FilaVacia(int fila)
    {
        if (fila < 1 || fila > _celdas.Length) return true;
        return _celdas[fila - 1].All(string.IsNullOrWhiteSpace);
    }

    /// <summary>La hoja con las filas y las columnas intercambiadas (también las celdas combinadas).</summary>
    public clsHojaCruda Trasponer()
    {
        var nuevas = new string[Columnas][];
        for (var c = 0; c < Columnas; c++)
        {
            nuevas[c] = new string[_celdas.Length];
            for (var f = 0; f < _celdas.Length; f++)
                nuevas[c][f] = c < _celdas[f].Length ? _celdas[f][c] ?? string.Empty : string.Empty;
        }
        var comb = Combinadas.Select(r => new clsRangoCombinado(r.ColDesde, r.FilaDesde, r.ColHasta, r.FilaHasta)).ToList();
        return new clsHojaCruda(Nombre, Oculta, nuevas, comb);
    }
}
