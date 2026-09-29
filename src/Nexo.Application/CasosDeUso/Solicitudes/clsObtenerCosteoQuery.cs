using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
public sealed record clsObtenerCosteoQuery(Guid SolicitudId) : IQuery<clsSolicitudCosteoDto>;

public sealed class clsObtenerCosteoQueryHandler(
    ISolicitudesRepository solicitudes, ITarifasCosteoPort tarifas, TimeProvider clock, IUnitOfWork uow,
    ICurrentUserPort usuarioActual)
    : IQueryHandler<clsObtenerCosteoQuery, clsSolicitudCosteoDto>
{
    public async Task<clsSolicitudCosteoDto> Handle(clsObtenerCosteoQuery query, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerParaCosteoAsync(query.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");

        // Aislamiento por comercial (issue #199, S4-HU-19.1): un Comercial no puede abrir
        // la solicitud de otro comercial, aunque tenga el Id. El resto de los roles
        // (Mesa de Costeo/CoordinadorFletes, Admin, etc.) no tienen esta restricción.
        if (usuarioActual.Rol == RolUsuario.Comercial && s.CreadoPor != usuarioActual.Id)
            throw new clsForbiddenException("SOLICITUD_AJENA", "No tiene acceso a esta solicitud.");

        if (s.Estado == EstadoSolicitud.Descartada)
            throw new clsConflictException("SOLICITUD_DESCARTADA", "La solicitud está descartada y no puede costearse.");

        var ahora = clock.GetUtcNow();

        var lineas = new List<clsLineaCosteoDto>();
        foreach (var l in s.Lineas)
        {
            var piso = l.clsRuta is { } ruta
                ? await tarifas.PisoVigenteAsync(
                    ruta.Origen, ruta.Destino, l.clsConfiguracionVehiculo?.TipoVehiculo, l.Tipologia,
                    l.clsConfiguracionVehiculo?.Carroceria, ct)
                : null;
            var historicas = l.RutaId is { } rutaId2
                ? await tarifas.HistoricasSugeridasAsync(rutaId2, l.Tipologia, ahora, 5, ct)
                : [];

            lineas.Add(new clsLineaCosteoDto(
                l.Id, l.RutaId, l.clsRuta == null ? null : $"{l.clsRuta.Origen} → {l.clsRuta.Destino}",
                l.Tipologia, l.Peso, l.clsConfiguracionVehiculo?.TipoVehiculo, l.clsConfiguracionVehiculo?.Carroceria,
                clsPosicionCargaDto.Desde(l.Dimensiones), piso, historicas));
        }

        // Al abrir, marca la solicitud en costeo (si estaba pendiente).
        if (s.Estado == EstadoSolicitud.Pendiente)
        {
            s.Estado = EstadoSolicitud.EnCosteo;
            await uow.SaveChangesAsync(ct);
        }

        var tiempos = clsCondicionesServicioDto.Desde(s.CondicionesServicio).TiemposLogisticos
            ?? clsTiemposLogisticosDto.PorDefecto;
        return new clsSolicitudCosteoDto(s.ToDto(), tiempos, lineas);
    }
}
