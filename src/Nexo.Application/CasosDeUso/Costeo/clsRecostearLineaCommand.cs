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
/// Le vuelve a preguntar a la regla. Si ahora resuelve en modo automático (SoloSicetac/
/// PlazaVigenteMayor), resuelve la línea, le agrega el flete a la cotización auto-costeada
/// y cierra el pendiente; si sigue en ConsultaCompleta (S5-HU-19.2/#282 — incluye el caso
/// de un solo candidato, que ya no se auto-aplica), lo deja exactamente como estaba.
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
        var decision = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);

        // Sigue en ConsultaCompleta: se queda esperando, sin escribir nada.
        if (decision.Modo == ModoDecision.ConsultaCompleta) return Unit.Value;

        var resultado = decision.Resultado;
        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);
        await AgregarFleteAutoCosteoAsync(cotizaciones, command.SolicitudId, command.LineaId, resultado, ct);
        await pendientes.MarcarResueltoAsync(command.TrabajoId, command.LineaId, TipoPendienteDeCosteo.Manual, ct);
        await uow.SaveChangesAsync(ct);

        return Unit.Value;
    }

    /// <summary>Mismo criterio que clsCostearLineaCommand — ver su doc-comment.</summary>
    private static async Task AgregarFleteAutoCosteoAsync(
        ICotizacionRepository cotizacionesRepo, Guid solicitudId, Guid lineaId, clsResultadoDeCosteo resultado, CancellationToken ct)
    {
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
