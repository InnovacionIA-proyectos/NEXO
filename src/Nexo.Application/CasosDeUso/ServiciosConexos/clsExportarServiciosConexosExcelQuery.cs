using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>Descarga masiva del catálogo completo a Excel (S4-HU-15.3/#249) — el mismo
/// archivo sirve de plantilla para reimportar como actualización.</summary>
public sealed record clsExportarServiciosConexosExcelQuery : IQuery<clsArchivoDto>;

public sealed class clsExportarServiciosConexosExcelQueryHandler(
    IServiciosConexosRepository repo, IExportadorServiciosConexosExcelPort exportador)
    : IQueryHandler<clsExportarServiciosConexosExcelQuery, clsArchivoDto>
{
    public async Task<clsArchivoDto> Handle(clsExportarServiciosConexosExcelQuery query, CancellationToken ct = default)
    {
        var pagina = await repo.ListarAsync(false, null, null, 1, int.MaxValue, ct);
        var (contenido, nombre) = exportador.Generar(pagina.Items);
        return new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
