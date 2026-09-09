using CabinaFletes.Domain.PlanDeCargue.Entidades;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// S3-HU-03 (issue #112): lectura del catálogo de vehículos proyectado en NEXO
/// (VehiculoCatalogoConsumer lo mantiene sincronizado — ver issue #111).
/// Sin escritura: los vehículos se crean/editan por el comando "guardar_vehiculo"
/// (S3-HU-02), nunca directo contra esta tabla.
/// </summary>
public interface IVehiculoCubicacionRepository
{
    Task<IReadOnlyList<VehiculoCubicacion>> ListarAsync(CancellationToken ct);

    Task<VehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct);
}
