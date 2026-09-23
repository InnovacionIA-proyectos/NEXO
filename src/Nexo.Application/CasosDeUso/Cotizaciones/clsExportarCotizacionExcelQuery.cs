using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>Exporta a Excel el detalle completo de la cotización (fletes + servicios).</summary>
public sealed record clsExportarCotizacionExcelQuery(Guid Id) : IQuery<clsArchivoDto>;

public sealed class clsExportarCotizacionExcelQueryHandler(
    ICotizacionRepository cotizaciones, IExportadorCotizacionExcelPort exportador)
    : IQueryHandler<clsExportarCotizacionExcelQuery, clsArchivoDto>
{
    public async Task<clsArchivoDto> Handle(clsExportarCotizacionExcelQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerParaExportarAsync(x => x.Id == query.Id, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");

        // Red de seguridad: si la cotización solicitada está VACÍA (p. ej. un borrador
        // recién abierto sobre una solicitud ya entregada), se exporta la cotización CON
        // contenido de esa solicitud (la entregada tiene prioridad). Nunca un Excel en ceros.
        if (c.Lineas.Count == 0 && c.Conexos.Count == 0)
        {
            var conContenidoId = await cotizaciones.BuscarIdConContenidoPorSolicitudAsync(c.SolicitudId, ct);
            if (conContenidoId is Guid otroId)
                c = await cotizaciones.ObtenerParaExportarAsync(x => x.Id == otroId, ct) ?? c;
        }

        var (contenido, nombre) = exportador.Generar(c);
        return new clsArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
