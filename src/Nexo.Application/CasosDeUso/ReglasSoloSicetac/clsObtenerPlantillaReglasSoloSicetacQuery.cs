using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Plantilla Excel descargable para la carga masiva de reglas "solo SICE-TAC".</summary>
public sealed record clsObtenerPlantillaReglasSoloSicetacQuery : IQuery<clsArchivoDto>;

public sealed class clsObtenerPlantillaReglasSoloSicetacQueryHandler(IPlantillaReglasSoloSicetacPort plantilla)
    : IQueryHandler<clsObtenerPlantillaReglasSoloSicetacQuery, clsArchivoDto>
{
    public Task<clsArchivoDto> Handle(clsObtenerPlantillaReglasSoloSicetacQuery query, CancellationToken ct = default)
    {
        var (contenido, nombre) = plantilla.Generar();
        return Task.FromResult(new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }
}
