using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

public sealed record clsListarReglasSoloSicetacQuery() : IQuery<IReadOnlyList<clsReglaSoloSicetacDto>>;

public sealed class clsListarReglasSoloSicetacQueryHandler(IReglasSoloSicetacRepository reglas)
    : IQueryHandler<clsListarReglasSoloSicetacQuery, IReadOnlyList<clsReglaSoloSicetacDto>>
{
    public async Task<IReadOnlyList<clsReglaSoloSicetacDto>> Handle(clsListarReglasSoloSicetacQuery query, CancellationToken ct = default)
    {
        var lista = await reglas.ListarAsync(ct);
        return lista
            .OrderBy(r => r.Origen).ThenBy(r => r.Destino).ThenBy(r => r.TipoVehiculo)
            .Select(r => new clsReglaSoloSicetacDto(r.Id, r.Origen, r.Destino, r.TipoVehiculo, r.CreatedAt))
            .ToList();
    }
}
