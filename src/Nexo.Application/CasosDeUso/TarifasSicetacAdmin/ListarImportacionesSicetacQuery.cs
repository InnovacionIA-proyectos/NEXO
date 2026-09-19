using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ListarImportacionesSicetacQuery : IQuery<IReadOnlyList<ImportacionDto>>;

public sealed class ListarImportacionesSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>>
{
    public Task<IReadOnlyList<ImportacionDto>> Handle(ListarImportacionesSicetacQuery query, CancellationToken ct = default) =>
        repo.ListarImportacionesAsync(ct);
}
