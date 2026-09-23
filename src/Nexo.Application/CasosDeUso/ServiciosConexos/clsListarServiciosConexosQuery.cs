using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

public sealed record clsListarServiciosConexosQuery(
    bool SoloActivos, string? Q, string? Categoria, int Page, int PageSize) : IQuery<clsPaginaServiciosDto>;

public sealed class clsListarServiciosConexosQueryHandler(IServiciosConexosRepository repo)
    : IQueryHandler<clsListarServiciosConexosQuery, clsPaginaServiciosDto>
{
    public Task<clsPaginaServiciosDto> Handle(clsListarServiciosConexosQuery query, CancellationToken ct = default) =>
        repo.ListarAsync(query.SoloActivos, query.Q, query.Categoria, query.Page, query.PageSize, ct);
}
