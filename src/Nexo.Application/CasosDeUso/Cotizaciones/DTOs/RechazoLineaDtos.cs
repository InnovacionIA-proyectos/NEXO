namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

/// <summary>
/// Estado de una línea tras rechazarla o deshacer el rechazo: <paramref name="EstadoLinea"/> es <c>rechazada</c> o <c>porCostear</c>;
/// <paramref name="MotivoRechazo"/> y <paramref name="RechazadaAt"/> son null al deshacer.
/// </summary>
public record clsLineaRechazoDto(Guid LineaSolicitudId, string EstadoLinea, string? MotivoRechazo, DateTimeOffset? RechazadaAt);

/// <summary>Una línea rechazada de una solicitud, para armar el Excel ("Sin costeo") y el correo de entrega (con su motivo).</summary>
public record clsLineaRechazadaInfo(string Ruta, string? Tipologia, string Motivo);
