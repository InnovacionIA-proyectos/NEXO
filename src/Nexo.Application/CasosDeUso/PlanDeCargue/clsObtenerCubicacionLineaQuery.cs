using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>Puente Solicitudes → Cubicador: lo que el Cubicador necesita de una línea para cubicarla, sin exponerle el resto del módulo. Lo llama el worker vía GET /api/lineas-solicitud/{id}/cubicacion.</summary>
public sealed record clsObtenerCubicacionLineaQuery(Guid Id) : IQuery<clsLineaParaCubicacionDto>;

public sealed class clsObtenerCubicacionLineaQueryHandler(ILineaSolicitudCubicacionPort lineas)
    : IQueryHandler<clsObtenerCubicacionLineaQuery, clsLineaParaCubicacionDto>
{
    public async Task<clsLineaParaCubicacionDto> Handle(clsObtenerCubicacionLineaQuery query, CancellationToken ct = default) =>
        await lineas.ObtenerAsync(query.Id, ct)
        ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "Línea de solicitud no encontrada.");
}
