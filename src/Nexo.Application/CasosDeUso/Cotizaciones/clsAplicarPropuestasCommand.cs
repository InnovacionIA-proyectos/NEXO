using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Precarga como flete el valor propuesto por la Biblioteca en cada línea sin flete, con su aprovisionamiento y retorno del contenedor si la
/// propuesta los trae. Se saltan las líneas sin valor, con motivo de pendiente y las rechazadas.
/// <paramref name="SoloAutomaticas"/> se conserva por compatibilidad y no cambia el resultado.
/// </summary>
public sealed record clsAplicarPropuestasCommand(Guid CotizacionId, bool SoloAutomaticas = false) : ICommand<clsAplicarPropuestasResultDto>;

public sealed class clsAplicarPropuestasCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IPropuestaTarifaPort propuestas,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsAplicarPropuestasCommand, clsAplicarPropuestasResultDto>
{
    public async Task<clsAplicarPropuestasResultDto> Handle(clsAplicarPropuestasCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var conFlete = c.Lineas.Where(l => l.LineaSolicitudId != null).Select(l => l.LineaSolicitudId!.Value).ToHashSet();

        // Idempotencia: si TODAS las líneas ya tienen flete no hay nada que aplicar (evita reejecutar la propuesta, consulta pesada a los DW,
        // ante doble clic o reintento).
        var totalLineas = await solicitudes.ContarLineasAsync(c.SolicitudId, ct);
        if (conFlete.Count >= totalLineas)
            return new clsAplicarPropuestasResultDto(c.ToDto(), 0);

        var propuesta = await propuestas.ProponerAsync(c.SolicitudId, ct);
        var lineasSol = await lineasSolicitud.ObtenerPorSolicitudAsync(c.SolicitudId, ct);

        var agregadas = 0;
        var lineasCosteadas = new List<Guid>();
        foreach (var p in propuesta.Lineas)
        {
            if (p.ValorPropuesto is not { } valor || conFlete.Contains(p.LineaId)) continue;
            // Una línea con motivo de pendiente nunca se aplica en bloque: sin piso contra el cual comparar sería guardar un flete sin validar.
            if (!p.Autocosteable) continue;
            // Una línea rechazada no se costea (ni en bloque): se rechazó a propósito, sin flete.
            if (lineasSol.GetValueOrDefault(p.LineaId)?.EstadoLinea == Nexo.Domain.Enums.EstadoLinea.Rechazada) continue;
            var linea = new clsCotizacionLinea
            {
                CotizacionId = c.Id,
                LineaSolicitudId = p.LineaId,
                ValorFlete = valor,
                ValorAprovisionamiento = p.AprovisionamientoContenedor?.Valor,
                ValorRetornoContenedor = p.RetornoContenedor?.Valor,
                // ORIGENTARIFA: SICETAC o TARIFARIO según la fuente ganadora.
                Fuente = p.FuentePropuesta switch
                {
                    "sicetac" => FuenteFlete.Sicetac,
                    "plaza" => FuenteFlete.Tarifario,
                    "historico_sap" => FuenteFlete.Historica,
                    _ => FuenteFlete.Manual,
                },
            };
            c.Lineas.Add(linea);
            lineasCosteadas.Add(p.LineaId);
            agregadas++;
        }

        if (agregadas > 0)
        {
            clsCotizacionCalculos.Recalcular(c);
            auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "aplicar_propuestas", $"{{\"agregadas\":{agregadas}}}");
            await uow.SaveChangesAsync(ct);
            // Marca esas líneas como Revisada (gestión en la Mesa) para la bandeja.
            await lineasSolicitud.MarcarRevisadasAsync(lineasCosteadas, ct);
        }
        return new clsAplicarPropuestasResultDto(c.ToDto(), agregadas);
    }
}
