using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Solicitudes.Ports;

/// <summary>Fila cruda de la Bandeja Priorizada: la proyección EF, sin el score (se calcula en Application con Domain.Scoring).</summary>
public sealed record BandejaFilaRaw(
    Guid Id, string Cliente, TipoSolicitud Tipo, EstadoSolicitud Estado, DateTimeOffset? Deadline,
    decimal? ValorEstimado, decimal? ProbabilidadCierre, DateTimeOffset CreatedAt, int Lineas,
    decimal? FleteTotal, int SinFlete, int Auto, int Revisadas);

public interface ISolicitudesRepository
{
    void Agregar(Solicitud solicitud);

    void AgregarLineas(IEnumerable<LineaSolicitud> lineas);

    /// <summary>Tracked, sin includes. Para recalcular score / cargar líneas bulk / rechazar.</summary>
    Task<Solicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con Cliente + Lineas.Ruta + Lineas.ConfiguracionVehiculo. Para el costeo.</summary>
    Task<Solicitud?> ObtenerParaCosteoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con Lineas (sin Ruta/ConfiguracionVehiculo). Para el auto-costeo en segundo plano.</summary>
    Task<Solicitud?> ObtenerConLineasAsync(Guid id, CancellationToken ct);

    /// <summary>Registra la cotización-borrador que crea el auto-costeo (Cotizaciones aún no expone un puerto de escritura hacia Solicitudes).</summary>
    void AgregarCotizacion(Cotizacion cotizacion);

    Task<IReadOnlyList<BandejaFilaRaw>> BandejaFilasAsync(
        string? atencion, EstadoSolicitud? estado, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct);

    Task<ParametrosScore> ParametrosScoreAsync(CancellationToken ct);

    /// <summary>Tracked. Para el recálculo periódico del score (RecalculoScoreHostedService).</summary>
    Task<IReadOnlyList<Solicitud>> ObtenerActivasAsync(CancellationToken ct);
}
