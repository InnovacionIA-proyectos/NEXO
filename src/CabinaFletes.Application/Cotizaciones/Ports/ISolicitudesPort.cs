using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.Cotizaciones.Ports;

/// <summary>
/// Puerto de SOLO LECTURA (más una transición de estado puntual) hacia el
/// agregado Solicitud, del módulo Solicitudes (aún no migrado a esta
/// arquitectura). Cotizaciones solo necesita consultarlo y, al entregar o
/// marcar un resultado, transicionar su estado.
/// </summary>
public interface ISolicitudesPort
{
    /// <summary>AsNoTracking, sin includes. Para validar existencia (Crear).</summary>
    Task<Solicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con Cliente. Para Entregar y MarcarResultado (transiciona Estado).</summary>
    Task<Solicitud?> ObtenerConClienteAsync(Guid id, CancellationToken ct);

    Task<int> ContarLineasAsync(Guid solicitudId, CancellationToken ct);
}
