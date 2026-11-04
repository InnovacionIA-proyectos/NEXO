using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>Al cargar o extender tarifas, vuelve a encolar las líneas manuales abiertas. Revisa TODAS en vez de emparejar tarifa y línea:
/// replicar la cascada de búsqueda del piso produciría falsos negativos (líneas esperando para siempre).
/// Las que sigan sin tarifa no escriben nada; si el volumen crece, acotar por ruta.</summary>
public sealed class clsRecosteoPorTarifasConsumer(
    IPendientesDeCosteoPort pendientes,
    ILogger<clsRecosteoPorTarifasConsumer> log)
    : IConsumer<clsTarifasActualizadas>
{
    public async Task Consume(ConsumeContext<clsTarifasActualizadas> contexto)
    {
        var abiertas = await pendientes.ManualesAbiertasAsync(contexto.CancellationToken);
        if (abiertas.Count == 0) return;

        foreach (var p in abiertas)
            await contexto.Publish(new clsRecostearLineaSolicitada(p.TrabajoId, p.SolicitudId, p.LineaId));

        log.LogInformation(
            "Tarifas actualizadas ({Motivo}): se encolaron {Cantidad} líneas para recosteo.",
            contexto.Message.Motivo, abiertas.Count);
    }
}
