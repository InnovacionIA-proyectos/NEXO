using System.Text.Json;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Excepciones;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Mappers;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Validadores;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.BibliotecaTarifas.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Reglas;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Mensajes.Notificaciones;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones;

/// <summary>Registrar el resultado (ganada/perdida) de la cotización y realimentar la Biblioteca.</summary>
public sealed record MarcarResultadoCommand(
    Guid CotizacionId, string Resultado, string? Motivo, string? Comentario) : ICommand<CotizacionDto>;

public sealed class MarcarResultadoCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    ITarifasHistoricasPort tarifasHistoricas,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IPublicadorDeMensajes publicador,
    IUnitOfWork uow)
    : ICommandHandler<MarcarResultadoCommand, CotizacionDto>
{
    public async Task<CotizacionDto> Handle(MarcarResultadoCommand command, CancellationToken ct = default)
    {
        MarcarResultadoValidador.Validar(command);
        var resultado = Enum.Parse<ResultadoCotizacion>(command.Resultado, ignoreCase: true);

        var c = await cotizaciones.ObtenerParaMarcarResultadoAsync(command.CotizacionId, ct)
            ?? throw new NotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");

        // Idempotencia: re-marcar el mismo resultado no repite efectos (evita duplicar
        // tarifas históricas por doble clic o reintento del cliente).
        if (MaquinaEstados.EsReMarcadoIdentico(c.Resultado, resultado))
            return c.ToDto();

        // No se puede cambiar Ganada<->Perdida con un re-marcado (eso sería una corrección
        // explícita, no soportada aquí): se rechaza en vez de sobrescribir en silencio.
        if (!MaquinaEstados.PuedeMarcarResultado(c.Resultado, resultado))
            throw new ConflictException("RESULTADO_YA_DEFINIDO",
                $"La cotización ya está marcada como {c.Resultado} y no puede cambiarse a {resultado}.");

        // La mesa del comercial permite marcar el resultado directo desde el borrador:
        // se registra una entrega implícita (sin correo) para conservar la trazabilidad.
        if (c.Estado == EstadoCotizacion.Borrador)
        {
            if (c.Lineas.Count == 0)
                throw new CotizacionSinFleteException("marcar el resultado de");
            CotizacionCalculos.Recalcular(c);
            c.Estado = EstadoCotizacion.Entregada;
            c.EntregadaPor = usuarioActual.Id;
            c.EntregadaAt = clock.GetUtcNow();
            auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "entrega_implicita", $"{{\"total\":{c.Total}}}");
        }
        else if (c.Estado != EstadoCotizacion.Entregada)
        {
            throw new ConflictException("COTIZACION_NO_ENTREGADA", "Solo se puede marcar el resultado de una cotización entregada.");
        }

        var esNuevaGanada = resultado == ResultadoCotizacion.Ganada && c.Resultado != ResultadoCotizacion.Ganada;
        c.Resultado = resultado;

        // Estado de la solicitud + motivo de la pérdida (mismo formato que el rechazo).
        var solicitud = await solicitudes.ObtenerConClienteAsync(c.SolicitudId, ct);
        if (solicitud is not null)
        {
            // Una solicitud descartada no puede reactivarse marcando una cotización vieja.
            if (!MaquinaEstados.PuedeTransicionar(solicitud.Estado, EstadoSolicitud.Entregada))
                throw new ConflictException("SOLICITUD_NO_TRANSICIONABLE",
                    $"La solicitud está {solicitud.Estado} y no puede pasar a Entregada.");
            solicitud.Estado = EstadoSolicitud.Entregada;
            if (resultado == ResultadoCotizacion.Perdida && !string.IsNullOrWhiteSpace(command.Motivo))
            {
                var condiciones = CondicionesServicioDto.Desde(solicitud.CondicionesServicio) with
                {
                    Rechazo = new MotivoRechazoDto(command.Motivo.Trim(),
                        string.IsNullOrWhiteSpace(command.Comentario) ? null : command.Comentario.Trim(),
                        clock.GetUtcNow()),
                };
                solicitud.CondicionesServicio = JsonSerializer.Serialize(condiciones, CondicionesServicioDto.Json);
            }
        }

        // Realimenta la Biblioteca: las ganadas se vuelven tarifas históricas reutilizables.
        // Solo al pasar A ganada por primera vez, y verificando que no existan ya para esta
        // cotización (doble protección contra duplicados en la Biblioteca).
        if (esNuevaGanada && !await tarifasHistoricas.ExistenParaCotizacionAsync(c.Id, ct))
        {
            var hoy = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            foreach (var linea in c.Lineas.Where(l => l.LineaSolicitud is not null))
            {
                tarifasHistoricas.Agregar(new TarifaHistorica
                {
                    RutaId = linea.LineaSolicitud!.RutaId,
                    ConfigVehiculoId = linea.LineaSolicitud.ConfigVehiculoId,
                    Tipologia = linea.LineaSolicitud.Tipologia,
                    Valor = linea.ValorFlete,
                    Fecha = hoy,
                    CotizacionId = c.Id
                });
            }
        }

        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "marcar_resultado",
            JsonSerializer.Serialize(new { resultado = resultado.ToString(), motivo = command.Motivo, comentario = command.Comentario }));

        // Sección 9 del documento de entendimiento: alerta de urgencia Baja al
        // Administrador de Reglas. Solo al pasar A ganada por primera vez, igual que la
        // realimentación de la Biblioteca de arriba.
        if (esNuevaGanada)
            await publicador.PublicarAsync(new CotizacionMarcadaGanada(c.Id, c.SolicitudId, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
