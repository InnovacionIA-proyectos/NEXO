using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

public interface ILineasSolicitudPort
{
    /// <summary>
    /// S6-HU-24.3/#320: las líneas RECHAZADAS de la solicitud con su ruta, sin seguimiento y por orden de captura — para el Excel ("Sin
    /// costeo") y el correo de entrega (con su motivo).
    /// </summary>
    Task<IReadOnlyList<clsLineaSolicitud>> ObtenerRechazadasAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>
    /// S6-HU-26.1/#30: la línea con su ruta (y los municipios de origen/destino, que traen el código DANE) y su vehículo, SIN
    /// seguimiento — lo justo para decidir si cae en una regla Solo-SICE. Null si la línea no es de esa solicitud.
    /// </summary>
    Task<clsLineaSolicitud?> ObtenerConRutaYVehiculoAsync(Guid lineaId, Guid solicitudId, CancellationToken ct);

    /// <summary>Tracked. Valida que la línea pertenezca a la solicitud (AgregarLinea).</summary>
    Task<clsLineaSolicitud?> ObtenerDeSolicitudAsync(Guid lineaId, Guid solicitudId, CancellationToken ct);

    /// <summary>AsNoTracking, indexado por Id (AplicarPropuestas).</summary>
    Task<IReadOnlyDictionary<Guid, clsLineaSolicitud>> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Marca las líneas dadas como Revisada (gestión en la Mesa) sin cargarlas.</summary>
    Task MarcarRevisadasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
