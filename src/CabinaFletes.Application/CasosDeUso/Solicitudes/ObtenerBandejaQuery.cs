using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Reglas;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Bandeja Priorizada (anti-FIFO): la cola se sirve por score descendente, no por
/// orden de llegada. Por defecto (atención 'pendientes') es la COLA DE TRABAJO:
/// solo solicitudes con líneas SIN flete.
///
/// Issue #166: pagina (page 1-based, pageSize) - a diferencia de los listados
/// paginados en SQL (Skip/Take antes de traer las filas), acá el score se
/// recalcula en memoria por fila con "ahora" (el score persistido se congela al
/// capturar), así que la paginación se aplica DESPUÉS de traer y ordenar todo lo
/// que cumple el filtro. No reduce el trabajo del backend (eso exigiría poder
/// calcular el score en SQL, fuera de alcance de este fix), pero sí acota el
/// tamaño de la respuesta - que es el síntoma que reportó el frontend.
/// </summary>
public sealed record ObtenerBandejaQuery(BandejaFiltro Filtro, int Page = 1, int PageSize = 50) : IQuery<PaginaDto<BandejaItemDto>>;

public sealed class ObtenerBandejaQueryHandler(ISolicitudesRepository solicitudes, TimeProvider clock)
    : IQueryHandler<ObtenerBandejaQuery, PaginaDto<BandejaItemDto>>
{
    public async Task<PaginaDto<BandejaItemDto>> Handle(ObtenerBandejaQuery query, CancellationToken ct = default)
    {
        var filtro = query.Filtro;
        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var ahora = clock.GetUtcNow();
        var atencion = (filtro.Atencion ?? "pendientes").Trim().ToLowerInvariant();

        EstadoSolicitud? estado = null;
        if (!string.IsNullOrWhiteSpace(filtro.Estado) && Enum.TryParse<EstadoSolicitud>(filtro.Estado, ignoreCase: true, out var e) && Enum.IsDefined(e))
            estado = e;

        var filas = await solicitudes.BandejaFilasAsync(atencion, estado, filtro.Desde, filtro.Hasta, ct);

        var limiteVigencia = ahora.AddDays(-7);
        var vigenciaFiltro = filtro.Vigencia?.Trim().ToLowerInvariant();
        if (vigenciaFiltro == "vigente") filas = filas.Where(f => f.CreatedAt >= limiteVigencia).ToList();
        else if (vigenciaFiltro == "vencida") filas = filas.Where(f => f.CreatedAt < limiteVigencia).ToList();

        // El score persistido se congela al capturar; la urgencia depende del "ahora",
        // así que se recalcula al servir la bandeja para que el orden anti-FIFO no se
        // degrade (una licitación urgente debe subir a medida que se acerca su deadline).
        var ordenadas = filas
            .Select(f =>
            {
                var stub = new Solicitud { Tipo = f.Tipo, Deadline = f.Deadline, ValorEstimado = f.ValorEstimado, ProbabilidadCierre = f.ProbabilidadCierre };
                var score = (decimal)ScorePrioridadCalculator.Calcular(stub, parametros, ahora).Total;
                var etiqueta = f.SinFlete > 0 ? "pendiente"
                    : f.Revisadas > 0 ? "administrador"
                    : f.Auto > 0 ? "automatico"
                    : "manual";
                var vigencia = f.CreatedAt >= limiteVigencia ? "vigente" : "vencida";
                return new BandejaItemDto(
                    f.Id, f.Cliente, f.Tipo.ToString(), f.Estado.ToString(), f.Deadline, f.ValorEstimado,
                    score, f.Lineas, f.FleteTotal, f.SinFlete, f.CreatedAt, etiqueta, vigencia);
            })
            .OrderByDescending(x => x.ScorePrioridad)
            .ThenBy(x => x.Deadline)
            .ToList();

        var pagina = ordenadas.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new PaginaDto<BandejaItemDto>(pagina, ordenadas.Count);
    }
}
