using Nexo.Application.CasosDeUso.Trabajos.DTOs;

namespace Nexo.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>Lee el estado de un trabajo en segundo plano; es puerto porque el estado vive en la saga de MassTransit, que Application no conoce.</summary>
public interface IEstadoDeTrabajoPort
{
    Task<clsEstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default);
}
