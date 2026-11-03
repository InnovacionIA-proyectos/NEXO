using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Baja de un vehículo/contenedor del catálogo — DELETE /api/vehiculos/{id}.
/// Cierra el par de <see cref="clsGuardarVehiculoCommand"/>.
///
/// Por qué existe, ya que no venía en la lista original de 8: el frontend llama
/// <c>eliminarVehiculo()</c> (cubicador-api.service.ts) directo contra Cubicador con la
/// API key del bundle. Toda escritura del navegador tiene que tener camino por NEXO o
/// esa migración no puede cerrar — retirar el CORS y rotar la clave exige CERO llamadas de
/// navegador a Cubicador, y esta era la única escritura que quedaba sin equivalente.
///
/// No hay fila propia que borrar en NEXO: igual que <see cref="clsGuardarVehiculoCommand"/>,
/// el catálogo de vehículos vive únicamente en Cubicador. Este comando
/// solo publica y responde 202.
///
/// Cubicador rechaza con VEHICULO_EN_USO si el vehículo tiene planes de
/// cargue asociados, pero ese rechazo llega async por `cubicacion.rechazada` con el
/// VehiculoId viajando en el campo PlanId del sobre — el consumer de ese evento busca por
/// PlanId en PlanesCargue, nunca encuentra fila (porque el id es de un vehículo, no de un
/// plan) y descarta el mensaje en silencio: el comando queda "procesando" para siempre
/// sin que nadie se entere. En vez de arreglar esa correlación async, se valida acá,
/// síncrono, ANTES de publicar: la fila espejo de clsPlanCargue (a diferencia del catálogo de
/// vehículos) sí vive en esta base, así que preguntar "¿hay algún plan con este
/// VehiculoId?" no requiere ida y vuelta a Cubicador.
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

        // Obligatorio aunque este comando no toque ninguna fila propia: es el
        // SaveChanges lo que dispara el flush del Outbox, no que haya cambios
        // rastreados (ver ICubicacionPort y docs/mensajeria/CUBICACION.md #5).
        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.VehiculoId, "procesando");
    }
}
