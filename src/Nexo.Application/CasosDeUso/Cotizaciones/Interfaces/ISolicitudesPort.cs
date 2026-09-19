using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

/// <summary>
/// Puerto de SOLO LECTURA (más una transición de estado puntual) hacia el
/// agregado clsSolicitud, del módulo Solicitudes (aún no migrado a esta
/// arquitectura). Cotizaciones solo necesita consultarlo y, al entregar o
/// marcar un resultado, transicionar su estado.
/// </summary>
public interface ISolicitudesPort
{
    /// <summary>AsNoTracking, sin includes. Para validar existencia (Crear).</summary>
    Task<clsSolicitud?> ObtenerBasicoAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked, con clsCliente y Creador. Para Entregar y MarcarResultado (transiciona
    /// Estado); Creador lo usa además clsNotificarEntregaCommand (S2-HU-06.2): quien pidió la
    /// consulta de flete, no el cliente ni un responsable comercial del DW.</summary>
    Task<clsSolicitud?> ObtenerConClienteAsync(Guid id, CancellationToken ct);

    Task<int> ContarLineasAsync(Guid solicitudId, CancellationToken ct);
}
