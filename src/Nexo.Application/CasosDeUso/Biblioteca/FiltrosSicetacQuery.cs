using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public sealed record FiltrosSicetacQuery() : IQuery<SicetacFiltrosDto>;

public sealed class FiltrosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<FiltrosSicetacQuery, SicetacFiltrosDto>
{
    public Task<SicetacFiltrosDto> Handle(FiltrosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.FiltrosAsync(ct);
}
