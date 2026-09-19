using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Mappers;

/// <summary>Mapeadores entidad -> DTO. Único punto donde se decide qué se serializa.</summary>
public static class clsCotizacionDtoMappers
{
    public static clsCotizacionDto ToDto(this clsCotizacion c) =>
        new(
            c.Id,
            c.SolicitudId,
            c.Estado,
            c.Resultado,
            c.Total,
            c.EntregadaAt,
            c.Lineas.Select(l => new clsCotizacionLineaDto(
                l.Id, l.LineaSolicitudId, l.ValorFlete, l.Fuente,
                l.ValorAprovisionamiento, l.ValorRetornoContenedor)).ToList(),
            c.Conexos.Select(x => new clsCotizacionConexoDto(
                x.Id, x.ServicioConexoId, x.clsServicioConexo?.Nombre,
                x.clsServicioConexo?.Categoria ?? "logistico", x.Cantidad, x.ValorAplicado)).ToList());
}
