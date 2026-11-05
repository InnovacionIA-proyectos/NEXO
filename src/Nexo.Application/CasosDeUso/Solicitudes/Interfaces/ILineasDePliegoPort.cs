namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Ids de las líneas de un pliego para repartir el costeo; solo ids, porque quien costea cada línea la lee por su cuenta.</summary>
public interface ILineasDePliegoPort
{
    Task<IReadOnlyList<Guid>> IdsPorSolicitudAsync(Guid solicitudId, CancellationToken ct = default);
}
