using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ReglasSoloSicetac.Adaptadores;

/// <summary>
/// Excel de descarga de las reglas "solo SICE-TAC" (S5-HU-22.1/#288). Mismas 3 columnas que la
/// plantilla y que lee <see cref="clsExcelReglasSoloSicetacAdapter"/> al importar.
///
/// Origen y Destino se escriben TAL COMO están guardados —código DANE en las cargas de S4-HU-09.2,
/// texto normalizado en las filas más viejas— y como TEXTO, no número: un DANE como "05001000"
/// perdería el cero inicial si Excel lo tomara por numérico, y la regla reimportada ya no
/// coincidiría con la guardada.
/// </summary>
public sealed class clsExportadorReglasSoloSicetacExcelAdapter : IExportadorReglasSoloSicetacExcelPort
{
    private static readonly string[] Encabezados = ["ORIGEN", "DESTINO", "TIPO VEHICULO"];

    public (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsReglaSoloSicetac> reglas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Reglas Solo SICE-TAC");

        for (var i = 0; i < Encabezados.Length; i++) ws.Cell(1, i + 1).Value = Encabezados[i];
        ws.Range(1, 1, 1, Encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        var fila = 2;
        foreach (var r in reglas.OrderBy(x => x.Origen).ThenBy(x => x.Destino).ThenBy(x => x.TipoVehiculo))
        {
            ws.Cell(fila, 1).Value = r.Origen;   // string = texto (no se reinterpreta como número)
            ws.Cell(fila, 2).Value = r.Destino;
            ws.Cell(fila, 3).Value = r.TipoVehiculo;
            fila++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"reglas-solo-sicetac-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
