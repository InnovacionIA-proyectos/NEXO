using Nexo.Application.CasosDeUso.RangosPeso.DTOs;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.RangosPeso;

public sealed record clsListarRangosPesoQuery : IQuery<IReadOnlyList<clsRangoPesoDto>>;

public sealed class clsListarRangosPesoQueryHandler(IRangosPesoRepository rangos)
    : IQueryHandler<clsListarRangosPesoQuery, IReadOnlyList<clsRangoPesoDto>>
{
    public async Task<IReadOnlyList<clsRangoPesoDto>> Handle(clsListarRangosPesoQuery query, CancellationToken ct = default)
    {
        var lista = await rangos.ListarAsync(ct);
        return lista.OrderBy(r => r.PesoMinimo)
            .Select(r => new clsRangoPesoDto(r.Id, r.PesoMinimo, r.PesoMaximo))
            .ToList();
    }
}
