using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record clsListarImportacionesSicetacQuery : IQuery<IReadOnlyList<clsImportacionDto>>;

public sealed class clsListarImportacionesSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<clsListarImportacionesSicetacQuery, IReadOnlyList<clsImportacionDto>>
{
    public Task<IReadOnlyList<clsImportacionDto>> Handle(clsListarImportacionesSicetacQuery query, CancellationToken ct = default) =>
        repo.ListarImportacionesAsync(ct);
}
