using System.Text.Json;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.Cotizaciones.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Reglas;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>Registrar el resultado (ganada/perdida) de la cotización y realimentar la Biblioteca.</summary>
public sealed record clsMarcarResultadoCommand(
    Guid CotizacionId, string Resultado, string? Motivo, string? Comentario) : ICommand<clsCotizacionDto>;

public sealed class clsMarcarResultadoCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    ITarifasHistoricasPort tarifasHistoricas,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IPublicadorDeMensajes publicador,
    IUnitOfWork uow)
    : ICommandHandler<clsMarcarResultadoCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsMarcarResultadoCommand command, CancellationToken ct = default)
    {
        clsMarcarResultadoValidador.Validar(command);
        var resultado = Enum.Parse<ResultadoCotizacion>(command.Resultado, ignoreCase: true);

        var c = await cotizaciones.ObtenerParaMarcarResultadoAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");

        // Idempotencia: re-marcar el mismo resultado no repite efectos (evita duplicar
        // tarifas históricas por doble clic o reintento del cliente).
        if (clsMaquinaEstados.EsReMarcadoIdentico(c.Resultado, resultado))
            return c.ToDto();

        // No se puede cambiar Ganada<->Perdida con un re-marcado (eso sería una corrección
        // explícita, no soportada aquí): se rechaza en vez de sobrescribir en silencio.
        if (!clsMaquinaEstados.PuedeMarcarResultado(c.Resultado, resultado))
            throw new clsConflictException("RESULTADO_YA_DEFINIDO",
                $"La cotización ya está marcada como {c.Resultado} y no puede cambiarse a {resultado}.");

        // Antes este comando permitía marcar el resultado directo desde el borrador,
        // registrando una "entrega implícita" solo para conservar la trazabilidad. Eso
        // ponía EntregadaAt sin que nadie hubiera llamado a /entregar de verdad,
        // contaminando el cumplimiento de SLA y las cotizaciones emitidas del tablero de
        // KPIs. Por decisión del negocio esa entrega implícita se elimina: SOLO
        // clsEntregarCotizacionCommand (POST /entregar) puede marcar la entrega, sin
        // excepciones.
        if (c.Estado != EstadoCotizacion.Entregada)
            throw new clsConflictException("COTIZACION_NO_ENTREGADA", "Solo se puede marcar el resultado de una cotización entregada.");

        var esNuevaGanada = resultado == ResultadoCotizacion.Ganada && c.Resultado != ResultadoCotizacion.Ganada;
        c.Resultado = resultado;

        // Estado de la solicitud + motivo de la pérdida (mismo formato que el rechazo).
        var solicitud = await solicitudes.ObtenerConClienteAsync(c.SolicitudId, ct);
        if (solicitud is not null)
        {
            // Una solicitud descartada no puede reactivarse marcando una cotización vieja.
            if (!clsMaquinaEstados.PuedeTransicionar(solicitud.Estado, EstadoSolicitud.Entregada))
                throw new clsConflictException("SOLICITUD_NO_TRANSICIONABLE",
                    $"La solicitud está {solicitud.Estado} y no puede pasar a Entregada.");
            solicitud.Estado = EstadoSolicitud.Entregada;
            if (resultado == ResultadoCotizacion.Perdida && !string.IsNullOrWhiteSpace(command.Motivo))
            {
                var condiciones = clsCondicionesServicioDto.Desde(solicitud.CondicionesServicio) with
                {
                    Rechazo = new clsMotivoRechazoDto(command.Motivo.Trim(),
                        string.IsNullOrWhiteSpace(command.Comentario) ? null : command.Comentario.Trim(),
                        clock.GetUtcNow()),
                };
                solicitud.CondicionesServicio = JsonSerializer.Serialize(condiciones, clsCondicionesServicioDto.Json);
            }
        }

        // Realimenta la Biblioteca: las ganadas se vuelven tarifas históricas reutilizables.
        // Solo al pasar A ganada por primera vez, y verificando que no existan ya para esta
        // cotización (doble protección contra duplicados en la Biblioteca).
        if (esNuevaGanada && !await tarifasHistoricas.ExistenParaCotizacionAsync(c.Id, ct))
        {
            var hoy = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            foreach (var linea in c.Lineas.Where(l => l.clsLineaSolicitud is not null))
            {
                tarifasHistoricas.Agregar(new clsTarifaHistorica
                {
                    RutaId = linea.clsLineaSolicitud!.RutaId,
                    ConfigVehiculoId = linea.clsLineaSolicitud.ConfigVehiculoId,
                    Tipologia = linea.clsLineaSolicitud.Tipologia,
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
            await publicador.PublicarAsync(new clsCotizacionMarcadaGanada(c.Id, c.SolicitudId, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
