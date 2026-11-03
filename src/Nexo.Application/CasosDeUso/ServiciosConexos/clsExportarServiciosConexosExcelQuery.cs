using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>Descarga masiva del catálogo a Excel (S4-HU-15.3/#249) — el mismo archivo sirve
/// de plantilla para reimportar como actualización. S5-HU-19/#261: <paramref name="Q"/>
/// (texto libre sobre tipo/código de servicio) y <paramref name="Categoria"/>
/// ("logistico"/"adicional") filtran el catálogo exportado — mismos filtros que ya usa
/// GET /api/servicios-conexos, sin inventar un criterio de filtrado nuevo.</summary>
public sealed record clsExportarServiciosConexosExcelQuery(string? Q = null, string? Categoria = null) : IQuery<clsArchivoDto>;

public sealed class clsExportarServiciosConexosExcelQueryHandler(
    IServiciosConexosRepository repo, IExportadorServiciosConexosExcelPort exportador)
    : IQueryHandler<clsExportarServiciosConexosExcelQuery, clsArchivoDto>
{
    public async Task<clsArchivoDto> Handle(clsExportarServiciosConexosExcelQuery query, CancellationToken ct = default)
    {
        var pagina = await repo.ListarAsync(false, query.Q, query.Categoria, 1, int.MaxValue, ct);
        var (contenido, nombre) = exportador.Generar(pagina.Items);
        return new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
