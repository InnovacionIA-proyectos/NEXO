using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Crea un plan de cargue nuevo — POST /api/planes-cargue. El
/// PlanId lo genera NEXO acá (no Cubicador): la fila espejo
/// queda de inmediato en NEXO, y el comando "crear_plan" le pide a Cubicador crear la
/// fila REAL con ese mismo id en su propia base. Si Cubicador la rechaza (p.ej.
/// vehiculoId inexistente en su catálogo), <c>clsCubicacionRechazadaConsumer</c> marca esta
/// misma fila "error" — mismo camino que ya usa "optimizar".
///
/// La fila espejo nace en <b>"procesando"</b>, no en "borrador", y
/// <c>clsPlanDeCargueGeneradoConsumer</c> la mueve al estado real cuando llega la
/// confirmación (hace <c>plan.Estado = mensaje.Resultado.Estado</c>, que para un plan
/// recién creado es "borrador"). Corrige una carrera real: con la fila en "borrador"
/// desde el POST, un <c>POST /{id}/optimizar</c> inmediato pasaba la guarda de
/// <see cref="clsSolicitarOptimizacionCommand"/> (que solo rechaza 409 si ya está
/// "procesando") y encolaba "optimizar" mientras "crear_plan" seguía sin procesarse —
/// los dos mensajes van a la MISMA cola con prefetch 2, así que el worker podía atender
/// "optimizar" primero y fallar con PLAN_NO_ENCONTRADO sobre un plan que estaba por
/// crearse. Con "procesando", esa guarda ya existente bloquea el caso sin código nuevo.
/// Alinea además el 202 con la UX descrita para Cubo 2 (spinner mientras el estado sea
/// "procesando", la fila aparece cuando llega el aviso).
///
/// A propósito NO pasa por <c>IParidadCubicadorPort</c>: es una operación de datos, no de
/// cálculo — a diferencia de <see cref="clsSolicitarOptimizacionCommand"/>.
///
/// Decisión de que el planId lo genera NEXO (no Cubicador): ver
/// docs/decisiones/ADR-planid-generado-por-nexo.md.
/// </summary>
public sealed record clsCrearPlanCommand(string Nombre, Guid VehiculoId) : ICommand<clsSolicitudDeCubicacionAceptadaDto>;

public sealed class clsCrearPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto>
{
    public async Task<clsSolicitudDeCubicacionAceptadaDto> Handle(clsCrearPlanCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new clsValidationFailedException("NOMBRE_REQUERIDO", "El nombre del plan es obligatorio.");

        var planId = Guid.NewGuid();
        var plan = new clsPlanCargue
        {
            Id = planId, Nombre = command.Nombre.Trim(), VehiculoId = command.VehiculoId, Estado = "procesando",
        };

        await planes.PrepararCreacionAsync(plan, ct);

        // Publicar ANTES de guardar (mismo criterio que clsSolicitarOptimizacionCommand): el
        // Outbox mete el mensaje en la MISMA transacción que la fila nueva.
        await cubicacion.PublicarComandoAsync(
            "crear_plan", planId.ToString(),
            new clsCrearPlanInput(plan.Nombre, plan.VehiculoId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new clsSolicitudDeCubicacionAceptadaDto(planId, plan.Estado, $"/api/planes-cargue/{planId}");
    }
}
