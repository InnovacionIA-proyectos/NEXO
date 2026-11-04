using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>Genera el Excel de las reglas cargadas con EXACTAMENTE las columnas de la carga masiva y los valores guardados, para editar y reimportar sin conversión.</summary>
public interface IExportadorReglasSoloSicetacExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsReglaSoloSicetac> reglas);
}
