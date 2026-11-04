using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Crea la flota de un plan a partir de una línea ya cotizada (POST /api/planes-cargue/desde-linea/{lineaId}). Como <see cref="clsCrearPlanDemoCommand"/>,
/// no deja fila espejo: Cubicador arma la flota completa y el correlationId es a propósito el propio <see cref="clsCrearPlanDesdeLineaCommand.LineaSolicitudId"/>,
/// para sondear con <see cref="clsObtenerPlanesPorLineaQuery"/>. Valida que la línea exista ANTES de publicar (404 en vez de un viaje redondo a la cola).
/// No pasa por <see cref="IParidadCubicadorPort"/>: es dato, no cálculo. Limitación del worker: solo el primer plan de una flota multi-vehículo llega a proyectarse en NEXO.
/// </summary>
public sealed record clsCrearPlanDesdeLineaCommand(Guid LineaSolicitudId) : ICommand<clsComandoAceptadoDto>;

public sealed class clsCrearPlanDesdeLineaCommandHandler(
    ILineaSolicitudCubicacionPort lineas, ICubicacionPort cubicacion, IRechazoCubicacionLineaPort rechazos, IUnitOfWork uow)
    : ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsCrearPlanDesdeLineaCommand command, CancellationToken ct = default)
    {
        var linea = await lineas.ObtenerAsync(command.LineaSolicitudId, ct)
            ?? throw new clsNotFoundException("LINEA_SOLICITUD_NO_ENCONTRADA", "La línea de solicitud no existe.");

        // El peso decide la capacidad del vehículo: se rechaza con PESO_NO_DECLARADO (cero cuenta como no declarado) antes de encolar,
        // porque el rechazo del worker llegaría tarde y el front quedaría sondeando para siempre.
        var sinPeso = linea.Posiciones
            .Where(p => p.PesoKg is not > 0)
            .Select(p => string.IsNullOrWhiteSpace(p.Descripcion) ? "(sin descripcion)" : p.Descripcion)
            .ToList();
        if (sinPeso.Count > 0)
            throw new clsValidationFailedException("PESO_NO_DECLARADO",
                $"Estas posiciones no declaran su peso: {string.Join(", ", sinPeso)}. " +
                "El peso decide la capacidad del vehículo; recapturar la línea con el peso de cada posición.");

        var lineaIdTexto = command.LineaSolicitudId.ToString();

        // Un intento nuevo empieza de cero: se borra el rechazo anterior (mismo SaveChanges que flushea el Outbox).
        await rechazos.LimpiarAsync(command.LineaSolicitudId, ct);

        // El tipo de cola es "crear_desde_linea" (el que implementa el worker de Cubicador); el correlationId queda como el propio lineaId.
        await cubicacion.PublicarComandoAsync(
            "crear_desde_linea", lineaIdTexto, new clsCrearPlanDesdeLineaInput(lineaIdTexto), ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.LineaSolicitudId, "procesando");
    }
}
