using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>Puerto de SOLO LECTURA hacia el agregado clsSolicitud, más la transición de estado al entregar o marcar un resultado.</summary>
public interface ISolicitudesPort
{
    /// <summary>AsNoTracking, sin includes. Para validar existencia (Crear).</summary>
    Task<clsSolicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con clsCliente y Creador. Para Entregar y MarcarResultado (transiciona Estado); Creador: quien pidió la consulta de
    /// flete, no el cliente ni un responsable comercial del DW.</summary>
    Task<clsSolicitud?> ObtenerConClienteAsync(Guid id, CancellationToken ct);

    Task<int> ContarLineasAsync(Guid solicitudId, CancellationToken ct);
}
