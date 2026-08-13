using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Cotizaciones.Dtos;
using CabinaFletes.Application.Cotizaciones.Ports;

namespace CabinaFletes.Application.Cotizaciones.Queries;

/// <summary>Consolidado/total de la cotización integral.</summary>
public sealed record ObtenerCotizacionQuery(Guid Id) : IQuery<CotizacionDto>;

public sealed class ObtenerCotizacionQueryHandler(ICotizacionRepository cotizaciones)
    : IQueryHandler<ObtenerCotizacionQuery, CotizacionDto>
{
    public async Task<CotizacionDto> Handle(ObtenerCotizacionQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(query.Id, ct)
            ?? throw new NotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        return c.ToDto();
    }
}
