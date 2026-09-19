using System.Linq.Expressions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>
/// Puerto hacia la persistencia del agregado Cotización. Los métodos son
/// deliberadamente específicos (no un CRUD genérico): cada uno refleja una
/// necesidad real de un caso de uso, con el nivel de tracking/includes que
/// ese caso de uso requiere.
/// </summary>
public interface ICotizacionRepository
{
    /// <summary>Tracked, con Lineas y Conexos.ServicioConexo. Para editar/leer una cotización por Id.</summary>
    Task<Cotizacion?> ObtenerConLineasYConexosAsync(Guid id, CancellationToken ct);

    /// <summary>Igual que el anterior, pero por un filtro arbitrario (buscar por solicitud + estado).</summary>
    Task<Cotizacion?> ObtenerConLineasYConexosAsync(Expression<Func<Cotizacion, bool>> filtro, CancellationToken ct);

    /// <summary>Tracked, con Lineas.LineaSolicitud y Conexos.ServicioConexo. Para MarcarResultado.</summary>
    Task<Cotizacion?> ObtenerParaMarcarResultadoAsync(Guid id, CancellationToken ct);

    /// <summary>AsNoTracking, con los includes profundos que necesita la exportación a Excel.</summary>
    Task<Cotizacion?> ObtenerParaExportarAsync(Expression<Func<Cotizacion, bool>> filtro, CancellationToken ct);

    /// <summary>Id de otra cotización de la misma solicitud que sí tenga contenido (líneas o conexos).</summary>
    Task<Guid?> BuscarIdConContenidoPorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    void Agregar(Cotizacion cotizacion);
}
