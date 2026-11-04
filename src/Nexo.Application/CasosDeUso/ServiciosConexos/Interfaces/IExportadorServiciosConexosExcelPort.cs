using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;

/// <summary>Exporta el catálogo a Excel con las mismas columnas que lee <see cref="IServiciosConexosRepository.ImportarExcelAsync"/>, para editar y reimportar.</summary>
public interface IExportadorServiciosConexosExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsServicioConexoDto> servicios);
}
