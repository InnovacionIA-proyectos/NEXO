namespace Nexo.Application.CasosDeUso.Bandeja.DTOs;

/// <summary>
/// Lo que viaja por el canal cuando la bandeja cambia.
///
/// Es una señal, no la bandeja. El cliente reconsulta con SUS filtros — el servidor no
/// sabe cuáles usa cada uno, así que no existe "el nuevo orden" que empujar.
/// </summary>
/// <param name="Motivo">Por qué cambió. Sirve para el log y para que el cliente decida.</param>
/// <param name="En">Cuándo, en UTC. Permite al cliente agrupar refrescos seguidos.</param>
public record clsAvisoDeBandeja(string Motivo, DateTimeOffset En);
