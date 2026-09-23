using System.Net;
using Nexo.Application.Common.Ports;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>
/// Alerta al equipo cuando CUALQUIER mensaje agota sus reintentos y MassTransit
/// lo mueve a la cola de error.
///
/// Es un consumer de TIPO ABIERTO (<c>AddConsumer(typeof(clsAlertaDeFalloConsumer&lt;&gt;))</c>):
/// se engancha una sola vez para todos los tipos de mensaje presentes y futuros,
/// sin tener que acordarse de registrar uno nuevo cada vez que aparece un mensaje
/// - a diferencia de <see cref="Costeo.Adaptadores.clsFalloDeCosteoConsumer"/> (que sí
/// es específico de <c>clsCostearLineaSolicitada</c>, porque ahí el fallo se convierte
/// en un DESENLACE DE NEGOCIO que la saga necesita contar). Este consumer no tiene
/// opinión de negocio: solo avisa, y corre ADEMÁS de cualquier consumer específico
/// que ya exista para ese tipo de mensaje - no lo reemplaza.
///
/// Degrada en silencio (con log, no con excepción) si falta el SMTP o la lista de
/// destinatarios: un correo que no sale no debe, a su vez, agotar reintentos y
/// terminar en su propia cola de error.
/// </summary>
public sealed class clsAlertaDeFalloConsumer<T>(
    ICorreoPort correo, IConfiguration config, ILogger<clsAlertaDeFalloConsumer<T>> logger)
    : IConsumer<Fault<T>> where T : class
{
    public async Task Consume(ConsumeContext<Fault<T>> contexto)
    {
        var tipo = typeof(T).Name;
        var excepcion = contexto.Message.Exceptions.FirstOrDefault();
        var motivo = excepcion is null ? "(sin detalle de excepción)" : $"{excepcion.ExceptionType}: {excepcion.Message}";

        logger.LogError(
            "Mensaje {Tipo} cayó a la cola de error tras agotar reintentos ({FaultId}, {Timestamp}). Motivo: {Motivo}",
            tipo, contexto.Message.FaultId, contexto.Message.Timestamp, motivo);

        var destinatarios = config.GetSection("Alertas:EquipoTecnico").GetChildren()
            .Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();
        if (destinatarios.Length == 0)
        {
            logger.LogWarning("Alertas:EquipoTecnico está vacío - nadie recibió correo por este fallo.");
            return;
        }
        if (!correo.Configurado)
        {
            logger.LogWarning("El SMTP corporativo no está configurado - nadie recibió correo por este fallo.");
            return;
        }

        var asunto = $"[NEXO] Mensaje {tipo} cayó a la cola de error";
        var cuerpo = $"""
            <p>Un mensaje de tipo <strong>{WebUtility.HtmlEncode(tipo)}</strong> agotó sus reintentos
            y MassTransit lo movió a su cola de error.</p>
            <p>
            <strong>Fault Id:</strong> {contexto.Message.FaultId}<br/>
            <strong>Cuándo:</strong> {contexto.Message.Timestamp:yyyy-MM-dd HH:mm:ss} UTC<br/>
            <strong>Motivo:</strong> {WebUtility.HtmlEncode(motivo)}
            </p>
            <p>Revisa la consola de administración de RabbitMQ (colas terminadas en <code>_error</code>)
            para ver el mensaje completo y decidir si reintentar manualmente.</p>
            """;

        // Igual que arriba: un correo que no sale no debe, a su vez, agotar reintentos y
        // terminar en su propia cola de error - por eso se degrada en silencio (con log)
        // en vez de dejar subir clsCorreoRechazadoException/clsCorreoTransitorioException.
        try
        {
            await correo.EnviarAsync(
                new clsCorreoParaEnviar(destinatarios, asunto, cuerpo, []), contexto.CancellationToken);
        }
        catch (Exception ex) when (ex is clsCorreoRechazadoException or clsCorreoTransitorioException)
        {
            logger.LogWarning(ex, "No se pudo enviar la alerta por correo para {Tipo} - nadie recibió correo por este fallo.", tipo);
        }
    }
}
