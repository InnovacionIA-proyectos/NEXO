using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

public sealed record clsListarTarifasPlazaQuery : IQuery<IReadOnlyList<clsTarifaPlazaDto>>;

public sealed class clsListarTarifasPlazaQueryHandler(ITarifasPlazaRepository tarifas)
    : IQueryHandler<clsListarTarifasPlazaQuery, IReadOnlyList<clsTarifaPlazaDto>>
{
    public async Task<IReadOnlyList<clsTarifaPlazaDto>> Handle(clsListarTarifasPlazaQuery query, CancellationToken ct = default)
    {
        var lista = await tarifas.ListarAsync(ct);
        return lista
            .OrderBy(t => t.LineaNegocio).ThenBy(t => t.Origen != null ? t.Origen.Municipio : "").ThenBy(t => t.Destino != null ? t.Destino.Municipio : "")
            .Select(t => t.ToDto())
            .ToList();
    }
}
