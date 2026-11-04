using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Exceptions;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ServiciosConexos.Adaptadores;

/// <summary>Plantilla de Excel por tipo de servicio conexo, con SOLO las columnas que ese tipo necesita. La fila 2 prellena solo
/// columnas fijas del tipo y deja en blanco las variables: subida sin editar nunca crea un registro real.</summary>
public sealed class clsPlantillaServicioConexoAdapter : IPlantillaServicioConexoPort
{
    public (byte[] Contenido, string NombreArchivo) Generar(string tipo)
    {
        var (nombreHoja, encabezados, fijos) = tipo.Trim().ToUpperInvariant() switch
        {
            "ALMACENAMIENTO" => ("Almacenamiento",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "Almacenamiento", "Almacenamiento", "", "", "", "", "Por estiba", "logistico", "SI" }),

            "ITR" => ("ITR",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "PUERTO", "TIPO MAQUINA", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "ITR", "ITR", "", "", "", "", "", "", "Por estiba", "logistico", "SI" }),

            "PRECINTO" => ("Precinto - Barra Satelital",
                new[] { "CODIGO", "TIPO SERVICIO", "RUTA", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "", "", "", "", "", "", "Por unidad", "adicional", "SI" }),

            "ACOMPANAMIENTO" => ("Acompañamiento Vehicular",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "", "", "", "", "", "", "", "Por unidad", "adicional", "SI" }),

            _ => throw new clsValidationFailedException("TIPO_PLANTILLA_INVALIDO",
                "El tipo debe ser 'almacenamiento', 'itr', 'precinto' o 'acompanamiento'."),
        };

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(nombreHoja);

        for (var i = 0; i < encabezados.Length; i++) ws.Cell(1, i + 1).Value = encabezados[i];
        ws.Range(1, 1, 1, encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        // Fila 2: solo columnas fijas del tipo, nunca un dato variable de apariencia real.
        for (var i = 0; i < fijos.Length; i++)
            if (fijos[i].Length > 0) ws.Cell(2, i + 1).Value = fijos[i];

        ws.Columns().AdjustToContents();

        return (GuardarComoBytes(wb), $"plantilla-servicios-conexos-{tipo.Trim().ToLowerInvariant()}.xlsx");
    }

    private static byte[] GuardarComoBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
