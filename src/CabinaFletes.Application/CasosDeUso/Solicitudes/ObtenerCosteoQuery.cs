using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
public sealed record ObtenerCosteoQuery(Guid SolicitudId) : IQuery<SolicitudCosteoDto>;

public sealed class ObtenerCosteoQueryHandler(
    ISolicitudesRepository solicitudes, ITarifasCosteoPort tarifas, TimeProvider clock, IUnitOfWork uow)
    : IQueryHandler<ObtenerCosteoQuery, SolicitudCosteoDto>
{
    public async Task<SolicitudCosteoDto> Handle(ObtenerCosteoQuery query, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerParaCosteoAsync(query.SolicitudId, ct)
            ?? throw new NotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        if (s.Estado == EstadoSolicitud.Descartada)
            throw new ConflictException("SOLICITUD_DESCARTADA", "La solicitud está descartada y no puede costearse.");

        var ahora = clock.GetUtcNow();
        var periodoActual = ahora.Year * 100 + ahora.Month;

        var lineas = new List<LineaCosteoDto>();
        foreach (var l in s.Lineas)
        {
            var piso = l.RutaId is { } rutaId ? await tarifas.PisoVigenteAsync(rutaId, periodoActual, ct) : null;
            var historicas = l.RutaId is { } rutaId2
                ? await tarifas.HistoricasSugeridasAsync(rutaId2, l.Tipologia, ahora, 5, ct)
                : [];

            lineas.Add(new LineaCosteoDto(
                l.Id, l.RutaId, l.Ruta == null ? null : $"{l.Ruta.Origen} → {l.Ruta.Destino}",
                l.Tipologia, l.Peso, l.ConfiguracionVehiculo?.TipoVehiculo, l.ConfiguracionVehiculo?.Carroceria,
                PosicionCargaDto.Desde(l.Dimensiones), piso, historicas));
        }

        // Al abrir, marca la solicitud en costeo (si estaba pendiente).
        if (s.Estado == EstadoSolicitud.Pendiente)
        {
            s.Estado = EstadoSolicitud.EnCosteo;
            await uow.SaveChangesAsync(ct);
        }

        var tiempos = CondicionesServicioDto.Desde(s.CondicionesServicio).TiemposLogisticos
            ?? TiemposLogisticosDto.PorDefecto;
        return new SolicitudCosteoDto(s.ToDto(), tiempos, lineas);
    }
}
