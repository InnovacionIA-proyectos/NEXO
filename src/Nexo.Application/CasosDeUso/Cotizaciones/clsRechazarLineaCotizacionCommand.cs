using System.Text.Json;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// S6-HU-24.3/#320: rechazar UNA línea de la cotización con un motivo, para entregar el resto al comercial sin que una línea sin costear
/// frene la entrega. Antes solo existía rechazar la solicitud COMPLETA (<c>Descartada</c>).
///
/// Reglas:
///  · Solo cotización Borrador (una entregada no se toca): 409 COTIZACION_NO_EDITABLE.
///  · El motivo es obligatorio (hasta 500): 400 MOTIVO_RECHAZO_REQUERIDO.
///  · La línea tiene que ser de esta solicitud (404) y estar SIN costear: una línea que ya tiene flete en la cotización no se rechaza
///    (409 LINEA_CON_FLETE) — así una línea rechazada nunca tiene fila de cotización y no puede sumar al total ni salir con precio.
///    Una ya rechazada: 409 LINEA_YA_RECHAZADA.
///  · Queda en la auditoría con quién y con qué motivo.
/// </summary>
public sealed record clsRechazarLineaCotizacionCommand(Guid CotizacionId, Guid LineaSolicitudId, string? Motivo)
    : ICommand<clsLineaRechazoDto>;

public sealed class clsRechazarLineaCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsRechazarLineaCotizacionCommand, clsLineaRechazoDto>
{
    public const int MaxMotivo = 500;

    public async Task<clsLineaRechazoDto> Handle(clsRechazarLineaCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var motivo = clsTexto.Limpiar(command.Motivo);
        if (motivo is null)
            throw new clsValidationFailedException("MOTIVO_RECHAZO_REQUERIDO", "Indica el motivo por el que se rechaza la línea.");
        if (motivo.Length > MaxMotivo)
            throw new clsValidationFailedException("MOTIVO_RECHAZO_REQUERIDO",
                $"El motivo admite máximo {MaxMotivo} caracteres (llegaron {motivo.Length}).");

        var linea = await lineasSolicitud.ObtenerDeSolicitudAsync(command.LineaSolicitudId, c.SolicitudId, ct)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "La línea no pertenece a la solicitud de esta cotización.");

        if (linea.EstadoLinea == EstadoLinea.Rechazada)
            throw new clsConflictException("LINEA_YA_RECHAZADA", "La línea ya está rechazada. Deshaga el rechazo antes de cambiar el motivo.");
        if (c.Lineas.Any(l => l.LineaSolicitudId == linea.Id))
            throw new clsConflictException("LINEA_CON_FLETE",
                "La línea ya tiene un flete en la cotización: solo se puede rechazar una línea que está sin costear.");

        linea.EstadoLinea = EstadoLinea.Rechazada;
        linea.MotivoRechazo = motivo;
        linea.RechazadaPor = usuarioActual.Id;
        linea.RechazadaAt = clock.GetUtcNow();
        linea.MotivoPendiente = null;   // ya no está "pendiente": se decidió no costearla
        linea.UpdatedAt = clock.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "rechazar_linea",
            JsonSerializer.Serialize(new { lineaSolicitudId = linea.Id, motivo }));
        await uow.SaveChangesAsync(ct);

        return new clsLineaRechazoDto(linea.Id, "rechazada", motivo, linea.RechazadaAt);
    }
}
