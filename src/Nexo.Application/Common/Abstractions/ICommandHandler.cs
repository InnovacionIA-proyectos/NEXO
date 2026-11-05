namespace Nexo.Application.Common.Abstractions;

/// <summary>Marca un Command (intención de escritura/mutación) que produce <typeparamref name="TResult"/>.</summary>
public interface ICommand<TResult>
{
}

/// <summary>Caso de uso de escritura, un handler por Command. Sin mediador a propósito: el controller inyecta el handler concreto.</summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}
