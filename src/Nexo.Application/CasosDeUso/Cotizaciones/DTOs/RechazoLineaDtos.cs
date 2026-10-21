namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

/// <summary>
/// S6-HU-24.3/#320: el estado de una línea tras rechazarla o deshacer el rechazo. <paramref name="EstadoLinea"/>: <c>rechazada</c> o
/// <c>porCostear</c> (minúscula inicial, igual que <c>clsLineaCosteoDto.EstadoLinea</c>). <paramref name="MotivoRechazo"/> y
/// <paramref name="RechazadaAt"/> son null al deshacer.
/// </summary>
public record clsLineaRechazoDto(Guid LineaSolicitudId, string EstadoLinea, string? MotivoRechazo, DateTimeOffset? RechazadaAt);

/// <summary>Una línea rechazada de una solicitud, para armar el Excel ("Sin costeo") y el correo de entrega (con su motivo).</summary>
public record clsLineaRechazadaInfo(string Ruta, string? Tipologia, string Motivo);
