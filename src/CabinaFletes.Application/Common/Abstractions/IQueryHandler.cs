namespace CabinaFletes.Application.Common.Abstractions;

/// <summary>Marca una Query (lectura, sin efectos secundarios) que produce <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult>
{
}

/// <summary>Caso de uso de lectura. Un handler por Query.</summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken ct = default);
}
