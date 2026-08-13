using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Infrastructure.Persistence;

namespace CabinaFletes.Infrastructure.Repositories;

public sealed class EfUnitOfWork(CabinaFletesDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
