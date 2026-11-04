namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public sealed record clsProspectoDwDto(string Nit, string Nombre);

/// <summary>
/// Prospectos comerciales (dbSolucionesTI.tblProspectos), aún sin BP en el DW (ver <see cref="IClientesDwPort"/>). Solo se consulta si la
/// búsqueda no halla nada en DimBPSolicitante. Null/Configurado=false si <c>ConnectionStrings:DbSolucionesTi</c> no está configurada.
/// </summary>
public interface IProspectosDwPort
{
    bool Configurado { get; }

    /// <summary>Catálogo completo, cacheado 1 h.</summary>
    Task<IReadOnlyList<clsProspectoDwDto>> ListarAsync(CancellationToken ct);
}
