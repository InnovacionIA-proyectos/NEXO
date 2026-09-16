namespace CabinaFletes.Mensajes.Notificaciones;

/// <summary>
/// Una cotización pasó a Ganada por primera vez. Sección 9 del documento de
/// entendimiento: alerta de urgencia Baja para el Administrador de Reglas — la tarifa ya
/// quedó realimentada a la Biblioteca (ver MarcarResultadoCommand), esto solo avisa.
/// </summary>
public sealed record CotizacionMarcadaGanada(Guid CotizacionId, Guid SolicitudId, DateTimeOffset MarcadaEn);
