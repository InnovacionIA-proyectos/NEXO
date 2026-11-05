using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Búsqueda de clientes para la captura: primero clientes reales del DW (nombre, NIT o BP); solo si no hay resultados cae a prospectos,
/// así nunca se combinan ni deduplican los dos catálogos. Sin DW configurado busca en la tabla local.
/// </summary>
public sealed record clsBuscarClientesQuery(string? Q) : IQuery<IReadOnlyList<clsClienteBusquedaDto>>;

public sealed class clsBuscarClientesQueryHandler(IClientesDwPort clientesDw, IProspectosDwPort prospectosDw, IClientesPort clientes)
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
                : catalogo.Where(c => Normalizar(c.Nombre).Contains(termino) || c.Nit.Contains(termino) || c.Bp.Contains(termino)).ToList();

            if (filtrados.Count > 0 || !prospectosDw.Configurado)
            {
                return filtrados
                    .OrderBy(c => c.Nombre)
                    .Take(Top)
                    .Select(c => new clsClienteBusquedaDto(null, c.Bp, c.Nit, c.Nombre, "cliente"))
                    .ToList();
            }

            // Sin coincidencias en el catálogo real de clientes: puede ser un
            // prospecto todavía sin BP asignado.
            var prospectos = await prospectosDw.ListarAsync(ct);
            var filtradosProspectos = string.IsNullOrEmpty(termino)
                ? prospectos
                : prospectos.Where(p => Normalizar(p.Nombre).Contains(termino) || p.Nit.Contains(termino));
            return filtradosProspectos
                .OrderBy(p => p.Nombre)
                .Take(Top)
                .Select(p => new clsClienteBusquedaDto(null, null, p.Nit, p.Nombre, "prospecto"))
                .ToList();
        }

        var locales = await clientes.BuscarAsync(termino, Top, ct);
        return locales.Select(c => new clsClienteBusquedaDto(c.Id, null, c.Nit, c.Nombre, "cliente")).ToList();
    }

    private static string Normalizar(string s) => s.Trim().ToUpperInvariant();
}
