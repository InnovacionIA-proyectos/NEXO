namespace Nexo.Mensajes.Costeo;

// ============================================================================
// Mensajes del auto-costeo de un pliego.
//
// La saga NO lee la base de datos: por eso el reparto es un mensaje aparte. Ella
// pide repartir, alguien reparte y le informa cuántas líneas hay. Así la máquina de
// estados solo orquesta.
//
// Y hay DOS formas distintas de que una línea no se resuelva, con ciclos de vida
// opuestos. Mezclarlas fue el error de diseño que se corrigió antes de escribir esto:
//
//   - Fallo técnico: la fuente se cayó. Se reintenta y se resuelve solo.
//   - Requiere tarifa manual: no hay tarifa válida. Nada va a cambiar hasta que el
//     Administrador de Tarifas actúe. Reintentar es quemar reintentos para nada, y
//     agotarlos dispararía la alerta del equipo como si algo se hubiera roto.
//
// De ahí que sean dos mensajes y no uno.
// ============================================================================

/// <summary>
/// La saga pide repartir el pliego en líneas. Quien lo atiende lee las líneas de la
/// base y publica un <see cref="CostearLineaSolicitada"/> por cada una.
/// </summary>
public sealed record RepartirLineasDeCosteo(Guid TrabajoId, Guid SolicitudId);

/// <summary>
/// Reparto terminado. El total que trae es la cuenta AUTORITATIVA con la que la saga
/// sabe cuándo está completa: el que venía en el mensaje inicial es informativo y
/// puede haber quedado desfasado.
/// </summary>
public sealed record PliegoRepartido(Guid TrabajoId, Guid SolicitudId, int TotalLineas);

/// <summary>Costear una línea. Lo atiende el consumidor de costeo (issue #16).</summary>
public sealed record CostearLineaSolicitada(Guid TrabajoId, Guid SolicitudId, Guid LineaId);

/// <summary>Línea resuelta por el servicio, con la fuente que gobernó la propuesta.</summary>
public sealed record LineaCosteada(Guid TrabajoId, Guid LineaId, decimal ValorPropuesto, string Fuente);

/// <summary>
/// Una fuente falló. Es transitorio: se reintenta y se espera que se resuelva solo.
/// El motivo viaja para poder mostrarlo mientras tanto.
/// </summary>
public sealed record LineaFalloTecnico(Guid TrabajoId, Guid LineaId, string Motivo);

/// <summary>
/// La línea necesita que una persona agregue o actualice la tarifa.
///
/// NO es un fallo y NO se reintenta. El servicio hizo su trabajo y la respuesta es
/// que aquí no hay tarifa válida. La línea pasa a la cola del Administrador de
/// Tarifas, donde no hay reloj: esa persona puede tener otros pliegos delante y eso
/// no es algo que un timeout deba resolver.
/// </summary>
public sealed record LineaRequiereTarifaManual(Guid TrabajoId, Guid SolicitudId, Guid LineaId, string Motivo);

// ============================================================================
// Recosteo: cuando aparece una tarifa que antes no existía.
// ============================================================================

/// <summary>
/// El Administrador de Tarifas cargó o extendió tarifas. Las líneas que estaban
/// esperando pueden ser resolubles ahora.
///
/// No dice QUÉ tarifa cambió a propósito. Saber que la línea X esperaba exactamente esa
/// tarifa exigiría replicar la cascada de búsqueda del piso —tipo de carga, unidad de
/// transporte, vía estándar, periodo—, y una réplica que se desvíe produce falsos
/// negativos: líneas que se quedan esperando para siempre porque el emparejamiento dijo
/// que no era su tarifa.
///
/// En vez de adivinar, se le vuelve a preguntar a la regla. Cuesta algún recosteo de
/// más; a cambio no hay línea que se quede olvidada.
/// </summary>
public sealed record TarifasActualizadas(string Motivo, DateTimeOffset OcurridoEn);

/// <summary>
/// Reintentar el costeo de una línea que había quedado esperando una tarifa.
///
/// Va por su propio mensaje y NO por CostearLineaSolicitada porque no debe llegar a la
/// saga: ese trabajo ya cerró, y cerró en Parcial con razón — en ese momento lo estaba.
/// Reabrirlo falsearía el histórico. Lo que el recosteo resuelve es la línea y su
/// pendiente, no el trabajo de entonces.
/// </summary>
public sealed record RecostearLineaSolicitada(Guid TrabajoId, Guid SolicitudId, Guid LineaId);

/// <summary>
/// El estado de un trabajo cambió. Sirve para avisarle a quien lo esté mirando.
///
/// Lleva SOLO el identificador, no el estado. Quien lo recibe lee el estado actual y
/// empuja eso, y con ello se ganan dos cosas.
///
/// La primera es que el aviso y la consulta de respaldo salen de la misma fuente, así
/// que no pueden decir cosas distintas — que es exactamente lo que el contrato promete.
///
/// La segunda es más sutil: dos mensajes pueden llegar desordenados. Si cada uno
/// llevara su foto del avance, un mensaje viejo entregado tarde haría RETROCEDER la
/// barra de progreso. Leyendo el estado al recibir, lo peor que pasa es avisar dos
/// veces lo mismo.
/// </summary>
public sealed record TrabajoActualizado(Guid TrabajoId);
