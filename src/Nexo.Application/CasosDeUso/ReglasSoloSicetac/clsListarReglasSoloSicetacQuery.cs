using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

public sealed record clsListarReglasSoloSicetacQuery() : IQuery<IReadOnlyList<clsReglaSoloSicetacDto>>;

public sealed class clsListarReglasSoloSicetacQueryHandler(IReglasSoloSicetacRepository reglas, IMunicipiosRepository municipios)
    : IQueryHandler<clsListarReglasSoloSicetacQuery, IReadOnlyList<clsReglaSoloSicetacDto>>
{
    public async Task<IReadOnlyList<clsReglaSoloSicetacDto>> Handle(clsListarReglasSoloSicetacQuery query, CancellationToken ct = default)
    {
        var lista = await reglas.ListarAsync(ct);

        // Origen/Destino se guardan como código DANE (carga masiva S4-HU-09.2) o texto
        // libre (filas más viejas) — se resuelve a nombre de municipio para el listado,
        // sin tocar el valor crudo (front#195/#242: el front necesita el nombre, no el
        // código). Se prefiere Colombia en el empate: el código DANE no es único por sí
        // solo entre CO/VE/EC (clsMunicipio.CodigoDane), pero las reglas Solo-SICETAC son
        // siempre rutas colombianas.
        var catalogo = await municipios.ListarAsync(ct);
        var porCodigoDane = catalogo
            .GroupBy(m => m.CodigoDane)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault(m => m.Pais == "CO") ?? g.First());
        string? NombreMunicipio(string codigoCrudo) =>
            porCodigoDane.TryGetValue(codigoCrudo, out var m) ? $"{m.Municipio}, {m.Departamento}" : null;

        return lista
            .OrderBy(r => r.Origen).ThenBy(r => r.Destino).ThenBy(r => r.TipoVehiculo)
            .Select(r => new clsReglaSoloSicetacDto(
                r.Id, r.Origen, r.Destino, NombreMunicipio(r.Origen), NombreMunicipio(r.Destino), r.TipoVehiculo, r.CreatedAt))
            .ToList();
    }
}
