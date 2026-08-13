using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.ServiciosConexos.Dtos;
using CabinaFletes.Application.ServiciosConexos.Ports;

namespace CabinaFletes.Application.ServiciosConexos.Queries;

public sealed record ListarServiciosConexosQuery(
    bool SoloActivos, string? Q, string? Categoria, int Page, int PageSize) : IQuery<PaginaServiciosDto>;

public sealed class ListarServiciosConexosQueryHandler(IServiciosConexosRepository repo)
    : IQueryHandler<ListarServiciosConexosQuery, PaginaServiciosDto>
{
    public Task<PaginaServiciosDto> Handle(ListarServiciosConexosQuery query, CancellationToken ct = default) =>
        repo.ListarAsync(query.SoloActivos, query.Q, query.Categoria, query.Page, query.PageSize, ct);
}
