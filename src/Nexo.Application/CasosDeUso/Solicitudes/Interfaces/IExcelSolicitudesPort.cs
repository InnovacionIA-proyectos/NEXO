using Nexo.Application.CasosDeUso.Solicitudes.DTOs;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>
/// Análisis tolerante de Excel de licitación: detecta encabezados y filas de datos
/// sin asumir una plantilla fija (cada cliente envía la suya). No persiste nada.
/// </summary>
public interface IExcelSolicitudesPort
{
    Task<AnalisisExcelDto> AnalizarAsync(Stream archivo, string nombreArchivo, CancellationToken ct);
}
