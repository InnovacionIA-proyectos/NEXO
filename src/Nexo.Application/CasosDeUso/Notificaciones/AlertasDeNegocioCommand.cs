using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Notificaciones;

// ---------------------------------------------------------------------------
// Las 3 alertas de negocio de la sección 9 del documento de entendimiento — hoy la
// única notificación que existía era "cotización entregada" (al cliente interno que
// pidió la consulta), y ninguna de estas tres, pese a ser las que sostienen la
// confianza del comercial en la propuesta (riesgo de "volver al Excel", sección 12).
//
// Las tres comparten forma: resolver destinatarios por ROL (no por una solicitud con
// creador identificable, como la de entrega) y mandar un correo simple por el mismo
// ICorreoPort/MailSender ya en producción. Un handler por alerta y no uno genérico
// parametrizado: el asunto, el cuerpo y a quién le llega son decisiones de negocio
// distintas para cada una, y ya son solo 3 — no hay abstracción que valga la pena.
// ---------------------------------------------------------------------------

public sealed record NotificarTarifaVencidaCommand(Guid LineaId, string Justificacion, DateTimeOffset DetectadaEn)
    : ICommand<ResultadoDeNotificacionDto>;

/// <summary>Urgencia Alta: Administrador de Fletes y Administrador de Reglas (partido hoy entre AdminTarifas y ServiciosLogisticos — ver docs/ROLES_Y_PERMISOS.md).</summary>
public sealed class NotificarTarifaVencidaCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<NotificarTarifaVencidaCommand, ResultadoDeNotificacionDto>
{
    public async Task<ResultadoDeNotificacionDto> Handle(NotificarTarifaVencidaCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = new HashSet<string>(
            [.. await destinatarios.EmailsPorRolAsync(RolUsuario.CoordinadorFletes, ct),
             .. await destinatarios.EmailsPorRolAsync(RolUsuario.AdminTarifas, ct),
             .. await destinatarios.EmailsPorRolAsync(RolUsuario.ServiciosLogisticos, ct)]);
        if (email.Count == 0)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos en los roles a notificar.");

        var cuerpo = $"""
            <p>Se detectó una tarifa de plaza SAP <strong>vencida</strong> al costear la línea
            <strong>{command.LineaId}</strong>.</p>
            <p>{command.Justificacion}</p>
            <p>La línea quedó costeada igual (nunca por debajo del piso), pero conviene renovar
            esta tarifa cuanto antes.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Alta.</p>
            """;

        var id = await correo.EnviarAsync(new CorreoParaEnviar(
            [.. email], "[Nexo] Tarifa de plaza vencida detectada", cuerpo, []), ct);
        return ResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}

public sealed record NotificarLineaFalloTecnicoCommand(Guid TrabajoId, Guid LineaId, string Motivo)
    : ICommand<ResultadoDeNotificacionDto>;

/// <summary>Urgencia Media: Administrador de Fletes (CoordinadorFletes).</summary>
public sealed class NotificarLineaFalloTecnicoCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<NotificarLineaFalloTecnicoCommand, ResultadoDeNotificacionDto>
{
    public async Task<ResultadoDeNotificacionDto> Handle(NotificarLineaFalloTecnicoCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = await destinatarios.EmailsPorRolAsync(RolUsuario.CoordinadorFletes, ct);
        if (email.Count == 0)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos con rol CoordinadorFletes.");

        var cuerpo = $"""
            <p>Una línea del trabajo <strong>{command.TrabajoId}</strong> no se pudo costear:
            agotó sus reintentos contra una fuente de la Biblioteca.</p>
            <p>Motivo técnico: {command.Motivo}</p>
            <p>El resto del pliego siguió su curso — solo esta línea quedó pendiente.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Media.</p>
            """;

        var id = await correo.EnviarAsync(new CorreoParaEnviar(
            email, "[Nexo] Línea sin costear por falla técnica", cuerpo, []), ct);
        return ResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}

public sealed record NotificarCotizacionGanadaCommand(Guid CotizacionId, Guid SolicitudId, DateTimeOffset MarcadaEn)
    : ICommand<ResultadoDeNotificacionDto>;

/// <summary>Urgencia Baja: Administrador de Reglas (AdminTarifas).</summary>
public sealed class NotificarCotizacionGanadaCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<NotificarCotizacionGanadaCommand, ResultadoDeNotificacionDto>
{
    public async Task<ResultadoDeNotificacionDto> Handle(NotificarCotizacionGanadaCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = await destinatarios.EmailsPorRolAsync(RolUsuario.AdminTarifas, ct);
        if (email.Count == 0)
            return ResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos con rol AdminTarifas.");

        var cuerpo = $"""
            <p>La cotización <strong>{command.CotizacionId}</strong> quedó marcada como
            <strong>Ganada</strong>.</p>
            <p>Sus tarifas ya se realimentaron a la Biblioteca de tarifas ganadas, reutilizables
            en futuras propuestas.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Baja.</p>
            """;

        var id = await correo.EnviarAsync(new CorreoParaEnviar(
            email, "[Nexo] Cotización marcada como ganada", cuerpo, []), ct);
        return ResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}
