using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.AjustesVigenciaPlaza.Ports;

/// <summary>
/// Persistencia de los overrides LOCALES de vigencia de tarifas de plaza. El mapa
/// {IdTarifaPlaza -> vigencia ajustada} lo consultan otros módulos (Biblioteca,
/// costeo — aún no migrados) para computar la vigencia EFECTIVA de una tarifa SAP.
/// </summary>
public interface IAjustesVigenciaPlazaRepository
{
    Task<IReadOnlyDictionary<int, DateTime>> MapaAsync(CancellationToken ct);

    Task<Domain.Entities.AjusteVigenciaPlaza?> ObtenerPorTarifaAsync(int tarifaPlazaId, CancellationToken ct);

    Task<IReadOnlyDictionary<int, Domain.Entities.AjusteVigenciaPlaza>> ObtenerPorTarifasAsync(
        IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct);

    void Agregar(Domain.Entities.AjusteVigenciaPlaza ajuste);

    void Eliminar(Domain.Entities.AjusteVigenciaPlaza ajuste);

    /// <summary>Invalida el caché del mapa — llamar DESPUÉS de confirmar los cambios (SaveChangesAsync).</summary>
    void InvalidarCache();
}
