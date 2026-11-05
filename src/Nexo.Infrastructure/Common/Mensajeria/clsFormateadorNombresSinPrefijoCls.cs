using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>
/// Quita el prefijo "cls-" de los nombres de cola/exchange de MassTransit para que renombrar una clase no cree una cola nueva y deje huérfana la anterior.
/// </summary>
public sealed class clsFormateadorNombresSinPrefijoCls : IEndpointNameFormatter
{
    private static readonly IEndpointNameFormatter Base = KebabCaseEndpointNameFormatter.Instance;

    private static string SinPrefijo(string nombre) =>
        nombre.StartsWith("cls-", StringComparison.Ordinal) ? nombre["cls-".Length..] : nombre;

    public string Separator => Base.Separator;

    public string Consumer<T>() where T : class, IConsumer => SinPrefijo(Base.Consumer<T>());
    public string Message<T>() where T : class => SinPrefijo(Base.Message<T>());
    public string Saga<T>() where T : class, ISaga => SinPrefijo(Base.Saga<T>());
    public string ExecuteActivity<T, TArguments>() where T : class, IExecuteActivity<TArguments> where TArguments : class =>
        SinPrefijo(Base.ExecuteActivity<T, TArguments>());
    public string CompensateActivity<T, TLog>() where T : class, ICompensateActivity<TLog> where TLog : class =>
        SinPrefijo(Base.CompensateActivity<T, TLog>());
    public string SanitizeName(string name) => Base.SanitizeName(name);
    public string TemporaryEndpoint(string tag) => Base.TemporaryEndpoint(tag);
}
