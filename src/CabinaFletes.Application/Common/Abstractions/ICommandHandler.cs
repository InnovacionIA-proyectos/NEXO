namespace CabinaFletes.Application.Common.Abstractions;

/// <summary>Marca un Command (intención de escritura/mutación) que produce <typeparamref name="TResult"/>.</summary>
public interface ICommand<TResult>
{
}

/// <summary>
/// Caso de uso de escritura. Un handler por Command (Single Responsibility). No hay
/// despachador/mediador de por medio a propósito: el controller inyecta el handler
/// concreto que necesita (menos indirección, sin depender de una librería externa).
/// </summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}
