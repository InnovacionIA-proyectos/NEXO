using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Baja de un vehículo del catálogo (DELETE /api/vehiculos/{id}); solo publica y responde 202 porque el catálogo vive únicamente en Cubicador.
/// Valida ANTES de publicar que ningún plan lo use: el rechazo async VEHICULO_EN_USO de Cubicador no se correlaciona (el VehiculoId viaja como PlanId).
/// </summary>
public sealed record clsEliminarVehiculoCommand(Guid VehiculoId) : ICommand<clsComandoAceptadoDto>;

public sealed class clsEliminarVehiculoCommandHandler(
    ICubicacionPort cubicacion, IPlanCargueRepository planes, IUnitOfWork uow)
    : ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsEliminarVehiculoCommand command, CancellationToken ct = default)
    {
        if (await planes.ExisteConVehiculoAsync(command.VehiculoId, ct))
            throw new clsConflictException(
                "VEHICULO_EN_USO", "El vehículo tiene planes de cargue asociados y no puede eliminarse.");

        await cubicacion.PublicarComandoAsync(
            "eliminar_vehiculo", command.VehiculoId.ToString(), new clsEliminarVehiculoInput(), ct);

        // Obligatorio: el SaveChanges dispara el flush del Outbox aunque no haya cambios rastreados.
        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.VehiculoId, "procesando");
    }
}
