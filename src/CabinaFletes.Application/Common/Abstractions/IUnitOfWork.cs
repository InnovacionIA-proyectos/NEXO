namespace CabinaFletes.Application.Common.Abstractions;

/// <summary>
/// Puerto de persistencia transaccional. Application decide CUÁNDO confirmar los
/// cambios; Infrastructure decide CÓMO (hoy: SaveChangesAsync de un DbContext de EF Core).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
