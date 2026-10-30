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

/// <summary>
/// Búsqueda de ciudades para Origen/Destino del formulario de captura (S5-HU-16.1/#254),
/// filtrada a Colombia (Pais == "CO") dentro de tbl_municipios — antes era un catálogo
/// aparte resuelto en vivo contra tblSAPDANE, filtrado a solo lo "costeable" en el
/// tarifario SAP; ahora es el catálogo DANE completo de Colombia (~1128 municipios, más
/// amplio: incluye los que el tarifario SAP de plaza todavía no tiene cargados, que de
/// todas formas pueden costear por SICE-TAC). 8 entradas que antes existían como alias de
/// zona agregada o localidad sub-municipal ("Valle de Aburrá", "Urabá", "Don Matías" sin
/// tilde, etc. — ver clsResolucionZonaSap.AliasZonasAgregadas, retirado en esta issue) ya
/// no aparecen: no tienen un código DANE propio que las represente.
/// Filtra por departamento y/o por texto — al menos uno debería venir, pero ninguno es obligatorio.
/// </summary>
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
