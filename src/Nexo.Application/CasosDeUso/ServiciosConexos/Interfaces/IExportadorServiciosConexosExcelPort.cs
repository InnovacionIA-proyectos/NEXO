using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;

namespace Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;

/// <summary>Exporta el catálogo completo a Excel — mismas columnas que lee
/// <see cref="IServiciosConexosRepository.ImportarExcelAsync"/>, así el mismo archivo
/// sirve para editar y reimportar como actualización (S4-HU-15.3/#249).</summary>
public interface IExportadorServiciosConexosExcelPort
{
    (byte[] Contenido, string NombreArchivo) Generar(IReadOnlyList<clsServicioConexoDto> servicios);
}
