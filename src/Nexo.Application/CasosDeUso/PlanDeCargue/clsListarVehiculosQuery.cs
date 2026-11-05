using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>GET /api/vehiculos — catálogo paginado (page 1-based, pageSize) desde la proyección local, sin llamar a Cubicador.</summary>
public sealed record clsListarVehiculosQuery(int Page = 1, int PageSize = 50) : IQuery<clsPaginaDto<clsVehiculoCubicacionDto>>;

public sealed class clsListarVehiculosQueryHandler(IVehiculoCubicacionRepository vehiculos)
    : IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>>
{
    public async Task<clsPaginaDto<clsVehiculoCubicacionDto>> Handle(clsListarVehiculosQuery query, CancellationToken ct = default)
    {
        var (items, total) = await vehiculos.ListarAsync(query.Page, query.PageSize, ct);
        return new clsPaginaDto<clsVehiculoCubicacionDto>(items.Select(clsResultadoPlanMapper.AVehiculo).ToList(), total);
    }
}
