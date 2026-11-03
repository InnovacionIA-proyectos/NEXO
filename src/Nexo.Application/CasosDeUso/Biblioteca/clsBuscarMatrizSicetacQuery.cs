using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public sealed record clsBuscarMatrizSicetacQuery(
    string? Q, int? Periodo, string? Configuracion, string? TipoCarga, string? UnidadTransporte,
    string? CondicionCarga, string? ViaEstandar, int Top, int Page = 1) : IQuery<clsMatrizSicetacResultDto>;

public sealed class clsBuscarMatrizSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<clsBuscarMatrizSicetacQuery, clsMatrizSicetacResultDto>
{
    public Task<clsMatrizSicetacResultDto> Handle(clsBuscarMatrizSicetacQuery query, CancellationToken ct = default) =>
        sicetac.BuscarAsync(query.Q, query.Periodo, query.Configuracion, query.TipoCarga,
            query.UnidadTransporte, query.CondicionCarga, query.ViaEstandar, query.Top, query.Page, ct);
}
