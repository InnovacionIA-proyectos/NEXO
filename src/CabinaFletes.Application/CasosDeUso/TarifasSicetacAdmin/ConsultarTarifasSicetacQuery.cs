using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ConsultarTarifasSicetacQuery(SicetacFiltro Filtro) : IQuery<IReadOnlyList<TarifaSicetacDto>>;

public sealed class ConsultarTarifasSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>>
{
    public Task<IReadOnlyList<TarifaSicetacDto>> Handle(ConsultarTarifasSicetacQuery query, CancellationToken ct = default) =>
        repo.ConsultarAsync(query.Filtro, ct);
}
