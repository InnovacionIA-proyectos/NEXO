using Nexo.Application.CasosDeUso.Bandeja.DTOs;
using Nexo.Application.CasosDeUso.Bandeja.Interfaces;
using Nexo.Mensajes.Bandeja;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Bandeja.Adaptadores;

/// <summary>Traduce el mensaje del broker en un empuje a quien tenga la bandeja abierta; el aviso solo dice "reconsulta", sin leer la base.
/// Un fallo al avisar se registra y se sigue: la garantía es la consulta, el aviso es la mejora.</summary>
public sealed class clsAvisarBandejaConsumer(
    INotificadorDeBandeja notificador,
    ILogger<clsAvisarBandejaConsumer> log)
    : IConsumer<clsBandejaCambio>
{
    public async Task Consume(ConsumeContext<clsBandejaCambio> contexto)
    {
        var mensaje = contexto.Message;
        try
        {
            await notificador.AvisarAsync(
                new clsAvisoDeBandeja(mensaje.Motivo, mensaje.En), contexto.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el cambio de bandeja ({Motivo}). La bandeja sigue correcta; " +
                "se ve al consultarla.", mensaje.Motivo);
        }
    }
}
