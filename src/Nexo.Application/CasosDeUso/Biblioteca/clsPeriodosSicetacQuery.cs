using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public sealed record clsPeriodosSicetacQuery() : IQuery<IReadOnlyList<int>>;

public sealed class clsPeriodosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<clsPeriodosSicetacQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(clsPeriodosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.PeriodosAsync(ct);
}
