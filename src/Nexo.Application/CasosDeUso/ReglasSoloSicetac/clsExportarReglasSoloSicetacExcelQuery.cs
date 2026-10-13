using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Exporta a Excel las reglas "solo SICE-TAC" ya cargadas (S5-HU-22.1/#288) — mismo
/// patrón que el export de Tarifas de Plaza; el archivo se puede editar y reimportar.</summary>
public sealed record clsExportarReglasSoloSicetacExcelQuery : IQuery<clsArchivoDto>;

public sealed class clsExportarReglasSoloSicetacExcelQueryHandler(
    IReglasSoloSicetacRepository reglas, IExportadorReglasSoloSicetacExcelPort exportador)
    : IQueryHandler<clsExportarReglasSoloSicetacExcelQuery, clsArchivoDto>
{
    public async Task<clsArchivoDto> Handle(clsExportarReglasSoloSicetacExcelQuery query, CancellationToken ct = default)
    {
        var lista = await reglas.ListarAsync(ct);
        var (contenido, nombre) = exportador.Generar(lista);
        return new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
