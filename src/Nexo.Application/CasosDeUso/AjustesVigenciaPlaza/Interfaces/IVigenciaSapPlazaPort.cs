namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;

/// <summary>Vigencia ORIGINAL de SAP para tarifas de plaza (solo lectura): referencia al crear un ajuste y base para extender cuando no hay override.</summary>
public interface IVigenciaSapPlazaPort
{
    bool Configurado { get; }
    Task<IReadOnlyDictionary<int, DateTime>> VigenciasAsync(IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct);
}
