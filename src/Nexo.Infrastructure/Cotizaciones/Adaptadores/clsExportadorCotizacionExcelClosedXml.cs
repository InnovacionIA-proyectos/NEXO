using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.Cotizaciones.Adaptadores;

/// <summary>Genera el Excel de detalle de una cotización (fletes + servicios) con ClosedXML.</summary>
public sealed class clsExportadorCotizacionExcelClosedXml : IExportadorCotizacionExcelPort
{
    private const string FormatoMoneda = "\"$\" #,##0";

    /// <summary>Texto de la columna "Flete" de una línea rechazada (S6-HU-24.3/#320): sin precio y sin motivo.</summary>
    public const string SinCosteo = "Sin costeo";

    public (byte[] Contenido, string NombreArchivo) Generar(clsCotizacion c, IReadOnlyList<clsLineaRechazadaInfo>? lineasRechazadas = null)
    {
        lineasRechazadas ??= [];
        // Catálogo de los servicios usados: nombre + categoría (logístico | adicional).
        string Nombre(clsCotizacionConexo x) => x.clsServicioConexo?.TipoServicio ?? "Servicio";
        string Categoria(clsCotizacionConexo x) => x.clsServicioConexo?.Categoria == "adicional" ? "adicional" : "logistico";

        var logisticos = c.Conexos.Where(x => Categoria(x) == "logistico").ToList();
        var adicionales = c.Conexos.Where(x => Categoria(x) == "adicional").ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cotización");
        var f = 1;

        ws.Cell(f, 1).Value = "Cotización de flete — Nexo · Botero Soto (Soluciones Logísticas)";
        ws.Range(f, 1, f, 7).Merge();
        ws.Cell(f, 1).Style.Font.SetBold().Font.SetFontSize(14);
        f += 2;
        void Dato(string k, string v) { ws.Cell(f, 1).Value = k; ws.Cell(f, 1).Style.Font.SetBold(); ws.Cell(f, 2).Value = v; f++; }
        Dato("clsCliente", c.clsSolicitud?.clsCliente?.Nombre ?? "—");
        Dato("NIT", c.clsSolicitud?.clsCliente?.Nit ?? "—");
        Dato("Estado", c.Estado.ToString());
        Dato("Resultado", c.Resultado.ToString());
        Dato("Generado", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm"));
        f++;

        ws.Cell(f, 1).Value = $"FLETES ({c.Lineas.Count + lineasRechazadas.Count})";
        ws.Cell(f, 1).Style.Font.SetBold();
        f++;
        // S4-HU-17.4/#230: Fuente, Aprovisionamiento y Retorno contenedor se quitan del
        // Excel (pedido de frontend, administracion_embarqa_nexo#206). "Flete" queda como
        // única columna monetaria de la línea, con el TOTAL completo (lo que antes calculaba
        // "Total línea": ValorFlete + Aprovisionamiento + Retorno contenedor) — no ValorFlete
        // aislado, que dejaría el número incompleto.
        EscribirEncabezado(ws, f, new[] { "clsRuta", "Tipología", "Flete" });
        f++;
        decimal totalFletes = 0;
        foreach (var l in c.Lineas)
        {
            var totalLinea = l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0);
            totalFletes += totalLinea;
            ws.Cell(f, 1).Value = l.clsLineaSolicitud?.clsRuta is { } r ? $"{r.Origen} → {r.Destino}" : "—";
            ws.Cell(f, 2).Value = l.clsLineaSolicitud?.Tipologia?.ToString() ?? "—";
            ws.Cell(f, 3).Value = totalLinea;
            ws.Cell(f, 3).Style.NumberFormat.Format = FormatoMoneda;
            f++;
        }
        // S6-HU-24.3/#320: las líneas rechazadas salen como «Sin costeo» — sin precio y sin motivo — y NO suman al total.
        foreach (var r in lineasRechazadas)
        {
            ws.Cell(f, 1).Value = r.Ruta;
            ws.Cell(f, 2).Value = r.Tipologia ?? "—";
            ws.Cell(f, 3).Value = SinCosteo;
            ws.Cell(f, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(f, 3).Style.Font.SetItalic();
            f++;
        }
        f++;

        var totalLog = EscribirServicios(ws, ref f, "SERVICIOS LOGÍSTICOS", logisticos, Nombre);
        var totalAdic = EscribirServicios(ws, ref f, "SERVICIOS ADICIONALES", adicionales, Nombre);

        ws.Cell(f, 1).Value = "CONSOLIDADO";
        ws.Cell(f, 1).Style.Font.SetBold();
        f++;
        void Total(string k, decimal v, bool fuerte = false)
        {
            ws.Cell(f, 1).Value = k;
            ws.Cell(f, 3).Value = v;
            ws.Cell(f, 3).Style.NumberFormat.Format = FormatoMoneda;
            if (fuerte) ws.Range(f, 1, f, 3).Style.Font.SetBold();
            f++;
        }
        Total("Subtotal fletes", totalFletes);
        Total("Subtotal servicios logísticos", totalLog);
        Total("Subtotal servicios adicionales", totalAdic);
        Total("TOTAL COTIZACIÓN", totalFletes + totalLog + totalAdic, fuerte: true);

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"cotizacion-{c.Id.ToString()[..8]}.xlsx");
    }

    private static void EscribirEncabezado(IXLWorksheet ws, int fila, string[] columnas)
    {
        for (var i = 0; i < columnas.Length; i++) ws.Cell(fila, i + 1).Value = columnas[i];
        ws.Range(fila, 1, fila, columnas.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));
    }

    private static decimal EscribirServicios(
        IXLWorksheet ws, ref int f, string titulo, List<clsCotizacionConexo> items, Func<clsCotizacionConexo, string> nombre)
    {
        ws.Cell(f, 1).Value = $"{titulo} ({items.Count})";
        ws.Cell(f, 1).Style.Font.SetBold();
        f++;
        EscribirEncabezado(ws, f, new[] { "Servicio", "Cantidad", "Valor" });
        f++;
        decimal subtotal = 0;
        if (items.Count == 0)
        {
            ws.Cell(f, 1).Value = "Sin servicios";
            f++;
        }
        else
        {
            foreach (var x in items)
            {
                ws.Cell(f, 1).Value = nombre(x);
                ws.Cell(f, 2).Value = x.Cantidad;
                // Lo que se muestra es el valor de venta: el costo del servicio es solo para el personal interno.
                ws.Cell(f, 3).Value = x.ValorVenta;
                ws.Cell(f, 3).Style.NumberFormat.Format = FormatoMoneda;
                subtotal += x.ValorVenta;
                f++;
            }
        }
        f++;
        return subtotal;
    }
}
