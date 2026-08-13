using CabinaFletes.Application.ReglasSoloSicetac.Dtos;

namespace CabinaFletes.Application.ReglasSoloSicetac.Ports;

/// <summary>
/// Lee un Excel de carga masiva (columnas Origen, Destino, Tipo de vehículo, con o
/// sin encabezado) y devuelve las filas SIN normalizar ni deduplicar — eso es
/// responsabilidad del Command (regla de negocio), no del parser.
/// </summary>
public interface IExcelReglasSoloSicetacPort
{
    IReadOnlyList<FilaReglaSoloSicetacDto> Leer(Stream archivo);
}
