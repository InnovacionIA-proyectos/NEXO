using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones.Mappers;

/// <summary>Mapeadores entidad -> DTO. Único punto donde se decide qué se serializa.</summary>
public static class CotizacionDtoMappers
{
    public static CotizacionDto ToDto(this Cotizacion c) =>
        new(
            c.Id,
            c.SolicitudId,
            c.Estado,
            c.Resultado,
            c.Total,
            c.EntregadaAt,
            c.Lineas.Select(l => new CotizacionLineaDto(
                l.Id, l.LineaSolicitudId, l.ValorFlete, l.Fuente,
                l.ValorAprovisionamiento, l.ValorRetornoContenedor)).ToList(),
            c.Conexos.Select(x => new CotizacionConexoDto(
                x.Id, x.ServicioConexoId, x.ServicioConexo?.Nombre,
                x.ServicioConexo?.Categoria ?? "logistico", x.Cantidad, x.ValorAplicado)).ToList());
}
