using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.DTOs;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;

/// <summary>
/// Lee un Excel de carga masiva (columnas Origen, Destino, Tipo de vehículo, con o
/// sin encabezado) y devuelve las filas SIN normalizar ni deduplicar — eso es
/// responsabilidad del Command (regla de negocio), no del parser.
/// </summary>
public interface IExcelReglasSoloSicetacPort
{
    IReadOnlyList<FilaReglaSoloSicetacDto> Leer(Stream archivo);
}
