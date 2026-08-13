using CabinaFletes.Application.Solicitudes.Dtos;

namespace CabinaFletes.Application.Solicitudes.Ports;

/// <summary>
/// Análisis tolerante de Excel de licitación: detecta encabezados y filas de datos
/// sin asumir una plantilla fija (cada cliente envía la suya). No persiste nada.
/// </summary>
public interface IExcelSolicitudesPort
{
    Task<AnalisisExcelDto> AnalizarAsync(Stream archivo, string nombreArchivo, CancellationToken ct);
}
