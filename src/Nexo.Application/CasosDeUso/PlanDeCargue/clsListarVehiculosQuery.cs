using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/vehiculos — catálogo desde la
/// proyección local (issue #111), sin llamar a Cubicador. Issue #166: pagina
/// (page 1-based, pageSize) aunque el catálogo hoy sea chico (~15 filas) — el
/// contrato queda consistente con el resto de los endpoints de listado.</summary>
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
