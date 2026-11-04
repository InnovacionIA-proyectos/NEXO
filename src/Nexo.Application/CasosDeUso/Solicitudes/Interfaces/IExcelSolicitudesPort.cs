using Nexo.Application.CasosDeUso.Solicitudes.DTOs;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Análisis tolerante de Excel de licitación: detecta encabezados y filas sin asumir plantilla fija. No persiste nada.</summary>
public interface IExcelSolicitudesPort
{
    Task<clsAnalisisExcelDto> AnalizarAsync(Stream archivo, string nombreArchivo, CancellationToken ct);
}
