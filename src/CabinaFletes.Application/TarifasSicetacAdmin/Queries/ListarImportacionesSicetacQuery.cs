using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasSicetacAdmin.Dtos;
using CabinaFletes.Application.TarifasSicetacAdmin.Ports;

namespace CabinaFletes.Application.TarifasSicetacAdmin.Queries;

public sealed record ListarImportacionesSicetacQuery : IQuery<IReadOnlyList<ImportacionDto>>;

public sealed class ListarImportacionesSicetacQueryHandler(ITarifasSicetacRepository repo)
    : IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>>
{
    public Task<IReadOnlyList<ImportacionDto>> Handle(ListarImportacionesSicetacQuery query, CancellationToken ct = default) =>
        repo.ListarImportacionesAsync(ct);
}
