using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

public sealed record ListarReglasSoloSicetacQuery() : IQuery<IReadOnlyList<ReglaSoloSicetacDto>>;

public sealed class ListarReglasSoloSicetacQueryHandler(IReglasSoloSicetacRepository reglas)
    : IQueryHandler<ListarReglasSoloSicetacQuery, IReadOnlyList<ReglaSoloSicetacDto>>
{
    public async Task<IReadOnlyList<ReglaSoloSicetacDto>> Handle(ListarReglasSoloSicetacQuery query, CancellationToken ct = default)
    {
        var lista = await reglas.ListarAsync(ct);
        return lista
            .OrderBy(r => r.Origen).ThenBy(r => r.Destino).ThenBy(r => r.TipoVehiculo)
            .Select(r => new ReglaSoloSicetacDto(r.Id, r.Origen, r.Destino, r.TipoVehiculo, r.CreatedAt))
            .ToList();
    }
}
