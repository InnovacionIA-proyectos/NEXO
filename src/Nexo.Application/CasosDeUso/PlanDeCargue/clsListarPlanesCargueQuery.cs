using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>GET /api/planes-cargue — listado contra la proyección local. Resumen, no
/// detalle — mismo shape que ya usa la consulta de respaldo de un solo plan. Pagina
/// (reemplaza el tope fijo de 100, que ya no alcanzaba a mostrar planes viejos, por
/// page/pageSize real).</summary>
public sealed record clsListarPlanesCargueQuery(int Page = 1, int PageSize = 50) : IQuery<clsPaginaDto<clsPlanCargueDto>>;

public sealed class clsListarPlanesCargueQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsListarPlanesCargueQuery, clsPaginaDto<clsPlanCargueDto>>
{
    public async Task<clsPaginaDto<clsPlanCargueDto>> Handle(clsListarPlanesCargueQuery query, CancellationToken ct = default)
    {
        var (recientes, total) = await planes.ListarRecientesAsync(query.Page, query.PageSize, ct);
        var items = recientes.Select(p => new clsPlanCargueDto(
            p.Id, p.Nombre, p.Estado, p.Estrategia, p.Algoritmo, p.Puntaje,
            p.PesoTotalKg, p.VolumenUtilizadoM3, p.UtilizacionVolumen, p.UtilizacionPiso,
            p.Items.Count, p.Colocaciones.Count, p.UpdatedAt)).ToList();
        return new clsPaginaDto<clsPlanCargueDto>(items, total);
    }
}
