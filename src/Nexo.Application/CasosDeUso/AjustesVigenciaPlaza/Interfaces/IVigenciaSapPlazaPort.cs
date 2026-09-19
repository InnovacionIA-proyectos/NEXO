namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;

/// <summary>
/// Vigencia ORIGINAL de SAP para tarifas de plaza (DbSolucionesTiDbContext, SOLO
/// LECTURA). Se usa como base al crear un ajuste nuevo (referencia/auditoría) y
/// para extender relativo a la vigencia SAP cuando no hay override todavía.
/// </summary>
public interface IVigenciaSapPlazaPort
{
    bool Configurado { get; }
    Task<IReadOnlyDictionary<int, DateTime>> VigenciasAsync(IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct);
}
