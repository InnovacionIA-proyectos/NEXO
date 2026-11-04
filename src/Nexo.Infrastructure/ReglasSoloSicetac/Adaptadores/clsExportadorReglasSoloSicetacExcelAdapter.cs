using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ReglasSoloSicetac.Adaptadores;

/// <summary>Excel de descarga de las reglas "solo SICE-TAC", con las mismas 3 columnas de la plantilla. Origen y Destino se escriben
/// tal como están guardados y como TEXTO: un DANE como "05001000" perdería el cero inicial si Excel lo tomara por número.</summary>
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
