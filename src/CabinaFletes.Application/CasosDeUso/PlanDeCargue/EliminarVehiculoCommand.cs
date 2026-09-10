using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
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
/// Cubicador puede rechazarlo con VEHICULO_EN_USO si el vehículo tiene planes de cargue
/// asociados. Es un rechazo de negocio: llega por `cubicacion.rechazada` y no se
/// reintenta. A propósito NO se anticipa esa validación acá — NEXO no mantiene espejo del
/// catálogo, así que no puede saberlo sin preguntarle a Cubicador, y preguntarle sería el
/// HTTP directo que esta arquitectura evita.
/// </summary>
public sealed record EliminarVehiculoCommand(Guid VehiculoId) : ICommand<ComandoAceptadoDto>;

public sealed class EliminarVehiculoCommandHandler(ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<EliminarVehiculoCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(EliminarVehiculoCommand command, CancellationToken ct = default)
    {
        await cubicacion.PublicarComandoAsync(
            "eliminar_vehiculo", command.VehiculoId.ToString(), new EliminarVehiculoInput(), ct);

        // Obligatorio aunque este comando no toque ninguna fila propia: es el
        // SaveChanges lo que dispara el flush del Outbox, no que haya cambios
        // rastreados (ver ICubicacionPort y docs/mensajeria/CUBICACION.md #5).
        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.VehiculoId, "procesando");
    }
}
