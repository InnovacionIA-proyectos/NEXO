using CabinaFletes.Application.Biblioteca.Dtos;
using CabinaFletes.Application.Biblioteca.Ports;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.Biblioteca.Queries;

public sealed record FiltrosSicetacQuery() : IQuery<SicetacFiltrosDto>;

public sealed class FiltrosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<FiltrosSicetacQuery, SicetacFiltrosDto>
{
    public Task<SicetacFiltrosDto> Handle(FiltrosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.FiltrosAsync(ct);
}
