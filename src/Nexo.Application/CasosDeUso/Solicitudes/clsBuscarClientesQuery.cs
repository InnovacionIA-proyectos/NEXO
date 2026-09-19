using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Búsqueda de clientes para el formulario de captura. Con el DW configurado busca
/// sobre los clientes REALES (DimBPSolicitante) por nombre, NIT o BP; sin él, sobre
/// la tabla local.
/// </summary>
public sealed record clsBuscarClientesQuery(string? Q) : IQuery<IReadOnlyList<clsClienteBusquedaDto>>;

public sealed class clsBuscarClientesQueryHandler(IClientesDwPort clientesDw, IClientesPort clientes)
    : IQueryHandler<clsBuscarClientesQuery, IReadOnlyList<clsClienteBusquedaDto>>
{
    private const int Top = 30;

    public async Task<IReadOnlyList<clsClienteBusquedaDto>> Handle(clsBuscarClientesQuery query, CancellationToken ct = default)
    {
        var termino = Normalizar(query.Q ?? string.Empty);

        if (clientesDw.Configurado)
        {
            var catalogo = await clientesDw.ListarAsync(ct);
            var filtrados = string.IsNullOrEmpty(termino)
                ? catalogo
                : catalogo.Where(c => Normalizar(c.Nombre).Contains(termino) || c.Nit.Contains(termino) || c.Bp.Contains(termino));
            return filtrados
                .OrderBy(c => c.Nombre)
                .Take(Top)
                .Select(c => new clsClienteBusquedaDto(null, c.Bp, c.Nit, c.Nombre))
                .ToList();
        }

        var locales = await clientes.BuscarAsync(termino, Top, ct);
        return locales.Select(c => new clsClienteBusquedaDto(c.Id, null, c.Nit, c.Nombre)).ToList();
    }

    private static string Normalizar(string s) => s.Trim().ToUpperInvariant();
}
