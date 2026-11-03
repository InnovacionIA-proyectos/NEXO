using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.CasosDeUso.Notificaciones.Plantillas;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>
/// S5-HU-20.1/#286: avisa a quien creó la solicitud que otra persona agregó o quitó un
/// servicio conexo de su cotización YA entregada ("Cotización actualizada: se agregó X x N;
/// nuevo total $Y"). Corre fuera de la petición, llamado desde un consumidor — mismo patrón
/// y mismas reglas que <see cref="clsNotificarEntregaCommand"/>:
///
/// - el destinatario es <c>clsSolicitud.Creador</c>, nunca el cliente;
/// - un fallo de la salida de correo SUBE (el bus reintenta y, si no tiene arreglo, lo lleva
///   a la cola de error): tragárselo dejaría un aviso perdido sin rastro;
/// - lo que no es avería —no hay creador, no hay relay configurado— se resuelve sin error.
///
/// Cuándo avisar (solo si el actor != creador, solo cotización Entregada) lo decide el caso
/// de uso que publica el evento; acá el mensaje ya viene decidido.
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
