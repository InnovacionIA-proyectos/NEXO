using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IConfiguracionesVehiculoPort
{
    Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct);

    void Agregar(ConfiguracionVehiculo vehiculo);
}
