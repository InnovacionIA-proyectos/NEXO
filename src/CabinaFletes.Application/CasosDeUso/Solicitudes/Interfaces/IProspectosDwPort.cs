namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

public sealed record ProspectoDwDto(string Nit, string Nombre);

/// <summary>
/// Prospectos comerciales (dbSolucionesTI.tblProspectos) — todavía no son clientes
/// reales con BP en el DW (ver <see cref="IClientesDwPort"/>). Solo se consulta cuando
/// la búsqueda no encuentra nada en DimBPSolicitante: evita cargar y combinar los dos
/// catálogos completos en cada búsqueda (HU-005 parte 2). Null/Configurado=false cuando
/// ConnectionStrings:DbSolucionesTi no está configurada.
/// </summary>
public interface IProspectosDwPort
{
    bool Configurado { get; }

    /// <summary>Catálogo completo, cacheado 1 h.</summary>
    Task<IReadOnlyList<ProspectoDwDto>> ListarAsync(CancellationToken ct);
}
