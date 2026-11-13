using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Common.Observabilidad;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Api.Tests.Infra;

/// <summary>El decorador de casos de uso no debe cambiar el resultado ni tragarse el error; solo abrir un tramo con el nombre del handler.</summary>
public class clsObservabilidadTests
{
    private sealed record clsSumar(int Valor) : ICommand<int>;

    private sealed class clsSumarHandler : ICommandHandler<clsSumar, int>
    {
        public Task<int> Handle(clsSumar command, CancellationToken ct = default)
            => command.Valor < 0 ? throw new InvalidOperationException("negativo") : Task.FromResult(command.Valor + 1);
    }

    [Fact]
    public async Task El_handler_decorado_devuelve_lo_mismo_y_deja_un_tramo_con_su_nombre_y_su_error()
    {
        var tramos = new List<Activity>();
        using var oyente = new ActivityListener
        {
            ShouldListenTo = f => f.Name == "Nexo.Application",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = tramos.Add,
        };
        ActivitySource.AddActivityListener(oyente);

        using var proveedor = new ServiceCollection()
            .AddLogging()
            .AddScoped<ICommandHandler<clsSumar, int>, clsSumarHandler>()
            .TrazarCasosDeUso()
            .BuildServiceProvider();
        var handler = proveedor.GetRequiredService<ICommandHandler<clsSumar, int>>();

        Assert.IsNotType<clsSumarHandler>(handler);
        Assert.Equal(2, await handler.Handle(new clsSumar(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new clsSumar(-1)));

        Assert.Equal(2, tramos.Count);
        Assert.All(tramos, t => Assert.Equal(nameof(clsSumarHandler), t.OperationName));
        Assert.Equal(ActivityStatusCode.Unset, tramos[0].Status);
        Assert.Equal(ActivityStatusCode.Error, tramos[1].Status);
    }

    [Theory]
    [InlineData(ActivityKind.Client, false)]   // SQL/HTTP suelto: sondeo en segundo plano
    [InlineData(ActivityKind.Server, true)]
    [InlineData(ActivityKind.Consumer, true)]
    [InlineData(ActivityKind.Internal, true)]
    public void Solo_se_descartan_las_trazas_que_empiezan_en_una_llamada_saliente(ActivityKind tipo, bool seTraza)
    {
        var decision = new clsSinSondeos().ShouldSample(
            new OpenTelemetry.Trace.SamplingParameters(default, ActivityTraceId.CreateRandom(), "x", tipo)).Decision;

        Assert.Equal(seTraza, decision == OpenTelemetry.Trace.SamplingDecision.RecordAndSample);
    }
}
