namespace Nexo.Mensajes.Costeo;

// Mensajes del auto-costeo de un pliego. Una línea puede no resolverse de dos formas con ciclos de vida
// opuestos: fallo técnico (transitorio, se reintenta) y requiere tarifa manual (no se reintenta, espera
// al Administrador de Tarifas). Por eso son dos mensajes distintos.

/// <summary>La saga pide repartir el pliego en líneas; el consumidor publica un <see cref="clsCostearLineaSolicitada"/> por cada una.</summary>
public sealed record clsRepartirLineasDeCosteo(Guid TrabajoId, Guid SolicitudId);

/// <summary>Reparto terminado. TotalLineas es la cuenta autoritativa con la que la saga sabe cuándo está completa.</summary>
public sealed record clsPliegoRepartido(Guid TrabajoId, Guid SolicitudId, int TotalLineas);

/// <summary>Costear una línea. Lo atiende el consumidor de costeo.</summary>
public sealed record clsCostearLineaSolicitada(Guid TrabajoId, Guid SolicitudId, Guid LineaId);

/// <summary>Línea resuelta, con la fuente que gobernó la propuesta.</summary>
public sealed record clsLineaCosteada(Guid TrabajoId, Guid LineaId, decimal ValorPropuesto, string Fuente);

/// <summary>Una fuente falló. Es transitorio: se reintenta.</summary>
public sealed record clsLineaFalloTecnico(Guid TrabajoId, Guid LineaId, string Motivo);

/// <summary>
/// No hay tarifa válida: la línea pasa a la cola del Administrador de Tarifas. No es un fallo y no se
/// reintenta (agotar reintentos dispararía la alerta del equipo sin que nada se haya roto).
/// </summary>
public sealed record clsLineaRequiereTarifaManual(Guid TrabajoId, Guid SolicitudId, Guid LineaId, string Motivo);

/// <summary>
/// Se cargaron o extendieron tarifas; las líneas en espera pueden ser resolubles. No dice qué tarifa cambió:
/// replicar la cascada de búsqueda del piso podría dejar líneas esperando para siempre, así que se vuelve
/// a preguntar a la regla.
/// </summary>
public sealed record clsTarifasActualizadas(string Motivo, DateTimeOffset OcurridoEn);

/// <summary>
/// Reintenta el costeo de una línea que esperaba tarifa. Va por mensaje propio y no por
/// clsCostearLineaSolicitada para no reabrir el trabajo de la saga, que ya cerró en Parcial.
/// </summary>
public sealed record clsRecostearLineaSolicitada(Guid TrabajoId, Guid SolicitudId, Guid LineaId);

/// <summary>
/// El estado de un trabajo cambió. Lleva solo el id: quien lo recibe lee el estado actual, así un mensaje
/// viejo entregado tarde no hace retroceder el progreso.
/// </summary>
public sealed record clsTrabajoActualizado(Guid TrabajoId);
