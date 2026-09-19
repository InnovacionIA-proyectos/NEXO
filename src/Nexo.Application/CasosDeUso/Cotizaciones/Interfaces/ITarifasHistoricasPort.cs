using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;

public interface ITarifasHistoricasPort
{
    /// <summary>¿Hay al menos una tarifa histórica vigente (rango [desde,hasta]) para la ruta/tipología?</summary>
    Task<bool> HayVigenteAsync(Guid rutaId, TipologiaCarga? tipologia, DateOnly desde, DateOnly hasta, CancellationToken ct);

    Task<bool> ExistenParaCotizacionAsync(Guid cotizacionId, CancellationToken ct);

    void Agregar(clsTarifaHistorica tarifa);
}
