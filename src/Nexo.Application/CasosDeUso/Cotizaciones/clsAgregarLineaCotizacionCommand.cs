using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.Cotizaciones.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>Agregar el flete costeado de una línea a la cotización (Administrador de Fletes).</summary>
public sealed record clsAgregarLineaCotizacionCommand(
    Guid CotizacionId,
    Guid? LineaSolicitudId,
    decimal ValorFlete,
    decimal? ValorRetornoContenedor,
    string Fuente) : ICommand<clsCotizacionDto>;

public sealed class clsAgregarLineaCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    ITarifasHistoricasPort tarifasHistoricas,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsAgregarLineaCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsAgregarLineaCotizacionCommand command, CancellationToken ct = default)
    {
        clsAgregarLineaCotizacionValidador.Validar(command);

        var c = await CargarEditableAsync(command.CotizacionId, ct);

        clsLineaSolicitud? lineaSol = null;
        if (command.LineaSolicitudId is Guid lid)
        {
            lineaSol = await lineasSolicitud.ObtenerDeSolicitudAsync(lid, c.SolicitudId, ct)
                ?? throw new clsValidationFailedException("LINEA_INVALIDA", "La línea de solicitud no pertenece a esta cotización.");
            // Además de validar, se marca la línea como Revisada (gestión del Administrador de
            // Fletes) para distinguirla del auto-costeo en la bandeja.
            lineaSol.EstadoLinea = EstadoLinea.Revisada;
        }

        var fuente = Enum.Parse<FuenteFlete>(command.Fuente, ignoreCase: true);

        // Bloqueo: una tarifa histórica solo se reutiliza si hay al menos una VIGENTE
        // (≤ 1 semana) para la ruta/tipología. Si todas están vencidas, se exige una
        // nueva solicitud en vez de reutilizar un valor obsoleto.
        if (fuente == FuenteFlete.Historica && lineaSol is not null)
        {
            var hoy = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var vigente = await tarifasHistoricas.HayVigenteAsync(lineaSol.RutaId ?? Guid.Empty, lineaSol.Tipologia, hoy.AddDays(-7), hoy, ct);
            if (!vigente)
                throw new clsConflictException("HISTORICA_VENCIDA",
                    "No se puede reutilizar una tarifa histórica vencida (más de 1 semana). Genere una nueva solicitud.");
        }

        var linea = new clsCotizacionLinea
        {
            CotizacionId = c.Id,
            LineaSolicitudId = command.LineaSolicitudId,
            ValorFlete = command.ValorFlete,
            ValorAprovisionamiento = lineaSol?.ValorAprovisionamiento,
            ValorRetornoContenedor = command.ValorRetornoContenedor,
            Fuente = fuente
        };
        c.Lineas.Add(linea);
        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "agregar_flete", $"{{\"valor\":{command.ValorFlete}}}");
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }

    private async Task<clsCotizacion> CargarEditableAsync(Guid id, CancellationToken ct)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(id, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");
        return c;
    }
}
