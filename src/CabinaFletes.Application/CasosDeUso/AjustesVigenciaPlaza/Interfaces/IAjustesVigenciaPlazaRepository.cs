using CabinaFletes.Domain.Costeo.Entidades;

namespace CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;

/// <summary>
/// Persistencia de los overrides LOCALES de vigencia de tarifas de plaza. El mapa
/// {IdTarifaPlaza -> vigencia ajustada} lo consultan otros módulos (Biblioteca,
/// costeo — aún no migrados) para computar la vigencia EFECTIVA de una tarifa SAP.
/// </summary>
public interface IAjustesVigenciaPlazaRepository
{
    Task<IReadOnlyDictionary<int, DateTime>> MapaAsync(CancellationToken ct);

    Task<Domain.Costeo.Entidades.AjusteVigenciaPlaza?> ObtenerPorTarifaAsync(int tarifaPlazaId, CancellationToken ct);

    Task<IReadOnlyDictionary<int, Domain.Costeo.Entidades.AjusteVigenciaPlaza>> ObtenerPorTarifasAsync(
        IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct);

    void Agregar(Domain.Costeo.Entidades.AjusteVigenciaPlaza ajuste);

    void Eliminar(Domain.Costeo.Entidades.AjusteVigenciaPlaza ajuste);

    /// <summary>Invalida el caché del mapa — llamar DESPUÉS de confirmar los cambios (SaveChangesAsync).</summary>
    void InvalidarCache();
}
