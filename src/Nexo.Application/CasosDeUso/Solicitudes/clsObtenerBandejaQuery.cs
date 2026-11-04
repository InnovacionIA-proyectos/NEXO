using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Bandeja Priorizada (anti-FIFO): se sirve por score descendente. Por defecto es la COLA DE TRABAJO (líneas sin flete, o todas revisadas pero
/// sin entregar). El score se recalcula en memoria con "ahora", así que la paginación se aplica DESPUÉS de traer y ordenar todo.
/// </summary>
public sealed record clsObtenerBandejaQuery(clsBandejaFiltro Filtro, int Page = 1, int PageSize = 50) : IQuery<clsPaginaDto<clsBandejaItemDto>>;

public sealed class clsObtenerBandejaQueryHandler(
    ISolicitudesRepository solicitudes, TimeProvider clock, ICurrentUserPort usuarioActual)
    : IQueryHandler<clsObtenerBandejaQuery, clsPaginaDto<clsBandejaItemDto>>
{
    public async Task<clsPaginaDto<clsBandejaItemDto>> Handle(clsObtenerBandejaQuery query, CancellationToken ct = default)
    {
        var filtro = query.Filtro;
        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var ahora = clock.GetUtcNow();
        var resultado = clsBandejaResultado.Parsear(filtro.Resultado);
        // La vista por defecto ("pendientes") solo trae Pendiente/EnCosteo; con un resultado informado y sin atención explícita se trata como
        // "todas", porque una cotización Ganada/Perdida/esperando ya es Entregada.
        var atencion = (string.IsNullOrWhiteSpace(filtro.Atencion) ? (resultado is null ? "pendientes" : "todas") : filtro.Atencion)
            .Trim().ToLowerInvariant();

        EstadoSolicitud? estado = null;
        if (!string.IsNullOrWhiteSpace(filtro.Estado) && Enum.TryParse<EstadoSolicitud>(filtro.Estado, ignoreCase: true, out var e) && Enum.IsDefined(e))
            estado = e;

        // Aislamiento por comercial: un Comercial solo ve lo que él mismo creó; los demás roles ven la bandeja completa.
        var creadoPorFiltro = usuarioActual.Rol == RolUsuario.Comercial ? usuarioActual.Id : (Guid?)null;

        var filas = await solicitudes.BandejaFilasAsync(atencion, estado, filtro.Desde, filtro.Hasta, creadoPorFiltro, resultado, ct);

        // La vigencia es la de la FECHA LÍMITE; sin Deadline cuenta como vigente, vencida = Deadline anterior a "ahora".
        bool Vigente(clsBandejaFilaRaw f) => f.Deadline is null || f.Deadline >= ahora;
        var vigenciaFiltro = filtro.Vigencia?.Trim().ToLowerInvariant();
        if (vigenciaFiltro == "vigente") filas = filas.Where(Vigente).ToList();
        else if (vigenciaFiltro == "vencida") filas = filas.Where(f => !Vigente(f)).ToList();

        // El score persistido se congela al capturar; la urgencia depende del "ahora",
        // así que se recalcula al servir la bandeja para que el orden anti-FIFO no se
        // degrade (una licitación urgente debe subir a medida que se acerca su deadline).
        var ordenadas = filas
            .Select(f =>
            {
                var stub = new clsSolicitud { Tipo = f.Tipo, Deadline = f.Deadline, ValorEstimado = f.ValorEstimado, ProbabilidadCierre = f.ProbabilidadCierre };
                var score = (decimal)clsScorePrioridadCalculator.Calcular(stub, parametros, ahora).Total;
                var etiqueta = f.SinFlete > 0 ? "pendiente"
                    : f.Revisadas > 0 ? "administrador"
                    : f.Auto > 0 ? "automatico"
                    : "manual";
                var vigencia = Vigente(f) ? "vigente" : "vencida";
                return new clsBandejaItemDto(
                    f.Id, f.clsCliente, f.Tipo.ToString(), f.Estado.ToString(), f.Deadline, f.ValorEstimado,
                    score, f.Lineas, f.FleteTotal, f.SinFlete, f.CreatedAt, etiqueta, vigencia, f.Resultado?.ToString());
            })
            .OrderByDescending(x => x.ScorePrioridad)
            .ThenBy(x => x.Deadline)
            .ToList();

        var pagina = ordenadas.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new clsPaginaDto<clsBandejaItemDto>(pagina, ordenadas.Count);
    }
}
