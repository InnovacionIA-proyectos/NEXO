using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/vehiculos — catálogo completo desde la
/// proyección local (issue #111), sin llamar a Cubicador.</summary>
public sealed record ListarVehiculosQuery : IQuery<IReadOnlyList<VehiculoCubicacionDto>>;

public sealed class ListarVehiculosQueryHandler(IVehiculoCubicacionRepository vehiculos)
    : IQueryHandler<ListarVehiculosQuery, IReadOnlyList<VehiculoCubicacionDto>>
{
    public async Task<IReadOnlyList<VehiculoCubicacionDto>> Handle(ListarVehiculosQuery query, CancellationToken ct = default) =>
        (await vehiculos.ListarAsync(ct)).Select(ResultadoPlanMapper.AVehiculo).ToList();
}
