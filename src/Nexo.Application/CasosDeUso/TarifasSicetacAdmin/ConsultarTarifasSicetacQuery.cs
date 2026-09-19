using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ConsultarTarifasSicetacQuery(SicetacFiltro Filtro) : IQuery<IReadOnlyList<TarifaSicetacDto>>;

public sealed class ConsultarTarifasSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>>
{
    public Task<IReadOnlyList<TarifaSicetacDto>> Handle(ConsultarTarifasSicetacQuery query, CancellationToken ct = default) =>
        repo.ConsultarAsync(query.Filtro, ct);
}
