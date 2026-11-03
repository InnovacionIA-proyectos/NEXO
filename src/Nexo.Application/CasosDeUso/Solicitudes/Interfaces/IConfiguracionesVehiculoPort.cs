using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

public interface IConfiguracionesVehiculoPort
{
    Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarAsync(CancellationToken ct);

    /// <summary>S6-HU-27.3/#322: la configuración con seguimiento (para descartarla o asignarla a una línea); null si no existe.</summary>
    Task<clsConfiguracionVehiculo?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>S6-HU-27.3/#322: las filas en cuarentena (<c>EsCatalogoOficial = false</c>) que nadie ha descartado, sin seguimiento y por antigüedad.</summary>
    Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarEnCuarentenaAsync(CancellationToken ct);

    void Agregar(clsConfiguracionVehiculo vehiculo);
}
