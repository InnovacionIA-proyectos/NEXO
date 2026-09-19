using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>
/// Evita que el prefijo "cls" de las clases (consumers, sagas, mensajes) cambie los
/// nombres de cola/exchange que MassTransit genera por convención. Sin esto, renombrar
/// una clase (p. ej. CostearLineaConsumer -> clsCostearLineaConsumer) crearía una cola
/// nueva en RabbitMQ y dejaría huérfana la anterior. Envuelve el formateador por
/// defecto y quita el segmento "cls-" inicial del resultado.
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
    public string TemporaryEndpoint() => Base.TemporaryEndpoint();
}
