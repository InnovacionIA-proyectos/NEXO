using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;

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
