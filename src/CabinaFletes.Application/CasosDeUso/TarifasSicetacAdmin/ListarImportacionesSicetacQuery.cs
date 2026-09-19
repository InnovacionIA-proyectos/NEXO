using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin;

public sealed record ListarImportacionesSicetacQuery : IQuery<IReadOnlyList<ImportacionDto>>;

public sealed class ListarImportacionesSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>>
{
    public Task<IReadOnlyList<ImportacionDto>> Handle(ListarImportacionesSicetacQuery query, CancellationToken ct = default) =>
        repo.ListarImportacionesAsync(ct);
}
