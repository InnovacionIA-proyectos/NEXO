using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// #304: el rechazo que el Cubicador reportó para el "crear_desde_linea" de una línea. Ver
/// <see cref="clsRechazoCubicacionLinea"/>. Escribe sobre el mismo DbContext de la petición: quien llama hace el
/// <c>SaveChangesAsync</c> (igual que el Outbox del mensaje que publica en la misma transacción).
/// </summary>
public interface IRechazoCubicacionLineaPort
{
    Task<clsRechazoCubicacionLinea?> ObtenerAsync(Guid lineaSolicitudId, CancellationToken ct = default);

    /// <summary>
    /// Registra (o reemplaza) el rechazo de una línea, sin guardar. Devuelve <c>false</c> si el Id NO es una línea real —
    /// el rechazo de un Id huérfano sigue siendo solo un log, no una fila. Recorta el código a 60 y el mensaje a 1000.
    /// </summary>
    Task<bool> RegistrarAsync(Guid lineaSolicitudId, string? codigo, string? mensaje, CancellationToken ct = default);

    /// <summary>Borra el rechazo previo, si lo hay (no guarda). Se llama al pedir el plan otra vez.</summary>
    Task LimpiarAsync(Guid lineaSolicitudId, CancellationToken ct = default);
}
