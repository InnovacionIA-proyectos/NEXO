namespace CabinaFletes.Application.Common.Abstractions;

/// <summary>Resultado "sin valor" para un Command que no tiene nada que devolver (equivalente a `void`,
/// que no puede ser argumento de <see cref="ICommand{TResult}"/>).</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
