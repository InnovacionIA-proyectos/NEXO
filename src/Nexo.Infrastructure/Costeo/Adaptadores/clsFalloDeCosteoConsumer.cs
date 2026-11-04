using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>Convierte un costeo que agotó sus reintentos (<c>Fault&lt;T&gt;</c> de MassTransit) en un desenlace de negocio,
/// para que la saga no espere hasta su timeout. Guarda la excepción real, no un texto genérico, para saber qué fuente falló.</summary>
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
