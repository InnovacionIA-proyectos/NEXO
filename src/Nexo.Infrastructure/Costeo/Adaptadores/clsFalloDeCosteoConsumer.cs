using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>
/// Convierte un costeo que agotó sus reintentos en un desenlace de negocio.
///
/// MassTransit publica Fault&lt;T&gt; cuando un consumidor falla después de todos los
/// reintentos, y manda el mensaje original a la cola de error. Sin esto, el pliego se
/// quedaría esperando esa línea hasta que venciera el timeout de la saga: la línea
/// habría fallado y nadie se habría enterado.
///
/// Aquí es donde el fallo pasa a ser información. Los reintentos son invisibles —así
/// deben ser, un hipo del DW no es noticia—; agotarlos sí es un hecho que la saga tiene
/// que contar y que la pantalla tiene que mostrar.
///
/// El motivo que se guarda es la excepción real, no un texto genérico: quien diagnostique
/// necesita saber QUÉ fuente falló y cómo.
/// </summary>
public sealed class clsFalloDeCosteoConsumer(
    IPendientesDeCosteoPort pendientes,
    ILineasCosteadasPort lineas,
    IUnitOfWork uow)
    : IConsumer<Fault<clsCostearLineaSolicitada>>
{
    public async Task Consume(ConsumeContext<Fault<clsCostearLineaSolicitada>> contexto)
    {
        var original = contexto.Message.Message;
        var excepcion = contexto.Message.Exceptions.FirstOrDefault();
        var motivo = excepcion is null
            ? "El costeo de la línea falló y agotó sus reintentos."
            : $"{excepcion.ExceptionType}: {excepcion.Message}";

        await pendientes.RegistrarAsync(
            original.TrabajoId, original.SolicitudId, original.LineaId,
            TipoPendienteDeCosteo.Tecnica, motivo, contexto.CancellationToken);

        await lineas.MarcarPendienteAsync(original.LineaId, motivo, contexto.CancellationToken);

        await uow.SaveChangesAsync(contexto.CancellationToken);

        // La saga cuenta este desenlace y puede cerrar el trabajo sin esperar el timeout.
        await contexto.Publish(new clsLineaFalloTecnico(original.TrabajoId, original.LineaId, motivo));
    }
}
