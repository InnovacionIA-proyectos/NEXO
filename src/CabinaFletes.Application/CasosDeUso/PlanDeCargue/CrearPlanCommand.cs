using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): crea un plan de cargue nuevo — POST /api/planes-cargue. El
/// PlanId lo genera NEXO acá (no Cubicador, ver criterio 1 de la HU): la fila espejo
/// queda de inmediato en NEXO, y el comando "crear_plan" le pide a Cubicador crear la
/// fila REAL con ese mismo id en su propia base. Si Cubicador la rechaza (p.ej.
/// vehiculoId inexistente en su catálogo), <c>CubicacionRechazadaConsumer</c> marca esta
/// misma fila "error" — mismo camino que ya usa "optimizar".
///
/// La fila espejo nace en <b>"procesando"</b>, no en "borrador", y
/// <c>PlanDeCargueGeneradoConsumer</c> la mueve al estado real cuando llega la
/// confirmación (hace <c>plan.Estado = mensaje.Resultado.Estado</c>, que para un plan
/// recién creado es "borrador"). Corrige una carrera real: con la fila en "borrador"
/// desde el POST, un <c>POST /{id}/optimizar</c> inmediato pasaba la guarda de
/// <see cref="SolicitarOptimizacionCommand"/> (que solo rechaza 409 si ya está
/// "procesando") y encolaba "optimizar" mientras "crear_plan" seguía sin procesarse —
/// los dos mensajes van a la MISMA cola con prefetch 2, así que el worker podía atender
/// "optimizar" primero y fallar con PLAN_NO_ENCONTRADO sobre un plan que estaba por
/// crearse. Con "procesando", esa guarda ya existente bloquea el caso sin código nuevo.
/// Alinea además el 202 con la UX descrita para Cubo 2 (spinner mientras el estado sea
/// "procesando", la fila aparece cuando llega el aviso).
///
/// A propósito NO pasa por <c>IParidadCubicadorPort</c>: es una operación de datos, no de
/// cálculo (HU S3-HU-02, criterio 5) — a diferencia de <see cref="SolicitarOptimizacionCommand"/>.
///
/// Decisión de que el planId lo genera NEXO (no Cubicador): ver
/// docs/decisiones/ADR-planid-generado-por-nexo.md (issue #102, S3-HU-01.2).
/// </summary>
public sealed record CrearPlanCommand(string Nombre, Guid VehiculoId) : ICommand<SolicitudDeCubicacionAceptadaDto>;

public sealed class CrearPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<CrearPlanCommand, SolicitudDeCubicacionAceptadaDto>
{
    public async Task<SolicitudDeCubicacionAceptadaDto> Handle(CrearPlanCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ValidationFailedException("NOMBRE_REQUERIDO", "El nombre del plan es obligatorio.");

        var planId = Guid.NewGuid();
        var plan = new PlanCargue
        {
            Id = planId, Nombre = command.Nombre.Trim(), VehiculoId = command.VehiculoId, Estado = "procesando",
        };

        await planes.PrepararCreacionAsync(plan, ct);

        // Publicar ANTES de guardar (mismo criterio que SolicitarOptimizacionCommand): el
        // Outbox mete el mensaje en la MISMA transacción que la fila nueva.
        await cubicacion.PublicarComandoAsync(
            "crear_plan", planId.ToString(),
            new CrearPlanInput(plan.Nombre, plan.VehiculoId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new SolicitudDeCubicacionAceptadaDto(planId, plan.Estado, $"/api/planes-cargue/{planId}");
    }
}
