using Nexo.Application.Common.Abstractions;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Common;

public sealed class EfUnitOfWork(NexoDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
