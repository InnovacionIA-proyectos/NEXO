using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>Consolidado/total de la cotización integral.</summary>
public sealed record clsObtenerCotizacionQuery(Guid Id) : IQuery<clsCotizacionDto>;

public sealed class clsObtenerCotizacionQueryHandler(ICotizacionRepository cotizaciones)
    : IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsObtenerCotizacionQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(query.Id, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        return c.ToDto();
    }
}
