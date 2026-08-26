using CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;
using CabinaFletes.Application.CasosDeUso.ServiciosConexos.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos;

public sealed record ListarServiciosConexosQuery(
    bool SoloActivos, string? Q, string? Categoria, int Page, int PageSize) : IQuery<PaginaServiciosDto>;

public sealed class ListarServiciosConexosQueryHandler(IServiciosConexosRepository repo)
    : IQueryHandler<ListarServiciosConexosQuery, PaginaServiciosDto>
{
    public Task<PaginaServiciosDto> Handle(ListarServiciosConexosQuery query, CancellationToken ct = default) =>
        repo.ListarAsync(query.SoloActivos, query.Q, query.Categoria, query.Page, query.PageSize, ct);
}
