using CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Mensajes.Costeo;

namespace CabinaFletes.Application.CasosDeUso.Costeo;

/// <summary>Costear una línea del pliego y contar el desenlace a quien orquesta.</summary>
public sealed record CostearLineaCommand(Guid TrabajoId, Guid SolicitudId, Guid LineaId) : ICommand<Unit>;

/// <summary>
/// Aplica la regla de costeo a una línea y publica el desenlace.
///
/// Hay tres, y cada uno va a un sitio distinto porque confundirlos tiene consecuencias:
///
///   Con propuesta        -> LineaCosteada. La línea queda auto-resuelta con su fuente.
///   Sin tarifa válida    -> LineaRequiereTarifaManual. NO es un fallo: el servicio hizo
///                           su trabajo y hace falta el Administrador de Tarifas.
///   Fuente caída         -> la excepción SUBE. MassTransit reintenta; si se agotan los
///                           reintentos, el consumidor de fallos lo convierte en
///                           LineaFalloTecnico.
///
/// Ese tercer caso es la razón de no envolver esto en un try/catch. Tragarse la
/// excepción convertiría un fallo transitorio en un pendiente definitivo sin haber
/// reintentado ni una vez, y perdería el paso a la cola de error que pide el criterio 3.
/// </summary>
public sealed class CostearLineaCommandHandler(
    IPropuestaDeLineaPort propuesta,
    IPublicadorDeMensajes publicador,
    IPendientesDeCosteoPort pendientes,
    ILineasCosteadasPort lineas,
    IUnitOfWork uow)
    : ICommandHandler<CostearLineaCommand, Unit>
{
    public async Task<Unit> Handle(CostearLineaCommand command, CancellationToken ct = default)
    {
        var resultado = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);

        if (resultado.RequiereTarifaManual)
        {
            await pendientes.RegistrarAsync(
                command.TrabajoId, command.SolicitudId, command.LineaId,
                TipoPendienteDeCosteo.Manual, resultado.Justificacion, ct);

            await publicador.PublicarAsync(new LineaRequiereTarifaManual(
                command.TrabajoId, command.SolicitudId, command.LineaId, resultado.Justificacion), ct);

            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);

        await publicador.PublicarAsync(new LineaCosteada(
            command.TrabajoId, command.LineaId, resultado.Valor!.Value, resultado.Fuente!), ct);

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
