using CabinaFletes.Application.Biblioteca.Ports;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.Biblioteca.Queries;

public sealed record PeriodosSicetacQuery() : IQuery<IReadOnlyList<int>>;

public sealed class PeriodosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<PeriodosSicetacQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(PeriodosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.PeriodosAsync(ct);
}
