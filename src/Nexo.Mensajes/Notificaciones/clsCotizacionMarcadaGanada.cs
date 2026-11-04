namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// Una cotización pasó a Ganada por primera vez. Alerta de urgencia Baja para el Administrador de Reglas:
/// la tarifa ya quedó realimentada a la Biblioteca, esto solo avisa.
/// </summary>
public sealed record clsCotizacionMarcadaGanada(Guid CotizacionId, Guid SolicitudId, DateTimeOffset MarcadaEn);
