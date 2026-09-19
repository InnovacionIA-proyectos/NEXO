using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public sealed record PeriodosSicetacQuery() : IQuery<IReadOnlyList<int>>;

public sealed class PeriodosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<PeriodosSicetacQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(PeriodosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.PeriodosAsync(ct);
}
