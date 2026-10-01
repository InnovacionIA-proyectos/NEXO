using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>Plantilla Excel descargable por tipo de servicio (S4-HU-15.3/#249):
/// "almacenamiento" | "itr" | "precinto" | "acompanamiento".</summary>
public sealed record clsObtenerPlantillaServicioConexoQuery(string Tipo) : IQuery<clsArchivoDto>;

public sealed class clsObtenerPlantillaServicioConexoQueryHandler(IPlantillaServicioConexoPort plantilla)
    : IQueryHandler<clsObtenerPlantillaServicioConexoQuery, clsArchivoDto>
{
    public Task<clsArchivoDto> Handle(clsObtenerPlantillaServicioConexoQuery query, CancellationToken ct = default)
    {
        var (contenido, nombre) = plantilla.Generar(query.Tipo);
        return Task.FromResult(new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }
}
