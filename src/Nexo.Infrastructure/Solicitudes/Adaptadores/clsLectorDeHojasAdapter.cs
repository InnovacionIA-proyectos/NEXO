using ClosedXML.Excel;
using Nexo.Application.CasosDeUso.LecturaExcel;
using Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>Lee todas las hojas de un .xlsx (visibles y ocultas) con el texto formateado de cada celda y sus celdas combinadas.</summary>
public sealed class clsLectorDeHojasAdapter : ILectorDeHojasPort
{
    private const int MaxFilas = 10_000;
    private const int MaxColumnas = 200;

    public Task<IReadOnlyList<clsHojaCruda>> LeerAsync(Stream archivo, CancellationToken ct = default)
    {
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(archivo);
        }
        catch (Exception ex)
        {
            throw new clsValidationFailedException("EXCEL_INVALIDO", $"No se pudo leer el archivo Excel: {ex.Message}");
        }

        using (wb)
        {
            var hojas = new List<clsHojaCruda>();
            foreach (var ws in wb.Worksheets)
            {
                var celdasUsadas = ws.CellsUsed(XLCellsUsedOptions.Contents).ToList();
                if (celdasUsadas.Count == 0) continue;

                var ultimaFila = Math.Min(celdasUsadas.Max(c => c.Address.RowNumber), MaxFilas);
                var ultimaCol = Math.Min(celdasUsadas.Max(c => c.Address.ColumnNumber), MaxColumnas);
                var celdas = new string[ultimaFila][];
                for (var f = 0; f < ultimaFila; f++)
                {
                    celdas[f] = new string[ultimaCol];
                    Array.Fill(celdas[f], string.Empty);
                }

                foreach (var c in celdasUsadas)
                {
                    var f = c.Address.RowNumber;
                    var col = c.Address.ColumnNumber;
                    if (f > ultimaFila || col > ultimaCol) continue;
                    celdas[f - 1][col - 1] = c.GetFormattedString().Trim();
                }

                var combinadas = ws.MergedRanges
                    .Select(r => new clsRangoCombinado(
                        r.RangeAddress.FirstAddress.RowNumber, r.RangeAddress.FirstAddress.ColumnNumber,
                        r.RangeAddress.LastAddress.RowNumber, r.RangeAddress.LastAddress.ColumnNumber))
                    .ToList();

                hojas.Add(new clsHojaCruda(ws.Name, ws.Visibility != XLWorksheetVisibility.Visible, celdas, combinadas));
            }

            if (hojas.Count == 0)
                throw new clsValidationFailedException("EXCEL_SIN_DATOS", "El archivo no contiene hojas con datos.");
            return Task.FromResult<IReadOnlyList<clsHojaCruda>>(hojas);
        }
    }
}
