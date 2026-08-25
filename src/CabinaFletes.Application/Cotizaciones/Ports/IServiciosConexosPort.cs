using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.Cotizaciones.Ports;

public interface IServiciosConexosPort
{
    Task<ServicioConexo?> ObtenerPorIdAsync(Guid id, CancellationToken ct);
}
