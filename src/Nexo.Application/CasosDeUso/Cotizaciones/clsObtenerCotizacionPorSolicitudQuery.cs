using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>La cotización que ya existe para una solicitud, SIN crear nada; devuelve null (no lanza) si no hay, que para quien llama es un estado normal.</summary>
public sealed record clsObtenerCotizacionPorSolicitudQuery(Guid SolicitudId) : IQuery<clsCotizacionDto?>;

public sealed class clsObtenerCotizacionPorSolicitudQueryHandler(
    ICotizacionRepository cotizaciones, ISolicitudesPort solicitudes)
    : IQueryHandler<clsObtenerCotizacionPorSolicitudQuery, clsCotizacionDto?>
{
    public async Task<clsCotizacionDto?> Handle(clsObtenerCotizacionPorSolicitudQuery query, CancellationToken ct = default)
    {
        // Misma elección que clsCrearCotizacionCommand para que GET y POST apunten a la misma cotización: la Entregada si la solicitud ya se
        // entregó, si no el borrador en curso.
        var solicitud = await solicitudes.ObtenerBasicoAsync(query.SolicitudId, ct);
        if (solicitud is null) return null;

        if (solicitud.Estado == EstadoSolicitud.Entregada)
        {
            var entregada = await cotizaciones.ObtenerConLineasYConexosAsync(
                c => c.SolicitudId == query.SolicitudId && c.Estado == EstadoCotizacion.Entregada, ct);
            if (entregada is not null) return entregada.ToDto();
        }

        var borrador = await cotizaciones.ObtenerConLineasYConexosAsync(
            c => c.SolicitudId == query.SolicitudId && c.Estado == EstadoCotizacion.Borrador, ct);
        return borrador?.ToDto();
    }
}
