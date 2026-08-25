using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Mappers;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones;

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
