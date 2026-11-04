using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;

/// <summary>Genera el Excel de descarga masiva con las mismas columnas legibles que espera el importador, así que sirve de plantilla.</summary>
public interface IExportadorTarifasPlazaExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsTarifaPlaza> tarifas);
}
