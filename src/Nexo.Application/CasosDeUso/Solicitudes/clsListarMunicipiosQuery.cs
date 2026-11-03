using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Catálogo completo de municipios (S4-HU-09.1), todas las columnas — sin
/// filtro ni paginación, para poblar el desplegable de Origen/Destino.</summary>
public sealed record clsListarMunicipiosQuery : IQuery<IReadOnlyList<clsMunicipioDto>>;

public sealed class clsListarMunicipiosQueryHandler(IMunicipiosRepository municipios)
    : IQueryHandler<clsListarMunicipiosQuery, IReadOnlyList<clsMunicipioDto>>
{
    public async Task<IReadOnlyList<clsMunicipioDto>> Handle(clsListarMunicipiosQuery query, CancellationToken ct = default)
    {
        var catalogo = await municipios.ListarAsync(ct);
        return catalogo
            .Select(m => new clsMunicipioDto(m.Id, m.CodigoDane, m.Municipio, m.Departamento, m.Pais, m.NombreTarifasPlaza, m.ZonaTarificacion))
            .ToList();
    }
}
