using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Avisa al creador de la solicitud que se agregó o quitó un servicio conexo de su cotización ya entregada. Mismas reglas
/// que <see cref="clsNotificarEntregaCommand"/>: el destinatario es <c>clsSolicitud.Creador</c>, un fallo del correo SUBE
/// para que el bus reintente, y lo que no es avería (sin creador, sin relay) se resuelve sin error.
/// </summary>
public sealed record clsNotificarConexosCambiadosCommand(clsConexosCotizacionCambiados Evento)
    : ICommand<clsResultadoDeNotificacionDto>;

public sealed class clsNotificarConexosCambiadosCommandHandler(
    ISolicitudesPort solicitudes,
    ICorreoPort correo)
    : ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto>
{
    public async Task<clsResultadoDeNotificacionDto> Handle(
        clsNotificarConexosCambiadosCommand command, CancellationToken ct = default)
    {
        var m = command.Evento;

        var solicitud = await solicitudes.ObtenerConClienteAsync(m.SolicitudId, ct);
        var cliente = solicitud?.clsCliente;
        if (cliente is null)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La solicitud no tiene cliente asociado; no hay a quién notificar.");

        var creador = solicitud!.Creador;
        if (creador is null)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La solicitud no tiene un usuario creador identificable; no hay a quién notificar.");

        if (!correo.Configurado)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar(
                "La salida de correo corporativo no está configurada.");

        var id = await correo.EnviarAsync(
            new clsCorreoParaEnviar(
                [creador.Email.Trim()],
                clsPlantillaConexosCambiados.Asunto(m, cliente),
                clsPlantillaConexosCambiados.CuerpoHtml(m, cliente, creador.Nombre),
                [],
                Metadatos: new Dictionary<string, string> { ["cotizacion"] = m.CotizacionId.ToString() }),
            ct);

        return clsResultadoDeNotificacionDto.Enviada(creador.Email.Trim(), id, 0);
    }
}
