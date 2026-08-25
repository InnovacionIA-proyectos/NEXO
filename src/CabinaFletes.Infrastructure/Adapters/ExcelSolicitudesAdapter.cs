using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Exceptions;
using ClosedXML.Excel;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Análisis tolerante de Excel de licitación: cada cliente envía una plantilla
/// distinta, así que solo se detecta el encabezado (fila con >= 3 valores
/// distintos) y el mapeo de columnas lo decide el usuario en el frontend.
/// Portado 1:1 desde CapturaSolicitudService.AnalizarExcelAsync del original.
/// </summary>
public sealed class ExcelSolicitudesAdapter : IExcelSolicitudesPort
{
    private const int MaxFilasPorHoja = 1000;
    private const int FilasBusquedaEncabezado = 15;

    public Task<AnalisisExcelDto> AnalizarAsync(Stream archivo, string nombreArchivo, CancellationToken ct)
    {
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(archivo);
        }
        catch (Exception ex)
        {
            throw new ValidationFailedException("EXCEL_INVALIDO", $"No se pudo leer el archivo Excel: {ex.Message}");
        }

        using (wb)
        {
            var hojas = new List<HojaExcelDto>();
            foreach (var ws in wb.Worksheets)
            {
                var usado = ws.RangeUsed();
                if (usado is null) continue;

                var primeraFila = usado.FirstRow().RowNumber();
                var ultimaFila = usado.LastRow().RowNumber();
                var primeraCol = usado.FirstColumn().ColumnNumber();
                var ultimaCol = usado.LastColumn().ColumnNumber();

                var combinadas = new Dictionary<(int Fila, int Col), string>();
                foreach (var rango in ws.MergedRanges)
                {
                    var valor = rango.FirstCell().GetFormattedString().Trim();
                    if (valor.Length == 0) continue;
                    var dir = rango.RangeAddress;
                    for (var r = dir.FirstAddress.RowNumber; r <= dir.LastAddress.RowNumber; r++)
                        for (var c = dir.FirstAddress.ColumnNumber; c <= dir.LastAddress.ColumnNumber; c++)
                            combinadas[(r, c)] = valor;
                }

                string Celda(int r, int c) =>
                    combinadas.TryGetValue((r, c), out var v) ? v : ws.Cell(r, c).GetFormattedString().Trim();

                var filaEncabezado = 0;
                for (var r = primeraFila; r <= Math.Min(ultimaFila, primeraFila + FilasBusquedaEncabezado); r++)
                {
                    var distintos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var c = primeraCol; c <= ultimaCol; c++)
                    {
                        var v = Celda(r, c);
                        if (v.Length > 0) distintos.Add(v);
                    }
                    if (distintos.Count >= 3) { filaEncabezado = r; break; }
                }
                if (filaEncabezado == 0) continue;

                var filaSubEncabezado = 0;
                if (filaEncabezado < ultimaFila)
                {
                    var rotulos = new List<string>();
                    for (var c = primeraCol; c <= ultimaCol; c++)
                    {
                        var v = Celda(filaEncabezado, c);
                        if (v.Length > 0) rotulos.Add(v);
                    }
                    var hayRepetidos = rotulos.Count != rotulos.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    if (hayRepetidos)
                    {
                        var sub = new List<string>();
                        for (var c = primeraCol; c <= ultimaCol; c++)
                        {
                            var v = Celda(filaEncabezado + 1, c);
                            if (v.Length > 0) sub.Add(v);
                        }
                        if (sub.Count >= 2 && sub.All(v => !EsNumerico(v)))
                            filaSubEncabezado = filaEncabezado + 1;
                    }
                }

                var encabezados = new List<string>();
                for (var c = primeraCol; c <= ultimaCol; c++)
                {
                    var texto = Celda(filaEncabezado, c);
                    if (filaSubEncabezado > 0)
                    {
                        var sub = Celda(filaSubEncabezado, c);
                        if (sub.Length > 0 && !sub.Equals(texto, StringComparison.OrdinalIgnoreCase))
                            texto = texto.Length == 0 ? sub : $"{texto} · {sub}";
                    }
                    encabezados.Add(texto.Length == 0 ? $"Columna {XLHelper.GetColumnLetterFromNumber(c)}" : texto);
                }

                var filas = new List<IReadOnlyList<string>>();
                var totalFilas = 0;
                for (var r = Math.Max(filaEncabezado, filaSubEncabezado) + 1; r <= ultimaFila; r++)
                {
                    var fila = new List<string>(encabezados.Count);
                    var vacia = true;
                    for (var c = primeraCol; c <= ultimaCol; c++)
                    {
                        var v = Celda(r, c);
                        if (v.Length > 0) vacia = false;
                        fila.Add(v);
                    }
                    if (vacia) continue;
                    totalFilas++;
                    if (filas.Count < MaxFilasPorHoja) filas.Add(fila);
                }

                hojas.Add(new HojaExcelDto(ws.Name, filaEncabezado, encabezados, filas, totalFilas));
            }

            if (hojas.Count == 0)
                throw new ValidationFailedException("EXCEL_SIN_DATOS", "El archivo no contiene hojas con datos tabulares.");

            return Task.FromResult(new AnalisisExcelDto(nombreArchivo, hojas));
        }
    }

    private static bool EsNumerico(string s)
    {
        var limpio = s.Replace("$", "").Replace(",", "").Replace(".", "").Replace(" ", "").TrimEnd('%');
        return limpio.Length > 0 && limpio.All(char.IsDigit);
    }
}
