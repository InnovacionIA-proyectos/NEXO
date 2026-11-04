using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;

/// <summary>Persistencia de los overrides LOCALES de vigencia de tarifas de plaza; el mapa {IdTarifaPlaza -> vigencia} sirve para computar la vigencia EFECTIVA.</summary>
public interface IAjustesVigenciaPlazaRepository
{
    Task<IReadOnlyDictionary<int, DateTime>> MapaAsync(CancellationToken ct);

    Task<Domain.Costeo.Entidades.clsAjusteVigenciaPlaza?> ObtenerPorTarifaAsync(int tarifaPlazaId, CancellationToken ct);

    Task<IReadOnlyDictionary<int, Domain.Costeo.Entidades.clsAjusteVigenciaPlaza>> ObtenerPorTarifasAsync(
        IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct);

    void Agregar(Domain.Costeo.Entidades.clsAjusteVigenciaPlaza ajuste);

    void Eliminar(Domain.Costeo.Entidades.clsAjusteVigenciaPlaza ajuste);

    /// <summary>Invalida el caché del mapa — llamar DESPUÉS de confirmar los cambios (SaveChangesAsync).</summary>
    void InvalidarCache();
}
