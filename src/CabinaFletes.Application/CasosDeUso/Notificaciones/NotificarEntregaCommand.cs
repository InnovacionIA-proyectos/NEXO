using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.CasosDeUso.Notificaciones.Plantillas;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;

namespace CabinaFletes.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Manda el correo de una cotización entregada.
///
/// FUERA DE LA PETICIÓN, y ese es el punto de la HU. Antes esto vivía dentro del caso de
/// uso de entrega: quien entregaba esperaba a que el correo saliera, y con el relay lento
/// esperaba de más. Ahora la entrega publica el evento y responde; esto corre después,
/// llamado desde un consumidor.
///
/// LA NOTIFICACIÓN ES INTERNA. Va al correo del comercial responsable —que es personal de
/// Botero Soto— y nunca al cliente. La #22 va a derivar el destinatario de la sesión de
/// quien pidió la consulta y a dejar una prueba de que ninguna dirección de cliente puede
/// ser destinataria; hoy se mantiene la resolución que ya existía.
///
/// LO QUE NO SE PUEDE HACER es tragarse los fallos. Antes se devolvían en el mensaje de
/// la respuesta y quedaban ahí; ahora que nadie mira la respuesta, un fallo silencioso
/// sería una notificación perdida sin rastro. Así que las excepciones de la salida de
/// correo SUBEN: el bus reintenta las pasajeras y manda a la cola de error las que no
/// tienen arreglo.
///
/// Lo que sí se resuelve sin error son las situaciones en que no hay a quién escribirle
/// —sin cliente, sin comercial responsable, sin relay configurado—. Eso no es una avería:
/// es que no había correo que mandar, y reintentarlo cuatro veces no lo va a cambiar.
/// </summary>
public sealed record NotificarEntregaCommand(
    Guid CotizacionId,
    Guid SolicitudId,
    byte[]? FormatoPdf,
    string? NombrePdf) : ICommand<ResultadoDeNotificacionDto>;

public sealed class NotificarEntregaCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    IComercialResponsablePort comercialResponsable,
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

        if (!comercialResponsable.Configurado)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "El DW del comercial responsable no está configurado (ConnectionStrings:DwCubosSap).");

        var responsable = await comercialResponsable.BuscarPorNitAsync(cliente.Nit.Trim(), ct);
        if (responsable is null)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar(
                $"El cliente (NIT {cliente.Nit.Trim()}) no tiene comercial responsable con correo en DimCliente.");

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
                [responsable.Correo.Trim()],
                PlantillaDeEntrega.Asunto(c, cliente),
                PlantillaDeEntrega.CuerpoHtml(c, cliente, responsable.Nombre),
                adjuntos,
                Metadatos: new Dictionary<string, string> { ["cotizacion"] = c.Id.ToString() }),
            ct);

        return ResultadoDeNotificacionDto.Enviada(responsable.Correo.Trim(), id, adjuntos.Length);
    }
}
