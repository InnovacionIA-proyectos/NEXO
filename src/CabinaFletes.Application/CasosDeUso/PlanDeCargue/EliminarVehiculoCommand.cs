using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02: baja de un vehículo/contenedor del catálogo — DELETE /api/vehiculos/{id}.
/// Cierra el par de <see cref="GuardarVehiculoCommand"/>.
///
/// Por qué existe, ya que no venía en la lista original de 8: el frontend llama
/// <c>eliminarVehiculo()</c> (cubicador-api.service.ts) directo contra Cubicador con la
/// API key del bundle. Toda escritura del navegador tiene que tener camino por NEXO o
/// S3-HU-05 no puede cerrar — retirar el CORS y rotar la clave exige CERO llamadas de
/// navegador a Cubicador, y esta era la única escritura que quedaba sin equivalente.
///
/// No hay fila propia que borrar en NEXO: igual que <see cref="GuardarVehiculoCommand"/>,
/// el catálogo de vehículos vive únicamente en Cubicador (ver issue #108). Este comando
/// solo publica y responde 202.
///
/// Issue #155: Cubicador rechaza con VEHICULO_EN_USO si el vehículo tiene planes de
/// cargue asociados, pero ese rechazo llega async por `cubicacion.rechazada` con el
/// VehiculoId viajando en el campo PlanId del sobre — el consumer de ese evento busca por
/// PlanId en PlanesCargue, nunca encuentra fila (porque el id es de un vehículo, no de un
/// plan) y descarta el mensaje en silencio: el comando queda "procesando" para siempre
/// sin que nadie se entere. En vez de arreglar esa correlación async, se valida acá,
/// síncrono, ANTES de publicar: la fila espejo de PlanCargue (a diferencia del catálogo de
/// vehículos) sí vive en esta base, así que preguntar "¿hay algún plan con este
/// VehiculoId?" no requiere ida y vuelta a Cubicador.
/// </summary>
public sealed record EliminarVehiculoCommand(Guid VehiculoId) : ICommand<ComandoAceptadoDto>;

public sealed class EliminarVehiculoCommandHandler(
    ICubicacionPort cubicacion, IPlanCargueRepository planes, IUnitOfWork uow)
    : ICommandHandler<EliminarVehiculoCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(EliminarVehiculoCommand command, CancellationToken ct = default)
    {
        if (await planes.ExisteConVehiculoAsync(command.VehiculoId, ct))
            throw new ConflictException(
                "VEHICULO_EN_USO", "El vehículo tiene planes de cargue asociados y no puede eliminarse.");

        await cubicacion.PublicarComandoAsync(
            "eliminar_vehiculo", command.VehiculoId.ToString(), new EliminarVehiculoInput(), ct);

        // Obligatorio aunque este comando no toque ninguna fila propia: es el
        // SaveChanges lo que dispara el flush del Outbox, no que haya cambios
        // rastreados (ver ICubicacionPort y docs/mensajeria/CUBICACION.md #5).
        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.VehiculoId, "procesando");
    }
}
