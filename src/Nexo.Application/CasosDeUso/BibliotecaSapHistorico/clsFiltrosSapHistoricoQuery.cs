using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public sealed record clsFiltrosSapHistoricoQuery() : IQuery<clsSapHistoricoFiltrosDto>;

public sealed class clsFiltrosSapHistoricoQueryHandler(ISapHistoricoPort sap)
    : IQueryHandler<clsFiltrosSapHistoricoQuery, clsSapHistoricoFiltrosDto>
{
    public Task<clsSapHistoricoFiltrosDto> Handle(clsFiltrosSapHistoricoQuery query, CancellationToken ct = default) =>
        sap.FiltrosAsync(ct);
}
