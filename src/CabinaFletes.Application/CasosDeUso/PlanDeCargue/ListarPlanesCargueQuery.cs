using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue — listado (los 100 más
/// recientes, mismo tope que usa Cubicador en su propio `listar_recientes`) contra la
/// proyección local. Resumen, no detalle — mismo shape que ya usa la consulta de
/// respaldo de un solo plan (S2-HU-08.1).</summary>
public sealed record ListarPlanesCargueQuery : IQuery<IReadOnlyList<PlanCargueDto>>;

public sealed class ListarPlanesCargueQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ListarPlanesCargueQuery, IReadOnlyList<PlanCargueDto>>
{
    public async Task<IReadOnlyList<PlanCargueDto>> Handle(ListarPlanesCargueQuery query, CancellationToken ct = default)
    {
        var recientes = await planes.ListarRecientesAsync(100, ct);
        return recientes.Select(p => new PlanCargueDto(
            p.Id, p.Nombre, p.Estado, p.Estrategia, p.Algoritmo, p.Puntaje,
            p.PesoTotalKg, p.VolumenUtilizadoM3, p.UtilizacionVolumen, p.UtilizacionPiso,
            p.Items.Count, p.Colocaciones.Count, p.UpdatedAt)).ToList();
    }
}
