using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IConfiguracionesVehiculoPort
{
    Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct);

    void Agregar(ConfiguracionVehiculo vehiculo);
}
