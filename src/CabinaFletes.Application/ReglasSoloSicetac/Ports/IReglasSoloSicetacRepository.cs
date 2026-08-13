using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.ReglasSoloSicetac.Ports;

public interface IReglasSoloSicetacRepository
{
    Task<IReadOnlyList<Domain.Entities.ReglaSoloSicetac>> ListarAsync(CancellationToken ct);

    /// <summary>Claves "ORIGEN|DESTINO" existentes hoy (para detectar duplicados sin traer las entidades completas).</summary>
    Task<IReadOnlySet<string>> ClavesOrigenDestinoTipoVehiculoAsync(CancellationToken ct);

    Task<bool> ExisteAsync(string origen, string destino, string tipoVehiculo, CancellationToken ct);

    void Agregar(Domain.Entities.ReglaSoloSicetac regla);
    void AgregarRango(IEnumerable<Domain.Entities.ReglaSoloSicetac> reglas);

    /// <summary>Elimina por Id; devuelve cuántas filas borró (0 o 1).</summary>
    Task<int> EliminarAsync(Guid id, CancellationToken ct);
}
