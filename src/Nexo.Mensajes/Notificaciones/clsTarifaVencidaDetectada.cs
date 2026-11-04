namespace Nexo.Mensajes.Notificaciones;

/// <summary>
/// El costeo encontró una tarifa de plaza SAP vencida para una ruta/vehículo: se descarta y gobierna el piso
/// SICE-TAC. No es un fallo; avisa al Administrador de Fletes/Reglas de que hay una tarifa para renovar (urgencia Alta).
/// </summary>
/// <param name="LineaId">La línea donde se detectó, para que quien reciba la alerta pueda ubicarla.</param>
/// <param name="Justificacion">La misma explicación que ve el usuario en la propuesta: ya dice qué tarifa se descartó y por qué.</param>
/// <param name="DetectadaEn">Momento en que el costeo encontró la vigencia vencida.</param>
public sealed record clsTarifaVencidaDetectada(Guid LineaId, string Justificacion, DateTimeOffset DetectadaEn);
