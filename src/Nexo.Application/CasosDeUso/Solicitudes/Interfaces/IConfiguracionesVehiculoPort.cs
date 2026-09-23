using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IConfiguracionesVehiculoPort
{
    Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarAsync(CancellationToken ct);

    void Agregar(clsConfiguracionVehiculo vehiculo);
}
