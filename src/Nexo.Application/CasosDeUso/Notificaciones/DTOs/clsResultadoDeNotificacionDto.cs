namespace Nexo.Application.CasosDeUso.Notificaciones.DTOs;

/// <summary>
/// Qué pasó al intentar notificar: distingue "se envió" de "no había que enviar" (configuración incompleta).
/// Un fallo de la salida de correo no cabe aquí: sube como excepción para que el bus reintente.
/// </summary>
/// <param name="Enviado">Si el correo se entregó a la salida corporativa.</param>
/// <param name="Destinatario">A quién, cuando se envió.</param>
/// <param name="IdEnElRelay">Identificador con el que consultar su estado, si lo dio.</param>
/// <param name="Adjuntos">Cuántos archivos se adjuntaron.</param>
/// <param name="Motivo">Por qué no había que enviar, cuando no se envió.</param>
public record clsResultadoDeNotificacionDto(
    bool Enviado,
    string? Destinatario,
    string? IdEnElRelay,
    int Adjuntos,
    string? Motivo)
{
    public static clsResultadoDeNotificacionDto Enviada(string destinatario, string? idEnElRelay, int adjuntos) =>
        new(true, destinatario, idEnElRelay, adjuntos, null);

    public static clsResultadoDeNotificacionDto NoHabiaQueEnviar(string motivo) =>
        new(false, null, null, 0, motivo);
}
