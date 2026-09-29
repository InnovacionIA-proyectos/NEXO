using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ReglasSoloSicetac.Adaptadores;

/// <summary>Genera la plantilla Excel de reglas "solo SICE-TAC" con ClosedXML — mismas
/// 3 columnas que espera <see cref="clsExcelReglasSoloSicetacAdapter"/> al leer la carga masiva.</summary>
public sealed class clsPlantillaReglasSoloSicetacAdapter : IPlantillaReglasSoloSicetacPort
{
    public (byte[] Contenido, string NombreArchivo) Generar()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Reglas Solo SICE-TAC");

        var encabezados = new[] { "ORIGEN", "DESTINO", "TIPO VEHICULO" };
        for (var i = 0; i < encabezados.Length; i++) ws.Cell(1, i + 1).Value = encabezados[i];
        ws.Range(1, 1, 1, encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        // Fila de ejemplo: tipo de vehículo con código real del catálogo SAP.
        ws.Cell(2, 1).Value = "Medellín";
        ws.Cell(2, 2).Value = "Bogotá D.C.";
        ws.Cell(2, 3).Value = "3S3";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), "plantilla-reglas-solo-sicetac.xlsx");
    }
}
