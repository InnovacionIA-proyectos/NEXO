using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Exceptions;
using ClosedXML.Excel;

namespace Nexo.Infrastructure.ServiciosConexos.Adaptadores;

/// <summary>
/// Plantilla de Excel descargable por tipo de servicio conexo (S4-HU-15.3/#249,
/// corregida en S5-HU-19/#261). El importador
/// (<see cref="Repositorios.clsEfServiciosConexosRepository.ImportarExcelAsync"/>) sigue
/// siendo genérico y tolerante — acepta columnas ausentes (las dimensiones que faltan
/// caen a "NO APLICA") — así que cada plantilla trae SOLO las columnas que ese tipo
/// necesita, sin forzar a incluir las que nunca aplican.
///
/// Corrección de #261: la primera versión (#249) traía las 12 columnas en las 4
/// plantillas (QA: "el criterio pedía que cada una trajera solo sus columnas") y una fila
/// de ejemplo con valores de apariencia real que se cargaban tal cual si alguien subía la
/// plantilla sin editar. Ahora: sin fila de datos cargable — la única fila 2 que se
/// entrega pre-llena SOLO las columnas verdaderamente FIJAS de ese tipo (código/tipo de
/// servicio cuando son únicos del tipo, unidad, categoría, activo), dejando en blanco
/// las que varían fila a fila (origen, destino, puerto, ruta, costo, vigencia) — así,
/// subida sin tocar, esa fila nunca crea un registro real: o queda vacía (Precinto/
/// Acompañamiento, donde código/tipo también varían) o queda inválida por falta de costo
/// (Almacenamiento/ITR), nunca "creada".
/// </summary>
public sealed class clsPlantillaServicioConexoAdapter : IPlantillaServicioConexoPort
{
    public (byte[] Contenido, string NombreArchivo) Generar(string tipo)
    {
        var (nombreHoja, encabezados, fijos) = tipo.Trim().ToUpperInvariant() switch
        {
            "ALMACENAMIENTO" => ("Almacenamiento",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "Almacenamiento", "Almacenamiento", "", "", "", "Por estiba", "logistico", "SI" }),

            "ITR" => ("ITR",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "PUERTO", "TIPO MAQUINA", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "ITR", "ITR", "", "", "", "", "", "Por estiba", "logistico", "SI" }),

            "PRECINTO" => ("Precinto - Barra Satelital",
                new[] { "CODIGO", "TIPO SERVICIO", "RUTA", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "", "", "", "", "", "Por unidad", "adicional", "SI" }),

            "ACOMPANAMIENTO" => ("Acompañamiento Vehicular",
                new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" },
                new[] { "", "", "", "", "", "", "Por unidad", "adicional", "SI" }),

            _ => throw new clsValidationFailedException("TIPO_PLANTILLA_INVALIDO",
                "El tipo debe ser 'almacenamiento', 'itr', 'precinto' o 'acompanamiento'."),
        };

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(nombreHoja);

        for (var i = 0; i < encabezados.Length; i++) ws.Cell(1, i + 1).Value = encabezados[i];
        ws.Range(1, 1, 1, encabezados.Length).Style.Font.SetBold()
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF7"));

        // Fila 2: solo las columnas verdaderamente fijas de este tipo (ver doc de clase) —
        // nunca un dato variable de apariencia real.
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
