using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ServiciosConexos.Adaptadores;

/// <summary>Exporta el catálogo de servicios conexos a Excel con ClosedXML — mismas columnas
/// que lee <see cref="Repositorios.clsEfServiciosConexosRepository.ImportarExcelAsync"/> al
/// reimportarlo, así el mismo archivo sirve para editar y volver a cargar como
/// actualización (S4-HU-15.3/#249).</summary>
public sealed class clsExportadorServiciosConexosExcelAdapter : IExportadorServiciosConexosExcelPort
{
    private static readonly string[] Encabezados =
    [
        "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "PUERTO", "RUTA", "TIPO MAQUINA",
        "COSTO UNITARIO", "UNIDAD", "VIGENCIA", "CATEGORIA", "ACTIVO",
    ];

    public (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsServicioConexoDto> servicios)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Servicios Conexos");

        for (var i = 0; i < Encabezados.Length; i++) ws.Cell(1, i + 1).Value = Encabezados[i];
        ws.Range(1, 1, 1, Encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        var fila = 2;
        foreach (var s in servicios.OrderBy(x => x.TipoServicio).ThenBy(x => x.Origen))
        {
            ws.Cell(fila, 1).Value = s.CodigoServicio;
            ws.Cell(fila, 2).Value = s.TipoServicio;
            ws.Cell(fila, 3).Value = s.Origen;
            ws.Cell(fila, 4).Value = s.Destino;
            ws.Cell(fila, 5).Value = s.Puerto;
            ws.Cell(fila, 6).Value = s.Ruta;
            ws.Cell(fila, 7).Value = s.TipoMaquina;
            ws.Cell(fila, 8).Value = s.CostoUnitario;
            ws.Cell(fila, 9).Value = s.Unidad;
            ws.Cell(fila, 10).Value = s.Vigencia.ToString("yyyy-MM-dd");
            ws.Cell(fila, 11).Value = s.Categoria;
            ws.Cell(fila, 12).Value = s.Activo ? "SI" : "NO";
            fila++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"servicios-conexos-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
