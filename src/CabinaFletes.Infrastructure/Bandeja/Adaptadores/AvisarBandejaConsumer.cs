using CabinaFletes.Application.CasosDeUso.Bandeja.DTOs;
using CabinaFletes.Application.CasosDeUso.Bandeja.Interfaces;
using CabinaFletes.Mensajes.Bandeja;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Bandeja.Adaptadores;

/// <summary>
/// Traduce el mensaje del broker en un empuje a quien tenga la bandeja abierta.
///
/// No lee nada de la base, al contrario que el consumidor de avisos de trabajos: aquel
/// tenía que leer el estado para que el aviso y la consulta no pudieran decir cosas
/// distintas. Aquí el aviso no dice nada del contenido — dice "reconsulta" — así que no
/// hay nada que leer ni nada que pueda quedar desalineado.
///
/// UN FALLO AL AVISAR NO ES UNA AVERÍA. Se registra y se sigue. Dejar subir la excepción
/// mandaría el mensaje a reintentos y luego a la cola de error, disparando la alerta del
/// equipo porque alguien cerró una pestaña. La bandeja sigue correcta: la garantía es la
/// consulta, el aviso es la mejora.
/// </summary>
public sealed class AvisarBandejaConsumer(
    INotificadorDeBandeja notificador,
    ILogger<AvisarBandejaConsumer> log)
    : IConsumer<BandejaCambio>
{
    public async Task Consume(ConsumeContext<BandejaCambio> contexto)
    {
        var mensaje = contexto.Message;
        try
        {
            await notificador.AvisarAsync(
                new AvisoDeBandeja(mensaje.Motivo, mensaje.En), contexto.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el cambio de bandeja ({Motivo}). La bandeja sigue correcta; " +
                "se ve al consultarla.", mensaje.Motivo);
        }
    }
}
