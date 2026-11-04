namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public sealed record clsClienteDwDto(string Bp, string Nit, string Nombre);

/// <summary>Clientes reales del DW (DimBPSolicitante). Null/Configurado=false si <c>ConnectionStrings:DwCubos</c> no está configurada: la captura usa la tabla local (ver IClientesPort).</summary>
public interface IClientesDwPort
{
    bool Configurado { get; }

    /// <summary>Catálogo completo (~4,5 K filas), cacheado 1 h.</summary>
    Task<IReadOnlyList<clsClienteDwDto>> ListarAsync(CancellationToken ct);
}
