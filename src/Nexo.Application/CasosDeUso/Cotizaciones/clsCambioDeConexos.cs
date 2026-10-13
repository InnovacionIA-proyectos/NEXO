using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286 (HU-B23/B24): reglas que comparten agregar y quitar un servicio conexo.
///
/// Los servicios se pueden cambiar mientras la cotización no tenga resultado
/// (<c>Resultado == SinDefinir</c>), o sea Borrador O Entregada. Antes la regla era
/// "solo Borrador", pero un servicio omitido se descubre justo después de entregar. Una vez
/// Ganada o Perdida ya no admite cambios: el resultado realimentó la Biblioteca y es del
/// Comercial. (Editar la cantidad de un conexo —PATCH— sigue siendo solo Borrador: ese cambio
/// no se pidió extender.)
/// </summary>
internal static class clsCambioDeConexos
{
    public static void ExigirSinResultado(clsCotizacion c)
    {
        if (c.Resultado != ResultadoCotizacion.SinDefinir)
            throw new clsConflictException("COTIZACION_CERRADA",
                "La cotización ya está marcada como Ganada/Perdida: no admite cambios de servicios.");
    }

    /// <summary>Detalle de la auditoría de agregar/quitar: servicio, cantidad y valor de cada
    /// ítem (al quitar, el valor que tenía) y si el cambio fue después de la entrega.</summary>
    public static string DetalleDeAuditoria(IReadOnlyList<clsConexoCambiado> items, bool trasEntrega) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            items = items.Select(i => new { servicio = i.Servicio, cantidad = i.Cantidad, valor = i.Valor }),
            trasEntrega,
        });

    /// <summary>Si la cotización ya estaba entregada: deja la marca <c>ModificadaTrasEntregaAt</c>
    /// (sin tocar Estado ni EntregadaAt: el SLA y las métricas siguen midiendo la entrega
    /// original) y publica el aviso a quien creó la solicitud — salvo que el actor sea esa misma
    /// persona. Sobre un Borrador no hace nada. Se llama ANTES de guardar: el evento va por el
    /// Outbox, en la misma transacción que el cambio.</summary>
    public static async Task RegistrarTrasEntregaAsync(
        clsCotizacion c, string accion, IReadOnlyList<clsConexoCambiado> items,
        ISolicitudesPort solicitudes, IPublicadorDeMensajes publicador,
        Common.ICurrentUserPort usuarioActual, TimeProvider clock, CancellationToken ct)
    {
        if (c.Estado != EstadoCotizacion.Entregada) return;

        var ahora = clock.GetUtcNow();
        c.ModificadaTrasEntregaAt = ahora;

        var solicitud = await solicitudes.ObtenerBasicoAsync(c.SolicitudId, ct);
        if (solicitud is null || solicitud.CreadoPor == usuarioActual.Id) return;

        await publicador.PublicarAsync(
            new clsConexosCotizacionCambiados(
                c.Id, c.SolicitudId, accion, items, c.Total, usuarioActual.Id, ahora),
            ct);
    }
}
