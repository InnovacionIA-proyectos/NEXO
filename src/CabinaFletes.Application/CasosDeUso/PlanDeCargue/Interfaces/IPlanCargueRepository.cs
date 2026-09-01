using CabinaFletes.Domain.PlanDeCargue.Entidades;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Tracked: quien lo pide va a mutar Estado (S2-HU-08.1).</summary>
public interface IPlanCargueRepository
{
    Task<PlanCargue?> ObtenerAsync(Guid id, CancellationToken ct);
}
