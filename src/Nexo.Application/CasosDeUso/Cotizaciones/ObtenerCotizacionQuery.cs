using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

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
