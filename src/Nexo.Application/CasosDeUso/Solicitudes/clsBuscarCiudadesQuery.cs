using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Departamentos del catálogo de ciudades costeables.</summary>
public sealed record clsListarDepartamentosQuery : IQuery<IReadOnlyList<string>>;

public sealed class clsListarDepartamentosQueryHandler(ICiudadesDwPort ciudades)
    : IQueryHandler<clsListarDepartamentosQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(clsListarDepartamentosQuery query, CancellationToken ct = default)
    {
        var catalogo = await ciudades.ListarAsync(ct);
        return catalogo
            .Select(c => c.Departamento)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }
}

/// <summary>
/// Búsqueda de ciudades para Origen/Destino del formulario de captura, dentro del
/// catálogo ya filtrado a solo lo "costeable". Filtra por departamento y/o por texto —
/// al menos uno debería venir, pero ninguno es obligatorio.
/// </summary>
public sealed record clsBuscarCiudadesQuery(string? Departamento, string? Q) : IQuery<IReadOnlyList<clsCiudadBusquedaDto>>;

public sealed class clsBuscarCiudadesQueryHandler(ICiudadesDwPort ciudades)
    : IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>>
{
    private const int Top = 50;

    public async Task<IReadOnlyList<clsCiudadBusquedaDto>> Handle(clsBuscarCiudadesQuery query, CancellationToken ct = default)
    {
        var catalogo = await ciudades.ListarAsync(ct);
        var termino = clsTexto.Normalizar(query.Q);
        var departamento = clsTexto.Normalizar(query.Departamento);

        return catalogo
            .Where(c => string.IsNullOrEmpty(departamento) || clsTexto.Normalizar(c.Departamento) == departamento)
            .Where(c => string.IsNullOrEmpty(termino) || clsTexto.Normalizar(c.Municipio).Contains(termino))
            .OrderBy(c => c.Municipio)
            .Take(Top)
            .Select(c => new clsCiudadBusquedaDto(c.Municipio, c.Departamento))
            .ToList();
    }
}
