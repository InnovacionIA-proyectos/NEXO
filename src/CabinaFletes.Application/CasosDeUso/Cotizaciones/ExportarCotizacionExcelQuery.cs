using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones;

/// <summary>Exporta a Excel el detalle completo de la cotización (fletes + servicios).</summary>
public sealed record ExportarCotizacionExcelQuery(Guid Id) : IQuery<ArchivoDto>;

public sealed class ExportarCotizacionExcelQueryHandler(
    ICotizacionRepository cotizaciones, IExportadorCotizacionExcelPort exportador)
    : IQueryHandler<ExportarCotizacionExcelQuery, ArchivoDto>
{
    public async Task<ArchivoDto> Handle(ExportarCotizacionExcelQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerParaExportarAsync(x => x.Id == query.Id, ct)
            ?? throw new NotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");

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
        return new ArchivoDto(contenido, nombre, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }
}
