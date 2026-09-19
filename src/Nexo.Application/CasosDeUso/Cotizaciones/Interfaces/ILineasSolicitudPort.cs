using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

public interface ILineasSolicitudPort
{
    /// <summary>Tracked. Valida que la línea pertenezca a la solicitud (AgregarLinea).</summary>
    Task<LineaSolicitud?> ObtenerDeSolicitudAsync(Guid lineaId, Guid solicitudId, CancellationToken ct);

    /// <summary>AsNoTracking, indexado por Id (AplicarPropuestas).</summary>
    Task<IReadOnlyDictionary<Guid, LineaSolicitud>> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Marca las líneas dadas como Revisada (gestión en la Mesa) sin cargarlas.</summary>
    Task MarcarRevisadasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
