namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public sealed record ClienteDwDto(string Bp, string Nit, string Nombre);

/// <summary>
/// Clientes reales del Data Warehouse (DWCubosOperacionesEBS.DimBPSolicitante).
/// Null/Configurado=false cuando ConnectionStrings:DwCubos no está configurada:
/// la captura se degrada a la tabla local de clientes (ver IClientesPort).
/// </summary>
public interface IClientesDwPort
{
    bool Configurado { get; }

    /// <summary>Catálogo completo (~4,5 K filas), cacheado 1 h.</summary>
    Task<IReadOnlyList<ClienteDwDto>> ListarAsync(CancellationToken ct);
}
