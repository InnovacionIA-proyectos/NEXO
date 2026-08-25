using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.Solicitudes.Ports;

public interface IConfiguracionesVehiculoPort
{
    Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct);

    void Agregar(ConfiguracionVehiculo vehiculo);
}
