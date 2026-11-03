using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286: la cotización que ya existe para una solicitud, SIN crear nada. Hasta
/// ahora el único camino para traerla era <c>POST /api/cotizaciones</c> (crear o recuperar),
/// que tiene efecto secundario: Mesa de Costeo solo lo llamaba si el usuario hacía clic o
/// tenía <c>autoAplicarPropuestas</c>, así que la pantalla no sabía que ya había una
/// cotización (p. ej. la auto-costeada al capturar) y "Iniciar costeo" parecía aplicar todo.
///
/// Devuelve null —no lanza— cuando no hay: para el llamador "todavía no hay cotización" es
/// un estado normal, no un error.
/// </summary>
public sealed record clsObtenerCotizacionPorSolicitudQuery(Guid SolicitudId) : IQuery<clsCotizacionDto?>;

public sealed class clsObtenerCotizacionPorSolicitudQueryHandler(
    ICotizacionRepository cotizaciones, ISolicitudesPort solicitudes)
    : IQueryHandler<clsObtenerCotizacionPorSolicitudQuery, clsCotizacionDto?>
{
    public async Task<clsCotizacionDto?> Handle(clsObtenerCotizacionPorSolicitudQuery query, CancellationToken ct = default)
    {
        // Misma elección que clsCrearCotizacionCommand, para que GET y POST nunca apunten a
        // cotizaciones distintas de la misma solicitud: si la solicitud ya se entregó, su
        // cotización Entregada; si no, el borrador en curso. Un FirstOrDefault a secas por
        // SolicitudId sería ambiguo cuando conviven una entregada y un borrador.
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
