using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public sealed record clsFiltrosSicetacQuery() : IQuery<clsSicetacFiltrosDto>;

public sealed class clsFiltrosSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<clsFiltrosSicetacQuery, clsSicetacFiltrosDto>
{
    public Task<clsSicetacFiltrosDto> Handle(clsFiltrosSicetacQuery query, CancellationToken ct = default) =>
        sicetac.FiltrosAsync(ct);
}
