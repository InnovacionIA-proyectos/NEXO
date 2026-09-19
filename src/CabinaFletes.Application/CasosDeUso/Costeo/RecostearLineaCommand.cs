using CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Costeo;

/// <summary>Volver a costear una línea que estaba esperando una tarifa.</summary>
public sealed record RecostearLineaCommand(Guid TrabajoId, Guid SolicitudId, Guid LineaId) : ICommand<Unit>;

/// <summary>
/// Le vuelve a preguntar a la regla. Si ya hay tarifa, resuelve la línea y cierra el
/// pendiente; si no, lo deja exactamente como estaba.
///
/// Ese "si no, no toca nada" es lo importante. Cerrar el pendiente sin haber resuelto
/// dejaría la línea invisible: desaparecería de la cola del Administrador de Tarifas y
/// no se costearía nunca. Y como esto corre sobre todos los pendientes cada vez que se
/// cargan tarifas, tampoco escribe en la base cuando no hay nada que cambiar.
///
/// NO publica nada hacia la saga a propósito. Ese trabajo ya cerró, y cerró en Parcial
/// con razón: en ese momento lo estaba. El recosteo arregla la línea, no reescribe el
/// histórico de aquella corrida.
/// </summary>
public sealed class RecostearLineaCommandHandler(
    IPropuestaDeLineaPort propuesta,
    IPendientesDeCosteoPort pendientes,
    ILineasCosteadasPort lineas,
    IUnitOfWork uow)
    : ICommandHandler<RecostearLineaCommand, Unit>
{
    public async Task<Unit> Handle(RecostearLineaCommand command, CancellationToken ct = default)
    {
        var resultado = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);

        // Sigue sin tarifa: se queda esperando, sin escribir nada.
        if (resultado.RequiereTarifaManual) return Unit.Value;

        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);
        await pendientes.MarcarResueltoAsync(command.TrabajoId, command.LineaId, TipoPendienteDeCosteo.Manual, ct);
        await uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
