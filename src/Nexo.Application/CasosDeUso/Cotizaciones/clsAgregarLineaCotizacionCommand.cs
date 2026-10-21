using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.Cotizaciones.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Common;

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
    IPropuestaTarifaPort propuestas,
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
        clsPropuestaLineaDto? propuestaLinea = null;
        if (command.LineaSolicitudId is Guid lid)
        {
            lineaSol = await lineasSolicitud.ObtenerDeSolicitudAsync(lid, c.SolicitudId, ct)
                ?? throw new clsValidationFailedException("LINEA_INVALIDA", "La línea de solicitud no pertenece a esta cotización.");
            // Además de validar, se marca la línea como Revisada (gestión del Administrador de
            // Fletes) para distinguirla del auto-costeo en la bandeja.
            lineaSol.EstadoLinea = EstadoLinea.Revisada;

            // S5-HU-19.3/#283: mismo cálculo que la propuesta — nunca se guarda un flete por
            // debajo del piso SICE-TAC, sea tarifario, histórico o manual. El servidor NO
            // confía en lo que diga el navegador (el piso ya se bloquea en pantalla, pero
            // llamar la API directo lo saltaría si solo se validara ahí).
            propuestaLinea = (await propuestas.ProponerAsync(c.SolicitudId, ct)).Lineas.FirstOrDefault(x => x.LineaId == lid);
            if (propuestaLinea?.Sicetac?.Valor is { } piso && command.ValorFlete < piso)
                throw new clsValidationFailedException("FLETE_BAJO_PISO_SICETAC",
                    $"El flete ({clsMoneda.Pesos(command.ValorFlete)}) está por debajo del piso SICE-TAC ({clsMoneda.Pesos(piso)}). No se puede guardar un valor por debajo del piso.");
        }

        var fuente = Enum.Parse<FuenteFlete>(command.Fuente, ignoreCase: true);

        // Bloqueo: una tarifa histórica solo se reutiliza si se cumple CUALQUIERA de las
        // dos — (1) hay al menos una "histórica sugerida" VIGENTE (≤ 1 semana) para la
        // ruta/tipología (regla de siempre, pensada para el botón "Usar" de esa lista), o
        // (2) el valor coincide con el candidato Histórico de ESTA propuesta (ventana de
        // 3 meses, ya recalculada arriba para el piso) — "Aplicar propuestas" ya guarda
        // ese mismo valor sin esta validación; agregarlo a mano con el mismo valor que
        // muestra la pantalla no debía poder fallar.
        if (fuente == FuenteFlete.Historica && lineaSol is not null)
        {
            var hoy = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var hayHistoricaSugeridaVigente = await tarifasHistoricas.HayVigenteAsync(lineaSol.RutaId ?? Guid.Empty, lineaSol.Tipologia, hoy.AddDays(-7), hoy, ct);
            var coincideConLaPropuesta = propuestaLinea?.HistoricoSap?.Valor == command.ValorFlete;
            if (!hayHistoricaSugeridaVigente && !coincideConLaPropuesta)
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
