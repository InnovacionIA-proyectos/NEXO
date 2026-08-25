using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones;

/// <summary>Crear o recuperar la cotización (borrador) de una solicitud.</summary>
public sealed record CrearCotizacionCommand(Guid SolicitudId) : ICommand<CotizacionDto>;

public sealed class CrearCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<CrearCotizacionCommand, CotizacionDto>
{
    public async Task<CotizacionDto> Handle(CrearCotizacionCommand command, CancellationToken ct = default)
    {
        var solicitud = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new ValidationFailedException("SOLICITUD_INVALIDA", "La solicitud indicada no existe.");

        // Una solicitud YA ENTREGADA se abre sobre su cotización entregada (con sus fletes
        // y servicios), no sobre un borrador vacío: así la Mesa y la exportación muestran
        // el detalle real y no se crea una cotización en ceros.
        if (solicitud.Estado == EstadoSolicitud.Entregada)
        {
            var entregada = await cotizaciones.ObtenerConLineasYConexosAsync(
                c => c.SolicitudId == command.SolicitudId && c.Estado == EstadoCotizacion.Entregada, ct);
            if (entregada is not null) return entregada.ToDto();
        }

        // Reutiliza el borrador existente si lo hay (una cotización en curso por solicitud).
        var existente = await cotizaciones.ObtenerConLineasYConexosAsync(
            c => c.SolicitudId == command.SolicitudId && c.Estado == EstadoCotizacion.Borrador, ct);
        if (existente is not null) return existente.ToDto();

        var cotizacion = new Cotizacion { SolicitudId = command.SolicitudId, Estado = EstadoCotizacion.Borrador };
        cotizaciones.Agregar(cotizacion);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", cotizacion.Id, "crear", $"{{\"solicitud\":\"{command.SolicitudId}\"}}");
        await uow.SaveChangesAsync(ct);
        return cotizacion.ToDto();
    }
}
