using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Common;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>Departamentos del catálogo de ciudades costeables (HU-005 parte 3).</summary>
public sealed record ListarDepartamentosQuery : IQuery<IReadOnlyList<string>>;

public sealed class ListarDepartamentosQueryHandler(ICiudadesDwPort ciudades)
    : IQueryHandler<ListarDepartamentosQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(ListarDepartamentosQuery query, CancellationToken ct = default)
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
/// catálogo ya filtrado a solo lo "costeable" (HU-005 parte 3). Filtra por departamento
/// y/o por texto — al menos uno debería venir, pero ninguno es obligatorio.
/// </summary>
public sealed record BuscarCiudadesQuery(string? Departamento, string? Q) : IQuery<IReadOnlyList<CiudadBusquedaDto>>;

public sealed class BuscarCiudadesQueryHandler(ICiudadesDwPort ciudades)
    : IQueryHandler<BuscarCiudadesQuery, IReadOnlyList<CiudadBusquedaDto>>
{
    private const int Top = 50;

    public async Task<IReadOnlyList<CiudadBusquedaDto>> Handle(BuscarCiudadesQuery query, CancellationToken ct = default)
    {
        var catalogo = await ciudades.ListarAsync(ct);
        var termino = Texto.Normalizar(query.Q);
        var departamento = Texto.Normalizar(query.Departamento);

        return catalogo
            .Where(c => string.IsNullOrEmpty(departamento) || Texto.Normalizar(c.Departamento) == departamento)
            .Where(c => string.IsNullOrEmpty(termino) || Texto.Normalizar(c.Municipio).Contains(termino))
            .OrderBy(c => c.Municipio)
            .Take(Top)
            .Select(c => new CiudadBusquedaDto(c.Municipio, c.Departamento))
            .ToList();
    }
}
