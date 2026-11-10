using System.Text.Json;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Cambia el vehículo de una línea con UNO de dos datos: <paramref name="ConfigVehiculoId"/> (configuración oficial) o
/// <paramref name="TipoVehiculo"/> (+ <paramref name="Carroceria"/>), resuelto como en la captura; un texto que el diccionario no conoce NO
/// crea nada en cuarentena: 400 VEHICULO_NO_RECONOCIDO.
/// Solo cotización Borrador (409 COTIZACION_NO_EDITABLE), línea de la solicitud (404) y no rechazada (409 LINEA_RECHAZADA); solo AdminTarifas y Admin.
/// El flete previo se calculó con el vehículo anterior: se DESCARTA (y el total se recalcula) y la línea vuelve a <c>PorCostear</c>; el nuevo
/// entra por los caminos de siempre, que validan contra el piso del vehículo nuevo. Mismo vehículo: no hace nada (<c>CambioVehiculo = false</c>).
/// </summary>
public sealed record clsCambiarVehiculoLineaCommand(
    Guid CotizacionId, Guid LineaSolicitudId, Guid? ConfigVehiculoId, string? TipoVehiculo, string? Carroceria)
    : ICommand<clsLineaVehiculoDto>;

public sealed class clsCambiarVehiculoLineaCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IConfiguracionesVehiculoPort vehiculos,
    IAliasCatalogoPort alias,
    IVehiculosNexoPort vehiculosNexo,
    IPropuestaTarifaPort propuestas,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsCambiarVehiculoLineaCommand, clsLineaVehiculoDto>
{
    public async Task<clsLineaVehiculoDto> Handle(clsCambiarVehiculoLineaCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var linea = await lineasSolicitud.ObtenerDeSolicitudAsync(command.LineaSolicitudId, c.SolicitudId, ct)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "La línea no pertenece a la solicitud de esta cotización.");
        if (linea.EstadoLinea == EstadoLinea.Rechazada)
            throw new clsConflictException("LINEA_RECHAZADA", "La línea está rechazada: deshaga el rechazo antes de cambiarle el vehículo.");

        var vehiculo = await ResolverVehiculoAsync(command, ct);

        if (linea.ConfigVehiculoId == vehiculo.Id)
        {
            // Sin cambio de vehículo no se descarta nada; si la resolución promovió una fila, se guarda (solo eso).
            await uow.SaveChangesAsync(ct);
            return Resultado(linea, vehiculo, cambio: false, fleteEliminado: false, await ProponerAsync(c.SolicitudId, linea.Id, ct));
        }

        var anterior = linea.ConfigVehiculoId;
        linea.ConfigVehiculoId = vehiculo.Id;

        var fila = c.Lineas.FirstOrDefault(l => l.LineaSolicitudId == linea.Id);
        var fleteEliminado = fila is not null;
        if (fila is not null)
        {
            c.Lineas.Remove(fila);
            clsCotizacionCalculos.Recalcular(c);
        }
        if (fleteEliminado || linea.EstadoLinea == EstadoLinea.AutoResuelta) linea.EstadoLinea = EstadoLinea.PorCostear;
        linea.MotivoPendiente = null;   // el motivo viejo hablaba del vehículo anterior (p. ej. "línea sin vehículo")
        linea.UpdatedAt = clock.GetUtcNow();

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "cambiar_vehiculo_linea",
            JsonSerializer.Serialize(new
            {
                lineaSolicitudId = linea.Id, vehiculoAnterior = anterior, vehiculoNuevo = vehiculo.Id,
                tipoVehiculo = vehiculo.TipoVehiculo, carroceria = vehiculo.Carroceria, fleteEliminado,
            }));
        await uow.SaveChangesAsync(ct);

        // La propuesta lee de la base: por eso va DESPUÉS de guardar el vehículo nuevo.
        var propuesta = await ProponerAsync(c.SolicitudId, linea.Id, ct);
        var motivo = propuesta?.MotivoPendiente
            ?? (fleteEliminado ? "Cambió el vehículo: el flete anterior se descartó y la línea hay que volver a costearla." : null);
        if (linea.EstadoLinea == EstadoLinea.PorCostear && motivo is not null)
        {
            linea.MotivoPendiente = motivo.Length <= 500 ? motivo : motivo[..500];
            await uow.SaveChangesAsync(ct);
        }

        return Resultado(linea, vehiculo, cambio: true, fleteEliminado, propuesta);
    }

    private async Task<clsConfiguracionVehiculo> ResolverVehiculoAsync(clsCambiarVehiculoLineaCommand command, CancellationToken ct)
    {
        var texto = clsTexto.Limpiar(command.TipoVehiculo);
        if (command.ConfigVehiculoId is null && texto is null)
            throw new clsValidationFailedException("VEHICULO_REQUERIDO", "Indica el vehículo: configVehiculoId o tipoVehiculo.");
        if (command.ConfigVehiculoId is not null && texto is not null)
            throw new clsValidationFailedException("VEHICULO_REQUERIDO", "Indica el vehículo con configVehiculoId o con tipoVehiculo, no con los dos.");

        if (command.ConfigVehiculoId is { } id)
        {
            var v = await vehiculos.ObtenerAsync(id, ct)
                ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "La configuración de vehículo no existe.");
            if (!v.EsCatalogoOficial || v.Descartada)
                throw new clsConflictException("VEHICULO_NO_OFICIAL", "Solo se puede asignar una configuración de vehículo oficial.");
            return v;
        }

        var mapa = await alias.MapaActivoAsync(clsAliasElemento.TipoVehiculo, ct);
        var todos = await vehiculos.ListarAsync(ct);
        var porClave = clsResolucionDeCatalogos.IndexarVehiculosPorClave(todos);
        var oficialidad = await Catalogos.clsOficialidadDeVehiculos.CargarAsync(vehiculosNexo, ct);

        // Con alias: igual que la captura (puede crear la fila oficial o promover una en cuarentena). Sin alias: tiene que ser una
        // configuración oficial que ya exista — acá no se crea nada en cuarentena.
        if (mapa.ContainsKey(clsResolucionDeCatalogos.Normalizar(texto)) || oficialidad.CodigoOficial(texto, command.Carroceria) is not null)
        {
            var (resuelto, _) = clsResolucionDeCatalogos.ResolverVehiculo(texto, command.Carroceria, porClave, vehiculos, mapa, oficialidad);
            if (resuelto!.Descartada)
                throw new clsConflictException("VEHICULO_NO_OFICIAL", "Esa configuración de vehículo fue descartada.");
            return resuelto;
        }

        if (porClave.TryGetValue(clsResolucionDeCatalogos.ClaveVehiculo(texto!, command.Carroceria), out var existente)
            && existente.EsCatalogoOficial && !existente.Descartada)
            return existente;

        throw new clsValidationFailedException("VEHICULO_NO_RECONOCIDO",
            $"'{texto}' no es un vehículo conocido: agréguelo al diccionario de alias o elija uno oficial.");
    }

    private async Task<Common.Dtos.clsPropuestaLineaDto?> ProponerAsync(Guid solicitudId, Guid lineaId, CancellationToken ct)
    {
        var propuesta = await propuestas.ProponerAsync(solicitudId, ct);
        return propuesta.Lineas.FirstOrDefault(l => l.LineaId == lineaId);
    }

    private static clsLineaVehiculoDto Resultado(
        Nexo.Domain.Solicitudes.Entidades.clsLineaSolicitud linea, clsConfiguracionVehiculo v, bool cambio, bool fleteEliminado,
        Common.Dtos.clsPropuestaLineaDto? propuesta) =>
        new(linea.Id, v.Id, v.TipoVehiculo, v.Carroceria, v.EsCatalogoOficial, cambio, fleteEliminado,
            clsTexto.MinusculaInicial(linea.EstadoLinea.ToString())!, linea.MotivoPendiente, propuesta);
}
