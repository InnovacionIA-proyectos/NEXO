using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Priorizacion.Reglas;

namespace CabinaFletes.Api.BackgroundServices;

/// <summary>
/// Mantiene fresco el score PERSISTIDO de las solicitudes activas (Pendiente / EnCosteo).
/// La bandeja ya recalcula el score al leerlo (la urgencia depende del "ahora"), pero el
/// valor guardado se usa en métricas/tableros; sin este refresco quedaría congelado al
/// momento de la captura. Portado 1:1 desde
/// CabinaFletes.Api/Services/RecalculoScoreService.cs del proyecto original.
///
/// Vive en Api (no en Application) porque <see cref="BackgroundService"/> es un tipo de
/// hosting de ASP.NET Core: Application no debe depender de él. La lógica de negocio
/// (el cálculo del score) sigue siendo Domain.Priorizacion.Reglas puro; este servicio solo orquesta
/// el ciclo periódico a través de los puertos de Application (ISolicitudesRepository/IUnitOfWork).
/// </summary>
public class RecalculoScoreHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecalculoScoreHostedService> _logger;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _intervalo;

    public RecalculoScoreHostedService(
        IServiceScopeFactory scopes, ILogger<RecalculoScoreHostedService> logger,
        TimeProvider clock, IConfiguration config)
    {
        _scopes = scopes;
        _logger = logger;
        _clock = clock;
        var minutos = config.GetValue("Score:IntervaloRecalculoMinutos", 15);
        _intervalo = TimeSpan.FromMinutes(Math.Clamp(minutos, 1, 1440));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);
        do
        {
            try
            {
                await RecalcularAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // apagado normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recálculo periódico de scores falló; se reintenta en el próximo ciclo.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RecalcularAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var solicitudes = scope.ServiceProvider.GetRequiredService<ISolicitudesRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var ahora = _clock.GetUtcNow();
        var activas = await solicitudes.ObtenerActivasAsync(ct);

        var actualizadas = 0;
        foreach (var s in activas)
        {
            var score = (decimal)ScorePrioridadCalculator.Calcular(s, parametros, ahora).Total;
            if (s.ScorePrioridad != score)
            {
                s.ScorePrioridad = score;
                actualizadas++;
            }
        }

        if (actualizadas > 0)
        {
            await uow.SaveChangesAsync(ct);
            _logger.LogInformation("Recálculo de scores: {Actualizadas}/{Total} solicitudes activas.", actualizadas, activas.Count);
        }
    }
}
