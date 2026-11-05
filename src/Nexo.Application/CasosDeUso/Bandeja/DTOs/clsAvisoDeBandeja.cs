namespace Nexo.Application.CasosDeUso.Bandeja.DTOs;

/// <summary>Señal de que la bandeja cambió, no la bandeja: el cliente reconsulta con SUS filtros porque el servidor no conoce el orden que usa cada uno.</summary>
/// <param name="Motivo">Por qué cambió. Sirve para el log y para que el cliente decida.</param>
/// <param name="En">Cuándo, en UTC. Permite al cliente agrupar refrescos seguidos.</param>
public record clsAvisoDeBandeja(string Motivo, DateTimeOffset En);
