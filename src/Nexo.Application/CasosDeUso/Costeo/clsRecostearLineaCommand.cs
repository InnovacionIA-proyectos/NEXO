using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Costeo;

/// <summary>Volver a costear una línea que estaba esperando una tarifa.</summary>
public sealed record clsRecostearLineaCommand(Guid TrabajoId, Guid SolicitudId, Guid LineaId) : ICommand<Unit>;

/// <summary>
/// Vuelve a preguntar a la regla: si la línea ya se puede guardar sola la resuelve, agrega el flete a la cotización y cierra
/// el pendiente; si no, la deja exactamente como estaba (cerrar sin resolver la volvería invisible).
/// No publica nada a la saga: aquella corrida ya cerró en Parcial con razón.
/// </summary>
public sealed class clsRecostearLineaCommandHandler(
    IPropuestaDeLineaPort propuesta,
    IPendientesDeCosteoPort pendientes,
    ILineasCosteadasPort lineas,
    ICotizacionRepository cotizaciones,
    IUnitOfWork uow)
    : ICommandHandler<clsRecostearLineaCommand, Unit>
{
    public async Task<Unit> Handle(clsRecostearLineaCommand command, CancellationToken ct = default)
    {
        // Una línea rechazada a propósito no se re-costea.
        if (await lineas.EstaRechazadaAsync(command.LineaId, ct)) return Unit.Value;

        var decision = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);

        // Sigue sin poder guardarse sola (sin valor o con motivo de pendiente): se queda esperando, sin escribir nada.
        if (!decision.Autocosteable) return Unit.Value;

        var resultado = decision.Resultado;
        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);
        await AgregarFleteAutoCosteoAsync(cotizaciones, command.SolicitudId, command.LineaId, decision, ct);
        await pendientes.MarcarResueltoAsync(command.TrabajoId, command.LineaId, TipoPendienteDeCosteo.Manual, ct);
        await uow.SaveChangesAsync(ct);

        return Unit.Value;
    }

    private static async Task AgregarFleteAutoCosteoAsync(
        ICotizacionRepository cotizacionesRepo, Guid solicitudId, Guid lineaId, clsDecisionDeFlujo decision, CancellationToken ct)
    {
        var resultado = decision.Resultado;
        var cotizacion = await cotizacionesRepo.ObtenerConLineasYConexosAsync(
            c => c.SolicitudId == solicitudId && c.EsAutoCosteo && c.Estado == EstadoCotizacion.Borrador, ct);
        if (cotizacion is null)
        {
            cotizacion = new clsCotizacion { SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador, EsAutoCosteo = true };
            cotizacionesRepo.Agregar(cotizacion);
        }

        if (cotizacion.Lineas.Any(l => l.LineaSolicitudId == lineaId)) return;

        cotizacion.Lineas.Add(new clsCotizacionLinea
        {
            CotizacionId = cotizacion.Id,
            LineaSolicitudId = lineaId,
            ValorFlete = resultado.Valor!.Value,
            ValorAprovisionamiento = decision.ValorAprovisionamiento,
            ValorRetornoContenedor = decision.ValorRetornoContenedor,
            Fuente = resultado.Fuente switch
            {
                "sicetac" => FuenteFlete.Sicetac,
                "plaza" => FuenteFlete.Tarifario,
                "historico_sap" => FuenteFlete.Historica,
                _ => FuenteFlete.Manual,
            },
        });
        cotizacion.Total = cotizacion.Lineas.Sum(l => l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
    }
}
