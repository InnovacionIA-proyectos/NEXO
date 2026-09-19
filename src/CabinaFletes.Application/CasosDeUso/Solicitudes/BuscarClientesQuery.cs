using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Búsqueda de clientes para el formulario de captura. Con el DW configurado busca
/// sobre los clientes REALES (DimBPSolicitante) por nombre, NIT o BP; sin él, sobre
/// la tabla local.
/// </summary>
public sealed record BuscarClientesQuery(string? Q) : IQuery<IReadOnlyList<ClienteBusquedaDto>>;

public sealed class BuscarClientesQueryHandler(IClientesDwPort clientesDw, IClientesPort clientes)
    : IQueryHandler<BuscarClientesQuery, IReadOnlyList<ClienteBusquedaDto>>
{
    private const int Top = 30;

    public async Task<IReadOnlyList<ClienteBusquedaDto>> Handle(BuscarClientesQuery query, CancellationToken ct = default)
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
                .Select(c => new ClienteBusquedaDto(null, c.Bp, c.Nit, c.Nombre))
                .ToList();
        }

        var locales = await clientes.BuscarAsync(termino, Top, ct);
        return locales.Select(c => new ClienteBusquedaDto(c.Id, null, c.Nit, c.Nombre)).ToList();
    }

    private static string Normalizar(string s) => s.Trim().ToUpperInvariant();
}
