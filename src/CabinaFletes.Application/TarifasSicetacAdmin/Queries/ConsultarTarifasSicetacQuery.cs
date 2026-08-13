using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasSicetacAdmin.Dtos;
using CabinaFletes.Application.TarifasSicetacAdmin.Ports;

namespace CabinaFletes.Application.TarifasSicetacAdmin.Queries;

public sealed record ConsultarTarifasSicetacQuery(SicetacFiltro Filtro) : IQuery<IReadOnlyList<TarifaSicetacDto>>;

public sealed class ConsultarTarifasSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>>
{
    public Task<IReadOnlyList<TarifaSicetacDto>> Handle(ConsultarTarifasSicetacQuery query, CancellationToken ct = default) =>
        repo.ConsultarAsync(query.Filtro, ct);
}
