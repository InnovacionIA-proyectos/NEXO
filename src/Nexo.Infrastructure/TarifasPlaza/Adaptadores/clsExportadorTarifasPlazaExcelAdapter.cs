using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.TarifasPlaza.Adaptadores;

/// <summary>Exporta el catálogo de tarifas de plaza a Excel con ClosedXML — mismas columnas
/// legibles que espera <see cref="Repositorios.clsEfTarifasPlazaRepository.ImportarExcelAsync"/>
/// al leerlo de vuelta, así el mismo archivo sirve para editar y volver a cargar.</summary>
public sealed class clsExportadorTarifasPlazaExcelAdapter : IExportadorTarifasPlazaExcelPort
{
    private static readonly string[] Encabezados =
    [
        "LINEA DE NEGOCIO", "CLASE DE VIAJE", "ORIGEN", "DESTINO", "CLASE CONTENEDOR",
        "RANGO PESO", "TIPO VEHICULO", "VALOR", "VIGENCIA", "DETALLES",
        // S5-HU-22.1/#288: al final y opcionales al importar. Sin ellas, exportar y reimportar un
        // municipio de nombre repetido ("ARMENIA") caería en el primero de la lista.
        "DEPARTAMENTO ORIGEN", "DEPARTAMENTO DESTINO",
    ];

    public (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsTarifaPlaza> tarifas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tarifas de Plaza");

        for (var i = 0; i < Encabezados.Length; i++) ws.Cell(1, i + 1).Value = Encabezados[i];
        ws.Range(1, 1, 1, Encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        var fila = 2;
        foreach (var t in tarifas.OrderBy(x => x.LineaNegocio).ThenBy(x => x.Origen?.Municipio).ThenBy(x => x.Destino?.Municipio))
        {
            ws.Cell(fila, 1).Value = t.LineaNegocio;
            ws.Cell(fila, 2).Value = t.ClaseViaje;
            ws.Cell(fila, 3).Value = t.Origen?.Municipio ?? "";
            ws.Cell(fila, 4).Value = t.Destino?.Municipio ?? "";
            ws.Cell(fila, 5).Value = t.ClaseContenedor ?? "";
            ws.Cell(fila, 6).Value = t.Peso is null ? "" : FormatoRango(t.Peso);
            ws.Cell(fila, 7).Value = t.TipoVehiculo ?? "";
            ws.Cell(fila, 8).Value = t.Valor;
            ws.Cell(fila, 9).Value = t.Vigencia.ToString("yyyy-MM-dd");
            ws.Cell(fila, 10).Value = t.DetallesTarifa;
            ws.Cell(fila, 11).Value = t.Origen?.Departamento ?? "";
            ws.Cell(fila, 12).Value = t.Destino?.Departamento ?? "";
            fila++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"tarifas-plaza-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private static string FormatoRango(clsRangoPeso p) => p.PesoMaximo is { } max ? $"{p.PesoMinimo}-{max}" : $"{p.PesoMinimo}+";
}
