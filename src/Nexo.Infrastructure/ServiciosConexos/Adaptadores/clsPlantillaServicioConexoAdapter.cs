using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Exceptions;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ServiciosConexos.Adaptadores;

/// <summary>
/// Plantilla de Excel descargable por tipo de servicio conexo (S4-HU-15.3/#249). El
/// importador (<see cref="Repositorios.clsEfServiciosConexosRepository.ImportarExcelAsync"/>)
/// sigue siendo genérico y tolerante — cada plantilla trae las MISMAS 12 columnas en el
/// mismo orden que el export, con las columnas que no aplican a ese tipo (dimensiones NO
/// APLICA, Unidad fija, Categoria) ya rellenadas en la fila de ejemplo para que el usuario
/// solo tenga que llenar las que sí le piden, sin arriesgarse a que falte alguna columna
/// obligatoria o a que la fila caiga en la categoría equivocada.
/// </summary>
public sealed class clsPlantillaServicioConexoAdapter : IPlantillaServicioConexoPort
{
    private static readonly string[] Encabezados =
    [
        "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "PUERTO", "RUTA", "TIPO MAQUINA",
        "COSTO UNITARIO", "UNIDAD", "VIGENCIA", "CATEGORIA", "ACTIVO",
    ];

    private const string NoAplica = "NO APLICA";

    public (byte[] Contenido, string NombreArchivo) Generar(string tipo)
    {
        var (nombreHoja, ejemplo) = tipo.Trim().ToUpperInvariant() switch
        {
            "ALMACENAMIENTO" => ("Almacenamiento", new[]
            {
                "Almacenamiento", "Almacenamiento", "Bogotá", NoAplica, NoAplica, NoAplica, NoAplica,
                "50000", "Por estiba", "2026-12-31", "logistico", "SI",
            }),
            "ITR" => ("ITR", new[]
            {
                "ITR", "ITR", "Cartagena", NoAplica, "Puerto Cartagena", NoAplica, "Reach Stacker",
                "180000", "Por estiba", "2026-12-31", "logistico", "SI",
            }),
            "PRECINTO" => ("Precinto - Barra Satelital", new[]
            {
                "PRECINTO-SAT", "Precinto / Barra Satelital", NoAplica, NoAplica, NoAplica, "Bogotá - Cartagena", NoAplica,
                "90000", "Por unidad", "2026-12-31", "adicional", "SI",
            }),
            "ACOMPANAMIENTO" => ("Acompañamiento Vehicular", new[]
            {
                "ACOMP-VEH", "Acompañamiento vehicular", "Bogotá", "Cartagena", NoAplica, NoAplica, NoAplica,
                "350000", "Por unidad", "2026-12-31", "adicional", "SI",
            }),
            _ => throw new clsValidationFailedException("TIPO_PLANTILLA_INVALIDO",
                "El tipo debe ser 'almacenamiento', 'itr', 'precinto' o 'acompanamiento'."),
        };

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(nombreHoja);

        for (var i = 0; i < Encabezados.Length; i++) ws.Cell(1, i + 1).Value = Encabezados[i];
        ws.Range(1, 1, 1, Encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        for (var i = 0; i < ejemplo.Length; i++) ws.Cell(2, i + 1).Value = ejemplo[i];
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
