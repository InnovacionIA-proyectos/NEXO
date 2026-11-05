using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>Lee un Excel de carga masiva (Origen, Destino, Tipo de vehículo; con o sin encabezado) y devuelve las filas SIN normalizar ni deduplicar: eso es del Command.</summary>
public interface IExcelReglasSoloSicetacPort
{
    IReadOnlyList<clsFilaReglaSoloSicetacDto> Leer(Stream archivo);
}
