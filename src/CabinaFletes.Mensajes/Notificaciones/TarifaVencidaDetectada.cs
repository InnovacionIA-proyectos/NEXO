namespace CabinaFletes.Mensajes.Notificaciones;

/// <summary>
/// El costeo encontró una tarifa de plaza SAP vencida para una ruta/vehículo (RN-02: se
/// descarta y gobierna el piso SICE-TAC). No es un fallo del servicio — el costeo sigue
/// su curso—, pero el Administrador de Fletes/Reglas necesita saber que hay una tarifa
/// para renovar. Sección 9 del documento de entendimiento: alerta de urgencia Alta.
/// </summary>
/// <param name="LineaId">La línea donde se detectó, para que quien reciba la alerta pueda ubicarla.</param>
/// <param name="Justificacion">La misma explicación que ve el usuario en la propuesta: ya dice qué tarifa se descartó y por qué.</param>
/// <param name="DetectadaEn">Momento en que el costeo encontró la vigencia vencida.</param>
public sealed record TarifaVencidaDetectada(Guid LineaId, string Justificacion, DateTimeOffset DetectadaEn);
