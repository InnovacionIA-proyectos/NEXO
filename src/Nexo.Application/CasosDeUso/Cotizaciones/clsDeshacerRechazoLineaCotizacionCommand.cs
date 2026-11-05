using System.Text.Json;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Deshace el rechazo de una línea solo mientras la cotización NO esté entregada (409 COTIZACION_NO_EDITABLE). La línea vuelve a <c>PorCostear</c>
/// y se limpian motivo, quién y cuándo; el rechazo anterior queda en la auditoría.
/// </summary>
public sealed record clsDeshacerRechazoLineaCotizacionCommand(Guid CotizacionId, Guid LineaSolicitudId) : ICommand<clsLineaRechazoDto>;

public sealed class clsDeshacerRechazoLineaCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsDeshacerRechazoLineaCotizacionCommand, clsLineaRechazoDto>
{
    public async Task<clsLineaRechazoDto> Handle(clsDeshacerRechazoLineaCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE",
                "La cotización ya fue entregada: el rechazo ya no se puede deshacer.");

        var linea = await lineasSolicitud.ObtenerDeSolicitudAsync(command.LineaSolicitudId, c.SolicitudId, ct)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "La línea no pertenece a la solicitud de esta cotización.");
        if (linea.EstadoLinea != EstadoLinea.Rechazada)
            throw new clsConflictException("LINEA_NO_RECHAZADA", "La línea no está rechazada: no hay nada que deshacer.");

        var motivoAnterior = linea.MotivoRechazo;
        linea.EstadoLinea = EstadoLinea.PorCostear;
        linea.MotivoRechazo = null;
        linea.RechazadaPor = null;
        linea.RechazadaAt = null;
        linea.MotivoPendiente = "El rechazo se deshizo: la línea vuelve a estar por costear.";
        linea.UpdatedAt = clock.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "deshacer_rechazo_linea",
            JsonSerializer.Serialize(new { lineaSolicitudId = linea.Id, motivoAnterior }));
        await uow.SaveChangesAsync(ct);

        return new clsLineaRechazoDto(linea.Id, "porCostear", null, null);
    }
}
