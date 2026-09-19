using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record clsConsultarTarifasSicetacQuery(clsSicetacFiltro Filtro) : IQuery<IReadOnlyList<clsTarifaSicetacDto>>;

public sealed class clsConsultarTarifasSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<clsConsultarTarifasSicetacQuery, IReadOnlyList<clsTarifaSicetacDto>>
{
    public Task<IReadOnlyList<clsTarifaSicetacDto>> Handle(clsConsultarTarifasSicetacQuery query, CancellationToken ct = default) =>
        repo.ConsultarAsync(query.Filtro, ct);
}
