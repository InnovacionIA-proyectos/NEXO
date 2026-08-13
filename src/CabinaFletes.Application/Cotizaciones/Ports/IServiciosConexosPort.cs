using CabinaFletes.Domain.Entities;

namespace CabinaFletes.Application.Cotizaciones.Ports;

public interface IServiciosConexosPort
{
    Task<ServicioConexo?> ObtenerPorIdAsync(Guid id, CancellationToken ct);
}
