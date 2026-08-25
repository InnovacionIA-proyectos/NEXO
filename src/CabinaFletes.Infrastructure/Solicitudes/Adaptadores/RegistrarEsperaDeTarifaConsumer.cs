using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;

namespace CabinaFletes.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Apunta la línea en la lista de espera cuando el costeo informa que necesita una
/// tarifa manual.
///
/// La saga cuenta cuántas esperan; esto guarda CUÁLES y qué les falta. Son cosas
/// distintas y las dos hacen falta: la saga necesita el número para saber cuándo
/// terminó, y la pantalla necesita el detalle para que alguien pueda actuar.
///
/// Es un consumidor aparte y no parte de la saga porque escribir en la base es
/// justamente lo que la saga no hace.
/// </summary>
public sealed class RegistrarEsperaDeTarifaConsumer(IEsperasDeTarifaPort esperas)
    : IConsumer<LineaRequiereTarifaManual>
{
    public Task Consume(ConsumeContext<LineaRequiereTarifaManual> contexto)
    {
        var m = contexto.Message;
        return esperas.RegistrarAsync(m.TrabajoId, m.SolicitudId, m.LineaId, m.Motivo, contexto.CancellationToken);
    }
}
