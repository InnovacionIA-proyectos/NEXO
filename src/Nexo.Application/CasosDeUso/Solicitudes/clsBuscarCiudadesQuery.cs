using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Departamentos del catálogo de municipios de Colombia.</summary>
public sealed record clsListarDepartamentosQuery : IQuery<IReadOnlyList<string>>;

public sealed class clsListarDepartamentosQueryHandler(IMunicipiosRepository municipios)
    : IQueryHandler<clsListarDepartamentosQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(clsListarDepartamentosQuery query, CancellationToken ct = default)
    {
        var catalogo = await municipios.ListarAsync(ct);
        return catalogo
            .Where(m => m.Pais == "CO")
            .Select(m => m.Departamento)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }
}

/// <summary>Búsqueda de ciudades (catálogo DANE de Colombia en tbl_municipios) para Origen/Destino; filtra por departamento y/o texto, ninguno es obligatorio.</summary>
public sealed record clsBuscarCiudadesQuery(string? Departamento, string? Q) : IQuery<IReadOnlyList<clsCiudadBusquedaDto>>;

public sealed class clsBuscarCiudadesQueryHandler(IMunicipiosRepository municipios)
    : IQueryHandler<clsBuscarCiudadesQuery, IReadOnlyList<clsCiudadBusquedaDto>>
{
    private const int Top = 50;

    public async Task<IReadOnlyList<clsCiudadBusquedaDto>> Handle(clsBuscarCiudadesQuery query, CancellationToken ct = default)
    {
        var catalogo = await municipios.ListarAsync(ct);
        var termino = clsTexto.Normalizar(query.Q);
        var departamento = clsTexto.Normalizar(query.Departamento);

        return catalogo
            .Where(m => m.Pais == "CO")
            .Where(m => string.IsNullOrEmpty(departamento) || clsTexto.Normalizar(m.Departamento) == departamento)
            .Where(m => string.IsNullOrEmpty(termino) || clsTexto.Normalizar(m.Municipio).Contains(termino))
            .OrderBy(m => m.Municipio)
            .Take(Top)
            .Select(m => new clsCiudadBusquedaDto(m.Id, m.CodigoDane, m.Municipio, m.Departamento, m.NombreTarifasPlaza, m.ZonaTarificacion))
            .ToList();
    }
}
