using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;

/// <summary>Genera el Excel de descarga masiva de tarifas de plaza, con las mismas
/// columnas legibles (descripciones resueltas, no ids/códigos crudos) que espera el
/// importador al leerlo de vuelta — el mismo archivo sirve de plantilla de referencia.</summary>
public interface IExportadorTarifasPlazaExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsTarifaPlaza> tarifas);
}
