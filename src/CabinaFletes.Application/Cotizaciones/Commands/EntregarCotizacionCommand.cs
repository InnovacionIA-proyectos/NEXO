using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.Cotizaciones.Dtos;
using CabinaFletes.Application.Cotizaciones.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Reglas;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.Cotizaciones.Commands;

/// <summary>
/// Entregar la cotización a Comercial: marca la entrega y envía la cotización
/// formal (PDF adjunto, si viene) por email al comercial responsable del cliente.
/// </summary>
public sealed record EntregarCotizacionCommand(
    Guid CotizacionId, byte[]? FormatoPdf, string? NombrePdf) : ICommand<EntregarCotizacionResultDto>;

public sealed class EntregarCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    IComercialResponsablePort comercialResponsable,
    ICorreoPort correo,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<EntregarCotizacionCommand, EntregarCotizacionResultDto>
{
    public async Task<EntregarCotizacionResultDto> Handle(EntregarCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new NotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado == EstadoCotizacion.Entregada)
            throw new ConflictException("COTIZACION_YA_ENTREGADA", "La cotización ya fue entregada.");
        if (c.Lineas.Count == 0)
            throw new ValidationFailedException("COTIZACION_SIN_FLETE", "No se puede entregar una cotización sin líneas de flete.");

        CotizacionCalculos.Recalcular(c);
        c.Estado = EstadoCotizacion.Entregada;
        c.EntregadaPor = usuarioActual.Id;
        c.EntregadaAt = clock.GetUtcNow();

        var solicitud = await solicitudes.ObtenerConClienteAsync(c.SolicitudId, ct);
        if (solicitud is not null)
        {
            if (!MaquinaEstados.PuedeTransicionar(solicitud.Estado, EstadoSolicitud.Entregada))
                throw new ConflictException("SOLICITUD_NO_TRANSICIONABLE",
                    $"La solicitud está {solicitud.Estado} y no puede pasar a Entregada.");
            solicitud.Estado = EstadoSolicitud.Entregada;
        }

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "entregar", $"{{\"total\":{c.Total}}}");
        await uow.SaveChangesAsync(ct);

        // La entrega ya quedó firme: el envío del email es best-effort y su falla
        // se informa (no revierte la entrega).
        var (enviado, nombre, correoDestino, mensaje) =
            await NotificarComercialAsync(c, solicitud, command.FormatoPdf, command.NombrePdf, ct);
        return new EntregarCotizacionResultDto(c.ToDto(), enviado, nombre, correoDestino, mensaje);
    }

    /// <summary>
    /// Busca el comercial responsable del cliente (DWCubosEBSSAP.DimCliente por NIT)
    /// y le envía la cotización formal con el PDF adjunto por el SMTP corporativo.
    /// </summary>
    private async Task<(bool Enviado, string? Nombre, string? Correo, string Mensaje)> NotificarComercialAsync(
        Cotizacion c, Solicitud? solicitud, byte[]? formatoPdf, string? nombrePdf, CancellationToken ct)
    {
        var cliente = solicitud?.Cliente;
        if (cliente is null)
            return (false, null, null, "Cotización entregada; sin cliente asociado para notificar.");
        if (!comercialResponsable.Configurado)
            return (false, null, null, "Cotización entregada; el DW del comercial responsable no está configurado (ConnectionStrings:DwCubosSap).");

        var responsable = await comercialResponsable.BuscarPorNitAsync(cliente.Nit.Trim(), ct);
        if (responsable is null)
            return (false, null, null, $"Cotización entregada; el cliente (NIT {cliente.Nit.Trim()}) no tiene comercial responsable con correo en DimCliente.");

        if (!correo.Configurado)
            return (false, responsable.Nombre, responsable.Correo,
                $"Cotización entregada; el SMTP corporativo no está configurado, no se envió a {responsable.Correo}.");

        try
        {
            var asunto = $"Cotización Nexo — {cliente.Nombre} — {c.Total:C0}";
            await correo.EnviarAsync(
                responsable.Correo.Trim(), asunto, CuerpoHtml(c, cliente, responsable.Nombre),
                formatoPdf, nombrePdf ?? $"cotizacion-{c.Id.ToString()[..8]}.pdf", ct);

            auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "email_comercial", $"{{\"para\":\"{responsable.Correo.Trim()}\"}}");
            await uow.SaveChangesAsync(ct);
            return (true, responsable.Nombre, responsable.Correo, $"Cotización entregada y enviada a {responsable.Correo.Trim()}.");
        }
        catch (Exception ex)
        {
            return (false, null, null, $"Cotización entregada; el envío del email falló: {ex.Message}");
        }
    }

    private string CuerpoHtml(Cotizacion c, Cliente cliente, string nombreResponsable)
    {
        var subtotalFletes = c.Lineas.Sum(l => l.ValorFlete + (l.ValorAprovisionamiento ?? 0) + (l.ValorRetornoContenedor ?? 0));
        var subtotalConexos = c.Conexos.Sum(x => x.ValorAplicado);
        return $"""
            <p>Hola {nombreResponsable},</p>
            <p>Nexo entregó la cotización para <strong>{cliente.Nombre}</strong> (NIT {cliente.Nit}).
            El formato completo con todos los ítems va adjunto en PDF.</p>
            <table cellpadding="6" style="border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px">
              <tr><td>Fletes ({c.Lineas.Count})</td><td align="right">{subtotalFletes:C0}</td></tr>
              <tr><td>Servicios Logísticos ({c.Conexos.Count})</td><td align="right">{subtotalConexos:C0}</td></tr>
              <tr style="font-weight:bold;border-top:2px solid #0b5394"><td>Total cotización</td><td align="right">{c.Total:C0}</td></tr>
            </table>
            <p style="color:#777;font-size:12px">Correo automático de Nexo · Botero Soto — Soluciones Logísticas.</p>
            """;
    }
}
