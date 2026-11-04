using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Reglas que comparten agregar y quitar un conexo: se admite mientras la cotización no tenga resultado (Borrador o Entregada); Ganada o
/// Perdida no admite cambios porque el resultado realimentó la Biblioteca. Editar la cantidad (PATCH) sigue siendo solo Borrador.
/// </summary>
internal static class clsCambioDeConexos
{
    public static void ExigirSinResultado(clsCotizacion c)
    {
        if (c.Resultado != ResultadoCotizacion.SinDefinir)
            throw new clsConflictException("COTIZACION_CERRADA",
                "La cotización ya está marcada como Ganada/Perdida: no admite cambios de servicios.");
    }

    /// <summary>Detalle de la auditoría de agregar/quitar: servicio, cantidad y valor de cada ítem, y si el cambio fue tras la entrega.</summary>
    public static string DetalleDeAuditoria(IReadOnlyList<clsConexoCambiado> items, bool trasEntrega) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            items = items.Select(i => new { servicio = i.Servicio, cantidad = i.Cantidad, valor = i.Valor }),
            trasEntrega,
        });

    /// <summary>Si la cotización ya estaba entregada: deja la marca <c>ModificadaTrasEntregaAt</c> (sin tocar Estado ni EntregadaAt, para que el SLA
    /// siga midiendo la entrega original) y avisa a quien creó la solicitud, salvo que sea el actor. Se llama ANTES de guardar: el evento va por el
    /// Outbox en la misma transacción.</summary>
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
