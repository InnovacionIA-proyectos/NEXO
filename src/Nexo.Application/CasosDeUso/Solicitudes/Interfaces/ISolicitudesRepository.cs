using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Fila cruda de la Bandeja Priorizada: la proyección EF, sin el score (se calcula en Application con Domain.Priorizacion.Reglas).</summary>
public sealed record clsBandejaFilaRaw(
    Guid Id, string clsCliente, TipoSolicitud Tipo, EstadoSolicitud Estado, DateTimeOffset? Deadline,
    decimal? ValorEstimado, decimal? ProbabilidadCierre, DateTimeOffset CreatedAt, int Lineas,
    decimal? FleteTotal, int SinFlete, int Auto, int Revisadas);

public interface ISolicitudesRepository
{
    void Agregar(clsSolicitud solicitud);

    void AgregarLineas(IEnumerable<clsLineaSolicitud> lineas);

    /// <summary>Tracked, sin includes. Para recalcular score / cargar líneas bulk / rechazar.</summary>
    Task<clsSolicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con clsCliente + Lineas.clsRuta + Lineas.clsConfiguracionVehiculo. Para el costeo.</summary>
    Task<clsSolicitud?> ObtenerParaCosteoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con Lineas (sin clsRuta/clsConfiguracionVehiculo). Para el auto-costeo en segundo plano.</summary>
    Task<clsSolicitud?> ObtenerConLineasAsync(Guid id, CancellationToken ct);

    /// <summary>Registra la cotización-borrador que crea el auto-costeo (Cotizaciones aún no expone un puerto de escritura hacia Solicitudes).</summary>
    void AgregarCotizacion(clsCotizacion cotizacion);

    /// <summary>
    /// <paramref name="creadoPor"/>: aislamiento por comercial (issue #199, S4-HU-19.1) —
    /// no nulo restringe la bandeja a las solicitudes creadas por ese usuario; nulo trae
    /// todas (roles distintos de Comercial).
    /// </summary>
    Task<IReadOnlyList<clsBandejaFilaRaw>> BandejaFilasAsync(
        string? atencion, EstadoSolicitud? estado, DateTimeOffset? desde, DateTimeOffset? hasta,
        Guid? creadoPor, CancellationToken ct);

    Task<clsParametrosScore> ParametrosScoreAsync(CancellationToken ct);

}
