using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Costeo;

namespace Nexo.Application.CasosDeUso.Costeo;

/// <summary>Costear una línea del pliego y contar el desenlace a quien orquesta.</summary>
public sealed record clsCostearLineaCommand(Guid TrabajoId, Guid SolicitudId, Guid LineaId) : ICommand<Unit>;

/// <summary>
/// Aplica la regla de costeo a una línea y publica el desenlace. Hay tres, y cada uno va a un sitio distinto porque
/// confundirlos tiene consecuencias:
///
///   Autocosteable (hay valor y ningún motivo de pendiente, en cualquiera de los tres caminos de decisión)
///                         -> clsLineaCosteada. La línea queda auto-resuelta CON su flete (y su retorno/aprovisionamiento)
///                            ya en la cotización — ver AgregarFleteAutoCosteoAsync.
///   No autocosteable      -> clsLineaRequiereTarifaManual. NO es un fallo: el servicio hizo su trabajo y hace falta el
///                            Administrador de Tarifas (ningún valor, o un motivo que impide guardarlo solo).
///   Fuente caída          -> la excepción SUBE. MassTransit reintenta; si se agotan los reintentos, el consumidor de fallos
///                            lo convierte en clsLineaFalloTecnico.
///
/// Ese tercer caso es la razón de no envolver esto en un try/catch. Tragarse la excepción convertiría un fallo transitorio
/// en un pendiente definitivo sin haber reintentado ni una vez, y perdería el paso a la cola de error.
/// </summary>
public sealed class clsCostearLineaCommandHandler(
    IPropuestaDeLineaPort propuesta,
    IPublicadorDeMensajes publicador,
    IPendientesDeCosteoPort pendientes,
    ILineasCosteadasPort lineas,
    ICotizacionRepository cotizaciones,
    IUnitOfWork uow)
    : ICommandHandler<clsCostearLineaCommand, Unit>
{
    public async Task<Unit> Handle(clsCostearLineaCommand command, CancellationToken ct = default)
    {
        // Una línea rechazada a propósito no se costea (podría llegar un mensaje encolado antes del rechazo).
        if (await lineas.EstaRechazadaAsync(command.LineaId, ct)) return Unit.Value;

        var decision = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);
        var resultado = decision.Resultado;
        // Un motivo propio (línea de Cupo sin vehículo, tramo vacío sin piso...) pesa más que el texto largo de la justificación.
        var motivo = decision.MotivoPendiente ?? resultado.Justificacion;

        if (!decision.Autocosteable)
        {
            await pendientes.RegistrarAsync(
                command.TrabajoId, command.SolicitudId, command.LineaId,
                TipoPendienteDeCosteo.Manual, motivo, ct);

            await lineas.MarcarPendienteAsync(command.LineaId, motivo, ct);

            await publicador.PublicarAsync(new clsLineaRequiereTarifaManual(
                command.TrabajoId, command.SolicitudId, command.LineaId, motivo), ct);

            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        // Además de marcar la línea resuelta, el flete queda en la cotización — igual que la captura
        // (clsCapturarSolicitudCommand.AutoCostearAsync). Sin esto, la línea quedaría "AutoResuelta" pero sin precio.
        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);
        await AgregarFleteAutoCosteoAsync(cotizaciones, command.SolicitudId, command.LineaId, decision, ct);

        await publicador.PublicarAsync(new clsLineaCosteada(
            command.TrabajoId, command.LineaId, resultado.Valor!.Value, resultado.Fuente!), ct);

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>
    /// Agrega a la cotización auto-costeada el flete de la línea, con su aprovisionamiento y su retorno del contenedor si la
    /// decisión los trae. Mismo criterio que clsCapturarSolicitudCommand.AutoCostearAsync.
    /// </summary>
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

        // Recosteo de una línea que ya estaba en la cotización auto-costeada (reintento tras un fallo técnico, por ejemplo):
        // no duplicar.
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
