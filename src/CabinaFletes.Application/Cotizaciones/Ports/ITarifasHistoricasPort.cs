using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Cotizaciones.Ports;

public interface ITarifasHistoricasPort
{
    /// <summary>¿Hay al menos una tarifa histórica vigente (rango [desde,hasta]) para la ruta/tipología?</summary>
    Task<bool> HayVigenteAsync(Guid rutaId, TipologiaCarga? tipologia, DateOnly desde, DateOnly hasta, CancellationToken ct);

    Task<bool> ExistenParaCotizacionAsync(Guid cotizacionId, CancellationToken ct);

    void Agregar(TarifaHistorica tarifa);
}
