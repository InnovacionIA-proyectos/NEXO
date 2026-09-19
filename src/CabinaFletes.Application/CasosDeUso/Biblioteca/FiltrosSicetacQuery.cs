using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca;

public sealed record FiltrosSicetacQuery() : IQuery<SicetacFiltrosDto>;

public sealed class FiltrosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<FiltrosSicetacQuery, SicetacFiltrosDto>
{
    public Task<SicetacFiltrosDto> Handle(FiltrosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.FiltrosAsync(ct);
}
