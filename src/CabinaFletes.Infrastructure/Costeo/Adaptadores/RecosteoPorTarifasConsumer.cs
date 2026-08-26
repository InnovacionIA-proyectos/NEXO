using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Costeo.Adaptadores;

/// <summary>
/// Cuando se cargan o extienden tarifas, vuelve a poner en cola las líneas que estaban
/// esperando intervención humana.
///
/// Revisa TODAS las manuales abiertas, no un subconjunto adivinado. Emparejar la tarifa
/// nueva con las líneas que la esperaban exigiría replicar la cascada de búsqueda del
/// piso, y una réplica que se desvíe produce falsos negativos: líneas que se quedan
/// esperando para siempre porque el emparejamiento decidió que no era su tarifa.
///
/// El costo es acotado: las que sigan sin tarifa no escriben nada en la base, y el
/// número de pendientes manuales está limitado por lo que una persona puede acumular.
/// Si algún día ese número crece de verdad, la salida es acotar por ruta, no adivinar.
///
/// Se registra cuántas se encolaron: un recosteo silencioso que no dice cuánto movió es
/// imposible de auditar cuando alguien pregunte por qué su línea se costeó sola.
/// </summary>
public sealed class RecosteoPorTarifasConsumer(
    IPendientesDeCosteoPort pendientes,
    ILogger<RecosteoPorTarifasConsumer> log)
    : IConsumer<TarifasActualizadas>
{
    public async Task Consume(ConsumeContext<TarifasActualizadas> contexto)
    {
        var abiertas = await pendientes.ManualesAbiertasAsync(contexto.CancellationToken);
        if (abiertas.Count == 0) return;

        foreach (var p in abiertas)
            await contexto.Publish(new RecostearLineaSolicitada(p.TrabajoId, p.SolicitudId, p.LineaId));

        log.LogInformation(
            "Tarifas actualizadas ({Motivo}): se encolaron {Cantidad} líneas para recosteo.",
            contexto.Message.Motivo, abiertas.Count);
    }
}
