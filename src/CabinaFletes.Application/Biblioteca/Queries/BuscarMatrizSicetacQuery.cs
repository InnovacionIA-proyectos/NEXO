using CabinaFletes.Application.Biblioteca.Dtos;
using CabinaFletes.Application.Biblioteca.Ports;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Biblioteca.Queries;

public sealed record BuscarMatrizSicetacQuery(
    string? Q, int? Periodo, string? Configuracion, string? TipoCarga, string? UnidadTransporte,
    string? CondicionCarga, string? ViaEstandar, int Top) : IQuery<MatrizSicetacResultDto>;

public sealed class BuscarMatrizSicetacQueryHandler(IBibliotecaSicetacPort sicetac)
    : IQueryHandler<BuscarMatrizSicetacQuery, MatrizSicetacResultDto>
{
    public Task<MatrizSicetacResultDto> Handle(BuscarMatrizSicetacQuery query, CancellationToken ct = default) =>
        sicetac.BuscarAsync(query.Q, query.Periodo, query.Configuracion, query.TipoCarga,
            query.UnidadTransporte, query.CondicionCarga, query.ViaEstandar, query.Top, ct);
}
