using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>
/// Genera el Excel de descarga de las reglas "solo SICE-TAC" ya cargadas (S5-HU-22.1/#288), con
/// EXACTAMENTE las columnas que acepta la carga masiva (ORIGEN, DESTINO, TIPO VEHICULO) y los
/// valores tal como están guardados, para que el archivo se edite y se reimporte sin conversión.
/// </summary>
public interface IExportadorReglasSoloSicetacExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsReglaSoloSicetac> reglas);
}
