namespace Nexo.Application.CasosDeUso.Notificaciones.DTOs;

/// <summary>
/// Qué pasó al intentar notificar.
///
/// Distingue "se envió" de "no había que enviar", y esa diferencia importa: lo primero va
/// al log como confirmación con el identificador del relay, lo segundo como aviso de
/// configuración incompleta. Sin separarlas, un entorno sin DW configurado se ve igual
/// que uno donde el correo salió — y nadie revisa lo que parece normal.
///
/// Lo que NO cabe aquí es "falló": un fallo de la salida de correo sube como excepción
/// para que el bus lo reintente o lo lleve a la cola de error. Devolverlo como resultado
/// lo dejaría enterrado en una línea de log.
/// </summary>
/// <param name="Enviado">Si el correo se entregó a la salida corporativa.</param>
/// <param name="Destinatario">A quién, cuando se envió.</param>
/// <param name="IdEnElRelay">Identificador con el que consultar su estado, si lo dio.</param>
/// <param name="Adjuntos">Cuántos archivos se adjuntaron.</param>
/// <param name="Motivo">Por qué no había que enviar, cuando no se envió.</param>
public record ResultadoDeNotificacionDto(
    bool Enviado,
    string? Destinatario,
    string? IdEnElRelay,
    int Adjuntos,
    string? Motivo)
{
    public static ResultadoDeNotificacionDto Enviada(string destinatario, string? idEnElRelay, int adjuntos) =>
        new(true, destinatario, idEnElRelay, adjuntos, null);

    public static ResultadoDeNotificacionDto NoHabiaQueEnviar(string motivo) =>
        new(false, null, null, 0, motivo);
}
