using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Excepciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Domain.Reglas;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Entregar la cotización a Comercial.
///
/// NO MANDA EL CORREO, y ese es el cambio de la HU-06.1. Marca la entrega, publica el
/// evento y responde. El correo lo manda un consumidor aparte.
///
/// Antes el envío ocurría aquí mismo, después de guardar. Ya era best-effort —un fallo no
/// revertía la entrega— pero quien entregaba esperaba a que el relay respondiera: un
/// correo lento se sentía como una aplicación lenta, y el relay caído dejaba a la persona
/// mirando una pantalla girando por algo que no tenía nada que ver con su trabajo.
///
/// El evento se publica en la MISMA transacción que la entrega, vía Outbox. Esa es la
/// garantía que reemplaza al envío síncrono: si la entrega quedó firme, la notificación
/// está pendiente de salir; y si la entrega se revirtió, no se notifica nada.
/// </summary>
public sealed record clsEntregarCotizacionCommand(
    Guid CotizacionId, byte[]? FormatoPdf, string? NombrePdf) : ICommand<clsEntregarCotizacionResultDto>;

public sealed class clsEntregarCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsEntregarCotizacionCommand, clsEntregarCotizacionResultDto>
{
    public async Task<clsEntregarCotizacionResultDto> Handle(clsEntregarCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado == EstadoCotizacion.Entregada)
            throw new clsConflictException("COTIZACION_YA_ENTREGADA", "La cotización ya fue entregada.");
        if (c.Lineas.Count == 0)
            throw new clsCotizacionSinFleteException("entregar");

        clsCotizacionCalculos.Recalcular(c);
        c.Estado = EstadoCotizacion.Entregada;
        c.EntregadaPor = usuarioActual.Id;
        c.EntregadaAt = clock.GetUtcNow();

        var solicitud = await solicitudes.ObtenerConClienteAsync(c.SolicitudId, ct);
        if (solicitud is not null)
        {
            if (!clsMaquinaEstados.PuedeTransicionar(solicitud.Estado, EstadoSolicitud.Entregada))
                throw new clsConflictException("SOLICITUD_NO_TRANSICIONABLE",
                    $"La solicitud está {solicitud.Estado} y no puede pasar a Entregada.");
            solicitud.Estado = EstadoSolicitud.Entregada;
        }

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "entregar", $"{{\"total\":{c.Total}}}");

        // Se publica ANTES de guardar: con el Outbox eso es lo que mete el evento en la
        // misma transacción. Al revés podría quedar la entrega firme sin notificación.
        //
        // El formato viaja en el mensaje porque es la única pieza que no está en la base:
        // llega como archivo en esta petición y no se persiste en ningún lado.
        await publicador.PublicarAsync(
            new clsCotizacionEntregada(
                c.Id, c.SolicitudId, usuarioActual.Id, c.EntregadaAt!.Value,
                command.FormatoPdf, command.NombrePdf),
            ct);

        await uow.SaveChangesAsync(ct);

        return new clsEntregarCotizacionResultDto(
            c.ToDto(),
            EstadoNotificacion: "encolada",
            Mensaje: "Cotización entregada. La notificación por correo sale en segundo plano.");
    }
}
