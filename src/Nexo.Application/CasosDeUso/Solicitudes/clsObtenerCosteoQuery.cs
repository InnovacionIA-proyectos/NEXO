using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
public sealed record clsObtenerCosteoQuery(Guid SolicitudId) : IQuery<clsSolicitudCosteoDto>;

public sealed class clsObtenerCosteoQueryHandler(
    ISolicitudesRepository solicitudes, ITarifasCosteoPort tarifas, TimeProvider clock, IUnitOfWork uow,
    ICurrentUserPort usuarioActual, IClavesSoloSicetacPort reglasSoloSicetac)
    : IQueryHandler<clsObtenerCosteoQuery, clsSolicitudCosteoDto>
{
    public async Task<clsSolicitudCosteoDto> Handle(clsObtenerCosteoQuery query, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerParaCosteoAsync(query.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");

        // Aislamiento por comercial: un Comercial no puede abrir la solicitud de otro, aunque tenga el Id.
        if (usuarioActual.Rol == RolUsuario.Comercial && s.CreadoPor != usuarioActual.Id)
            throw new clsForbiddenException("SOLICITUD_AJENA", "No tiene acceso a esta solicitud.");

        if (s.Estado == EstadoSolicitud.Descartada)
            throw new clsConflictException("SOLICITUD_DESCARTADA", "La solicitud está descartada y no puede costearse.");

        var ahora = clock.GetUtcNow();

        var claves = await reglasSoloSicetac.ClavesRutaVehiculoAsync(ct);
        var lineas = new List<clsLineaCosteoDto>();
        // Orden ESTABLE: NumeroLinea ascendente (las líneas sin número al final), luego fecha de creación y Id. NEXO numera por el orden del
        // arreglo de la captura.
        var ordenadas = s.Lineas
            .OrderBy(l => l.NumeroLinea is null).ThenBy(l => l.NumeroLinea)
            .ThenBy(l => l.CreatedAt).ThenBy(l => l.Id);
        foreach (var l in ordenadas)
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
                clsPosicionCargaDto.Desde(l.Dimensiones), piso, historicas, l.HorasCargue, l.HorasDescargue,
                l.GrupoRutaId, l.IdRecomendacionVehiculo, l.ValorMercancia, l.NumeroLinea,
                // Para que la Mesa sepa cuándo NO ofrecer "Editar flete" (con regla responde 409).
                clsReglaSoloSicetacDeLinea.Tiene(
                    claves, l.ClaseViaje, l.clsRuta?.OrigenMunicipio?.CodigoDane, l.clsRuta?.DestinoMunicipio?.CodigoDane,
                    l.clsConfiguracionVehiculo?.TipoVehiculo),
                clsTexto.MinusculaInicial((EstadoLinea?)l.EstadoLinea), l.MotivoPendiente,
                clsTexto.MinusculaInicial(l.LineaNegocio), clsTexto.MinusculaInicial(l.ClaseViaje),
                l.ClaseViaje == ClaseViaje.Contenedor ? l.ClaseContenedor : null,
                l.clsRuta?.OrigenMunicipioId, l.clsRuta?.DestinoMunicipioId, l.MotivoRechazo));
        }

        // Al abrir, marca la solicitud en costeo (si estaba pendiente).
        if (s.Estado == EstadoSolicitud.Pendiente)
        {
            s.Estado = EstadoSolicitud.EnCosteo;
            await uow.SaveChangesAsync(ct);
        }

        var tiempos = clsCondicionesServicioDto.Desde(s.CondicionesServicio).TiemposLogisticos
            ?? clsTiemposLogisticosDto.PorDefecto;
        // Si el piso de TODAS las líneas faltó porque DWSice no trae el período del mes actual, se dice (en vez de un "sin piso" mudo); solo se
        // consulta si alguna línea quedó sin piso.
        var avisoSicetac = lineas.Any(l => l.PisoSicetac is null) ? await tarifas.MotivoSicetacNoUsadoAsync(ct) : null;
        return new clsSolicitudCosteoDto(s.ToDto(), tiempos, lineas, avisoSicetac);
    }
}
