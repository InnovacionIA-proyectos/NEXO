namespace Nexo.Application.Common.Abstractions;

/// <summary>Application decide CUÁNDO confirmar los cambios; Infrastructure decide CÓMO.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
