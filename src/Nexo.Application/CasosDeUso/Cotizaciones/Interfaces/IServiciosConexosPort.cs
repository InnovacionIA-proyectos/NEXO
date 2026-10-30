using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

public interface IServiciosConexosPort
{
    Task<clsServicioConexo?> ObtenerPorIdAsync(Guid id, CancellationToken ct);
}
