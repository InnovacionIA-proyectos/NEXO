using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Precarga como flete por defecto el valor más alto propuesto por la Biblioteca
/// (plaza vs SICE-TAC) en cada línea de la solicitud que aún no tiene flete.
///
/// <paramref name="SoloAutomaticas"/> (S5-HU-19.2/#282, default false = aplica todas,
/// el botón de siempre): con true, salta las líneas en modo ConsultaCompleta — esas
/// necesitan que el Administrador de Tarifas elija, no se auto-aplican aunque tengan un
/// valor recomendado.
/// </summary>
public sealed record clsAplicarPropuestasCommand(Guid CotizacionId, bool SoloAutomaticas = false) : ICommand<clsAplicarPropuestasResultDto>;

public sealed class clsAplicarPropuestasCommandHandler(
    ICotizacionRepository cotizaciones,
    ILineasSolicitudPort lineasSolicitud,
    IPropuestaTarifaPort propuestas,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsAplicarPropuestasCommand, clsAplicarPropuestasResultDto>
{
    public async Task<clsAplicarPropuestasResultDto> Handle(clsAplicarPropuestasCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var conFlete = c.Lineas.Where(l => l.LineaSolicitudId != null).Select(l => l.LineaSolicitudId!.Value).ToHashSet();

        // Idempotencia: si TODAS las líneas de la solicitud ya tienen flete en la cotización,
        // no hay nada que aplicar (evita reejecutar la propuesta —consulta pesada a los DW—
        // ante un doble clic o reintento).
        var totalLineas = await solicitudes.ContarLineasAsync(c.SolicitudId, ct);
        if (conFlete.Count >= totalLineas)
            return new clsAplicarPropuestasResultDto(c.ToDto(), 0);

        var propuesta = await propuestas.ProponerAsync(c.SolicitudId, ct);
        var lineasSol = await lineasSolicitud.ObtenerPorSolicitudAsync(c.SolicitudId, ct);

        var agregadas = 0;
        var lineasCosteadas = new List<Guid>();
        foreach (var p in propuesta.Lineas)
        {
            if (p.ValorPropuesto is not { } valor || conFlete.Contains(p.LineaId)) continue;
            if (command.SoloAutomaticas && p.ModoDecision == nameof(Nexo.Domain.Costeo.Reglas.ModoDecision.ConsultaCompleta)) continue;
            var linea = new clsCotizacionLinea
            {
                CotizacionId = c.Id,
                LineaSolicitudId = p.LineaId,
                ValorFlete = valor,
                ValorAprovisionamiento = lineasSol.GetValueOrDefault(p.LineaId)?.ValorAprovisionamiento,
                ValorRetornoContenedor = p.RetornoContenedor?.Valor,
                // ORIGENTARIFA de la regla 9: SICETAC o TARIFARIO según la fuente ganadora.
                Fuente = p.FuentePropuesta switch
                {
                    "sicetac" => FuenteFlete.Sicetac,
                    "plaza" => FuenteFlete.Tarifario,
                    "historico_sap" => FuenteFlete.Historica,
                    _ => FuenteFlete.Manual,
                },
            };
            c.Lineas.Add(linea);
            lineasCosteadas.Add(p.LineaId);
            agregadas++;
        }

        if (agregadas > 0)
        {
            clsCotizacionCalculos.Recalcular(c);
            auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "aplicar_propuestas", $"{{\"agregadas\":{agregadas}}}");
            await uow.SaveChangesAsync(ct);
            // Marca esas líneas como Revisada (gestión en la Mesa) para la bandeja.
            await lineasSolicitud.MarcarRevisadasAsync(lineasCosteadas, ct);
        }
        return new clsAplicarPropuestasResultDto(c.ToDto(), agregadas);
    }
}
