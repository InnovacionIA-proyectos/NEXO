using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Consulta de respaldo del contrato (S2-HU-08.1): reconstruye estado/resultado sin
/// haber recibido ningún aviso por SignalR — la conexión se cae, el navegador se
/// duerme, y la verdad sigue estando acá.
/// </summary>
public sealed record ObtenerPlanCargueQuery(Guid PlanCargueId) : IQuery<PlanCargueDto>;

public sealed class ObtenerPlanCargueQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerPlanCargueQuery, PlanCargueDto>
{
    public async Task<PlanCargueDto> Handle(ObtenerPlanCargueQuery query, CancellationToken ct = default)
    {
        var p = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return new PlanCargueDto(
            p.Id, p.Nombre, p.Estado, p.Estrategia, p.Algoritmo, p.Puntaje,
            p.PesoTotalKg, p.VolumenUtilizadoM3, p.UtilizacionVolumen, p.UtilizacionPiso,
            p.Items.Count, p.Colocaciones.Count, p.UpdatedAt);
    }
}
