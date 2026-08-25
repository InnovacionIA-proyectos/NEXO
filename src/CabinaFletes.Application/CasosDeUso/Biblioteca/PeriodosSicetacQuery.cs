using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca;

public sealed record PeriodosSicetacQuery() : IQuery<IReadOnlyList<int>>;

public sealed class PeriodosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<PeriodosSicetacQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(PeriodosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.PeriodosAsync(ct);
}
