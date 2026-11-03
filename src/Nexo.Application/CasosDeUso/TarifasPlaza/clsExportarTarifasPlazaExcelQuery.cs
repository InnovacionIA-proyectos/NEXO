using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

/// <summary>Exporta a Excel el catálogo completo de tarifas de plaza.</summary>
public sealed record clsExportarTarifasPlazaExcelQuery : IQuery<clsArchivoDto>;

public sealed class clsExportarTarifasPlazaExcelQueryHandler(
    ITarifasPlazaRepository tarifas, IExportadorTarifasPlazaExcelPort exportador)
    : IQueryHandler<clsExportarTarifasPlazaExcelQuery, clsArchivoDto>
{
    public async Task<clsArchivoDto> Handle(clsExportarTarifasPlazaExcelQuery query, CancellationToken ct = default)
    {
        var lista = await tarifas.ListarAsync(ct);
        var (contenido, nombre) = exportador.Generar(lista);
        return new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
