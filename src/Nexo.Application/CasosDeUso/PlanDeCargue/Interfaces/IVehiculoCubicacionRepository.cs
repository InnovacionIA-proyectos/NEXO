using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// S3-HU-03 (issue #112): lectura del catálogo de vehículos proyectado en NEXO
/// (VehiculoCatalogoConsumer lo mantiene sincronizado — ver issue #111).
/// Sin escritura: los vehículos se crean/editan por el comando "guardar_vehiculo"
/// (S3-HU-02), nunca directo contra esta tabla.
/// </summary>
public interface IVehiculoCubicacionRepository
{
    /// <summary>Issue #166: page 1-based. Devuelve la página pedida + el total
    /// real de filas que cumplen (sin filtro alguno hoy: es el catálogo completo).</summary>
    Task<(IReadOnlyList<VehiculoCubicacion> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct);

    Task<VehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct);
}
