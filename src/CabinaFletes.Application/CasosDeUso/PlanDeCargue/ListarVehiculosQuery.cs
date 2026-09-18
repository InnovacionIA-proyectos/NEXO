using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/vehiculos — catálogo desde la
/// proyección local (issue #111), sin llamar a Cubicador. Issue #166: pagina
/// (page 1-based, pageSize) aunque el catálogo hoy sea chico (~15 filas) — el
/// contrato queda consistente con el resto de los endpoints de listado.</summary>
public sealed record ListarVehiculosQuery(int Page = 1, int PageSize = 50) : IQuery<PaginaDto<VehiculoCubicacionDto>>;

public sealed class ListarVehiculosQueryHandler(IVehiculoCubicacionRepository vehiculos)
    : IQueryHandler<ListarVehiculosQuery, PaginaDto<VehiculoCubicacionDto>>
{
    public async Task<PaginaDto<VehiculoCubicacionDto>> Handle(ListarVehiculosQuery query, CancellationToken ct = default)
    {
        var (items, total) = await vehiculos.ListarAsync(query.Page, query.PageSize, ct);
        return new PaginaDto<VehiculoCubicacionDto>(items.Select(ResultadoPlanMapper.AVehiculo).ToList(), total);
    }
}
