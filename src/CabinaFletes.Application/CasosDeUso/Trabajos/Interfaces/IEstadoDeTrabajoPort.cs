using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>
/// Lee el estado de un trabajo en segundo plano.
///
/// Es un puerto y no una consulta directa porque el estado vive en la saga de
/// MassTransit, y Application no puede conocer MassTransit. El día que la orquestación
/// cambie de tecnología, cambia el adaptador y este caso de uso no se entera.
/// </summary>
public interface IEstadoDeTrabajoPort
{
    Task<EstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default);
}
