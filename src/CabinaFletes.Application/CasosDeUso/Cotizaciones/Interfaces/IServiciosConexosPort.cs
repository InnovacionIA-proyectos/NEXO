using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;

public interface IServiciosConexosPort
{
    Task<ServicioConexo?> ObtenerPorIdAsync(Guid id, CancellationToken ct);
}
