using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// Descarga masiva del catálogo a Excel (sirve de plantilla para reimportar). <paramref name="Q"/> (texto libre sobre tipo/código)
/// y <paramref name="Categoria"/> ("logistico"/"adicional") filtran igual que GET /api/servicios-conexos.
/// </summary>
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
