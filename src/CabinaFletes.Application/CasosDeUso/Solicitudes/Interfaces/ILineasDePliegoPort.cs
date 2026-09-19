namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>
/// Identificadores de las líneas de un pliego, para repartir el costeo.
///
/// Devuelve solo los ids y no las líneas completas: quien cuesta cada una las lee por
/// su cuenta. Traer 300 líneas enteras para publicar 300 mensajes que solo llevan un
/// identificador sería cargar memoria sin motivo.
/// </summary>
public interface ILineasDePliegoPort
{
    Task<IReadOnlyList<Guid>> IdsPorSolicitudAsync(Guid solicitudId, CancellationToken ct = default);
}
