using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.ReglasSoloSicetac.Dtos;
using CabinaFletes.Application.ReglasSoloSicetac.Ports;
using ClosedXML.Excel;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>Lee el Excel de carga masiva de reglas "solo SICE-TAC" con ClosedXML.</summary>
public sealed class ExcelReglasSoloSicetacAdapter : IExcelReglasSoloSicetacPort
{
    public IReadOnlyList<FilaReglaSoloSicetacDto> Leer(Stream archivo)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(archivo); }
        catch (Exception) { throw new ValidationFailedException("EXCEL_INVALIDO", "No se pudo leer el archivo. Debe ser un Excel .xlsx válido."); }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(w => w.RangeUsed() is not null)
                ?? throw new ValidationFailedException("EXCEL_SIN_DATOS", "El archivo no contiene datos.");
            var usado = ws.RangeUsed()!;
            var filas = usado.RowsUsed().ToList();
            static string Txt(IXLRangeRow r, int c) => r.Cell(c).GetString().Trim();
            static string Norm(string s) => s.Trim().ToUpperInvariant();

            // Columnas: por defecto Origen | Destino | Tipo de vehículo (1,2,3). Si la primera
            // fila parece encabezado, se mapea por su texto y los datos empiezan en la fila 2.
            int cOrigen = 1, cDestino = 2, cVeh = 3, primeraDataIdx = 0;
            var cols = usado.ColumnCount();
            var encabezado = filas.First();
            var pareceEncabezado = Enumerable.Range(1, cols)
                .Select(c => Norm(Txt(encabezado, c)))
                .Any(h => h.Contains("ORIGEN") || h.Contains("DESTINO") || h.Contains("VEHIC") || h.Contains("VEHÍC") || h.Contains("TIPO"));
            if (pareceEncabezado)
            {
                for (var c = 1; c <= cols; c++)
                {
                    var h = Norm(Txt(encabezado, c));
                    if (h.Contains("ORIGEN")) cOrigen = c;
                    else if (h.Contains("DESTINO")) cDestino = c;
                    else if (h.Contains("VEHIC") || h.Contains("VEHÍC") || h.Contains("TIPO")) cVeh = c;
                }
                primeraDataIdx = 1;
            }

            return filas.Skip(primeraDataIdx)
                .Select(f => new FilaReglaSoloSicetacDto(Txt(f, cOrigen), Txt(f, cDestino), Txt(f, cVeh)))
                .ToList();
        }
    }
}
