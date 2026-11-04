using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Manda el correo de una cotización entregada, fuera de la petición (llamado desde un consumidor).
/// El destinatario es <c>clsSolicitud.Creador</c>, nunca el cliente (<c>clsCliente</c> no tiene correo).
/// Los fallos de la salida de correo SUBEN para que el bus reintente o los lleve a la cola de error; lo que no es avería
/// (sin cliente, sin creador, sin relay configurado) se resuelve sin error porque reintentar no lo cambia.
/// </summary>
public sealed record clsNotificarEntregaCommand(
    Guid CotizacionId,
    Guid SolicitudId,
    byte[]? FormatoPdf,
    string? NombrePdf) : ICommand<clsResultadoDeNotificacionDto>;

public sealed class clsNotificarEntregaCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    ILineasSolicitudPort lineasSolicitud,
    ICorreoPort correo)
    : ICommandHandler<clsNotificarEntregaCommand, clsResultadoDeNotificacionDto>
{
    public async Task<clsResultadoDeNotificacionDto> Handle(
        clsNotificarEntregaCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct);
        if (c is null)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La cotización ya no existe; no hay nada que notificar.");

        var solicitud = await solicitudes.ObtenerConClienteAsync(command.SolicitudId, ct);
        var cliente = solicitud?.clsCliente;
        if (cliente is null)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La cotización no tiene cliente asociado; no hay a quién notificar.");

        var solicitante = solicitud!.Creador;
        if (solicitante is null)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La solicitud no tiene un usuario creador identificable; no hay a quién notificar.");

        if (!correo.Configurado)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La salida de correo corporativo no está configurada.");

        // El correo lleva el motivo de cada línea rechazada.
        var rechazadas = (await lineasSolicitud.ObtenerRechazadasAsync(command.SolicitudId, ct))
            .Select(l => new clsLineaRechazadaInfo(
                l.clsRuta is { } r ? $"{r.Origen} → {r.Destino}" : "—", l.Tipologia?.ToString(), l.MotivoRechazo ?? string.Empty))
            .ToList();

        var adjuntos = command.FormatoPdf is { Length: > 0 }
            ? new[]
            {
                new clsAdjuntoDeCorreo(
                    command.NombrePdf ?? $"cotizacion-{c.Id.ToString()[..8]}.pdf",
                    command.FormatoPdf)
            }
            : [];

        // Los metadatos permiten responder "¿salió la notificación de esta cotización?"
        // consultando el relay, sin cruzar logs a mano.
        var id = await correo.EnviarAsync(
            new clsCorreoParaEnviar(
                [solicitante.Email.Trim()],
                clsPlantillaDeEntrega.Asunto(c, cliente),
                clsPlantillaDeEntrega.CuerpoHtml(c, cliente, solicitante.Nombre, rechazadas),
                adjuntos,
                Metadatos: new Dictionary<string, string> { ["cotizacion"] = c.Id.ToString() }),
            ct);

        return clsResultadoDeNotificacionDto.Enviada(solicitante.Email.Trim(), id, adjuntos.Length);
    }
}
