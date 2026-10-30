using ClosedXML.Excel;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.Cotizaciones.Adaptadores;

namespace Nexo.Api.Tests;

// S4-HU-17.4/#230: Fuente, Aprovisionamiento y Retorno contenedor se quitan del Excel
// (administracion_embarqa_nexo#206); "Flete" pasa a ser el total completo de la línea,
// no ValorFlete aislado — de lo contrario el número quedaría incompleto.
public class clsExportadorCotizacionExcelTests
{
    private static clsCotizacion NuevaCotizacionConUnaLinea(decimal flete, decimal? aprovisionamiento, decimal? retorno)
    {
        var cotizacion = new clsCotizacion();
        cotizacion.Lineas.Add(new clsCotizacionLinea
        {
            CotizacionId = cotizacion.Id,
            ValorFlete = flete,
            ValorAprovisionamiento = aprovisionamiento,
            ValorRetornoContenedor = retorno,
            Fuente = FuenteFlete.Sicetac,
        });
        return cotizacion;
    }

    [Fact]
    public void El_encabezado_ya_no_trae_Fuente_Aprovisionamiento_ni_Retorno_contenedor()
    {
        var cotizacion = NuevaCotizacionConUnaLinea(1_000_000m, 200_000m, 50_000m);
        var (contenido, _) = new clsExportadorCotizacionExcelClosedXml().Generar(cotizacion);

        using var wb = new XLWorkbook(new MemoryStream(contenido));
        var ws = wb.Worksheet("Cotización");
        var filaEncabezado = ws.Rows().First(r => r.Cell(1).GetString() == "clsRuta");
        var encabezados = filaEncabezado.CellsUsed().Select(c => c.GetString()).ToList();

        Assert.DoesNotContain("Fuente", encabezados);
        Assert.DoesNotContain("Aprovisionamiento", encabezados);
        Assert.DoesNotContain("Retorno contenedor", encabezados);
        Assert.Contains("Flete", encabezados);
    }

    [Fact]
    public void La_columna_Flete_es_el_total_completo_no_solo_ValorFlete()
    {
        var cotizacion = NuevaCotizacionConUnaLinea(flete: 1_000_000m, aprovisionamiento: 200_000m, retorno: 50_000m);
        var (contenido, _) = new clsExportadorCotizacionExcelClosedXml().Generar(cotizacion);

        using var wb = new XLWorkbook(new MemoryStream(contenido));
        var ws = wb.Worksheet("Cotización");
        var filaEncabezado = ws.Rows().First(r => r.Cell(1).GetString() == "clsRuta");
        var columnaFlete = filaEncabezado.CellsUsed().Single(c => c.GetString() == "Flete").Address.ColumnNumber;
        var valorFlete = ws.Cell(filaEncabezado.RowNumber() + 1, columnaFlete).GetValue<decimal>();

        Assert.Equal(1_250_000m, valorFlete); // 1.000.000 + 200.000 + 50.000, no 1.000.000 aislado
    }

    [Fact] // aprovisionamiento/retorno nulos (línea nacional sin contenedor): Flete = solo ValorFlete.
    public void Sin_aprovisionamiento_ni_retorno_Flete_es_solo_ValorFlete()
    {
        var cotizacion = NuevaCotizacionConUnaLinea(flete: 800_000m, aprovisionamiento: null, retorno: null);
        var (contenido, _) = new clsExportadorCotizacionExcelClosedXml().Generar(cotizacion);

        using var wb = new XLWorkbook(new MemoryStream(contenido));
        var ws = wb.Worksheet("Cotización");
        var filaEncabezado = ws.Rows().First(r => r.Cell(1).GetString() == "clsRuta");
        var columnaFlete = filaEncabezado.CellsUsed().Single(c => c.GetString() == "Flete").Address.ColumnNumber;
        var valorFlete = ws.Cell(filaEncabezado.RowNumber() + 1, columnaFlete).GetValue<decimal>();

        Assert.Equal(800_000m, valorFlete);
    }
}
