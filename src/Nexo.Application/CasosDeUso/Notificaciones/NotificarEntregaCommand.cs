using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Manda el correo de una cotización entregada.
///
/// FUERA DE LA PETICIÓN, y ese es el punto de la HU-06.1. Antes esto vivía dentro del caso
/// de uso de entrega: quien entregaba esperaba a que el correo saliera, y con el relay lento
/// esperaba de más. Ahora la entrega publica el evento y responde; esto corre después,
/// llamado desde un consumidor.
///
/// LA NOTIFICACIÓN ES INTERNA Y VA A QUIEN LA PIDIÓ (S2-HU-06.2). El destinatario es
/// <c>Solicitud.Creador</c> — el usuario de Nexo que dio de alta la solicitud de flete,
/// resuelto contra la sesión real del portal (S2-HU-03) — nunca el cliente ni un
/// "responsable comercial" sacado de un Data Warehouse por NIT (así resolvía esto antes
/// de la #22; se retiró IComercialResponsablePort/DwCubosSap por completo, ya sin uso).
/// Es, además, una salvaguarda estructural: <c>Cliente</c> no tiene columna de correo, así
/// que no hay de dónde sacar una dirección de cliente aunque alguien lo intentara.
///
/// LO QUE NO SE PUEDE HACER es tragarse los fallos. Antes se devolvían en el mensaje de
/// la respuesta y quedaban ahí; ahora que nadie mira la respuesta, un fallo silencioso
/// sería una notificación perdida sin rastro. Así que las excepciones de la salida de
/// correo SUBEN: el bus reintenta las pasajeras y manda a la cola de error las que no
/// tienen arreglo.
///
/// Lo que sí se resuelve sin error son las situaciones en que no hay a quién escribirle
/// —sin cliente, sin creador, sin relay configurado—. Eso no es una avería: es que no
/// había correo que mandar, y reintentarlo cuatro veces no lo va a cambiar.
/// </summary>
public sealed record NotificarEntregaCommand(
    Guid CotizacionId,
    Guid SolicitudId,
    byte[]? FormatoPdf,
    string? NombrePdf) : ICommand<ResultadoDeNotificacionDto>;

public sealed class NotificarEntregaCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    ICorreoPort correo)
    : ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto>
{
    public async Task<ResultadoDeNotificacionDto> Handle(
        NotificarEntregaCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct);
        if (c is null)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La cotización ya no existe; no hay nada que notificar.");

        var solicitud = await solicitudes.ObtenerConClienteAsync(command.SolicitudId, ct);
        var cliente = solicitud?.Cliente;
        if (cliente is null)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La cotización no tiene cliente asociado; no hay a quién notificar.");

        var solicitante = solicitud!.Creador;
        if (solicitante is null)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La solicitud no tiene un usuario creador identificable; no hay a quién notificar.");

        if (!correo.Configurado)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La salida de correo corporativo no está configurada.");

        var adjuntos = command.FormatoPdf is { Length: > 0 }
            ? new[]
            {
                new AdjuntoDeCorreo(
                    command.NombrePdf ?? $"cotizacion-{c.Id.ToString()[..8]}.pdf",
                    command.FormatoPdf)
            }
            : [];

        // Los metadatos permiten responder "¿salió la notificación de esta cotización?"
        // consultando el relay, sin cruzar logs a mano.
        var id = await correo.EnviarAsync(
            new CorreoParaEnviar(
                [solicitante.Email.Trim()],
                PlantillaDeEntrega.Asunto(c, cliente),
                PlantillaDeEntrega.CuerpoHtml(c, cliente, solicitante.Nombre),
                adjuntos,
                Metadatos: new Dictionary<string, string> { ["cotizacion"] = c.Id.ToString() }),
            ct);

        return ResultadoDeNotificacionDto.Enviada(solicitante.Email.Trim(), id, adjuntos.Length);
    }
}
