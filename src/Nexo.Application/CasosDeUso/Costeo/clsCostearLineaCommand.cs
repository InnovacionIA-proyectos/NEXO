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
/// Aplica la regla de costeo a una línea y publica el desenlace.
///
/// Hay tres, y cada uno va a un sitio distinto porque confundirlos tiene consecuencias:
///
///   Automático (SoloSicetac/PlazaVigenteMayor) -> clsLineaCosteada. La línea queda
///                           auto-resuelta CON su flete ya en la cotización — ver
///                           AgregarFleteAutoCosteoAsync.
///   ConsultaCompleta      -> clsLineaRequiereTarifaManual. NO es un fallo: el servicio
///                           hizo su trabajo y hace falta el Administrador de Tarifas,
///                           aunque <c>Resultado.Valor</c> no sea null (un solo candidato
///                           en consulta completa SÍ tiene valor, pero no se auto-aplica
///                           — S5-HU-19.2/#282).
///   Fuente caída         -> la excepción SUBE. MassTransit reintenta; si se agotan los
///                           reintentos, el consumidor de fallos lo convierte en
///                           clsLineaFalloTecnico.
///
/// Ese tercer caso es la razón de no envolver esto en un try/catch. Tragarse la
/// excepción convertiría un fallo transitorio en un pendiente definitivo sin haber
/// reintentado ni una vez, y perdería el paso a la cola de error que pide el criterio 3.
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
        var decision = await propuesta.ProponerParaLineaAsync(command.LineaId, ct);
        var resultado = decision.Resultado;

        if (decision.Modo == ModoDecision.ConsultaCompleta)
        {
            await pendientes.RegistrarAsync(
                command.TrabajoId, command.SolicitudId, command.LineaId,
                TipoPendienteDeCosteo.Manual, resultado.Justificacion, ct);

            await lineas.MarcarPendienteAsync(command.LineaId, resultado.Justificacion, ct);

            await publicador.PublicarAsync(new clsLineaRequiereTarifaManual(
                command.TrabajoId, command.SolicitudId, command.LineaId, resultado.Justificacion), ct);

            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        // SoloSicetac / PlazaVigenteMayor: automático de verdad. S5-HU-19.2/#282: además
        // de marcar la línea resuelta, el flete queda en la cotización — igual que la
        // captura (clsCapturarSolicitudCommand.AutoCostearAsync). Sin esto, la línea
        // quedaría "AutoResuelta" pero sin precio hasta que alguien clic "Aplicar todas
        // las propuestas".
        await lineas.MarcarAutoResueltaAsync(command.LineaId, resultado.Valor!.Value, resultado.Fuente!, ct);
        await AgregarFleteAutoCosteoAsync(cotizaciones, command.SolicitudId, command.LineaId, resultado, ct);

        await publicador.PublicarAsync(new clsLineaCosteada(
            command.TrabajoId, command.LineaId, resultado.Valor!.Value, resultado.Fuente!), ct);

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    /// <summary>
    /// Mismo criterio que clsCapturarSolicitudCommand.AutoCostearAsync, con una
    /// diferencia deliberada: acá no hay acceso al <see cref="clsPropuestaLineaDto"/>
    /// completo (el puerto por línea solo expone el resultado de dominio), así que
    /// ValorAprovisionamiento/ValorRetornoContenedor quedan null — un pliego cargado por
    /// Excel/lineas:bulk tampoco los trae hoy (ver clsCrearLineaInput), así que no es una
    /// pérdida real para este camino.
    /// </summary>
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

        // Recosteo de una línea que ya estaba en la cotización auto-costeada (reintento
        // tras un fallo técnico, por ejemplo): no duplicar.
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
