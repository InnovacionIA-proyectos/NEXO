using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Notificaciones;

// Un handler por alerta y no uno genérico: asunto, cuerpo y destinatarios (por ROL) son decisiones de negocio distintas.

public sealed record clsNotificarTarifaVencidaCommand(Guid LineaId, string Justificacion, DateTimeOffset DetectadaEn)
    : ICommand<clsResultadoDeNotificacionDto>;

/// <summary>Urgencia Alta: AdminBiblioteca, Administrador de Tarifas y Servicios Logísticos.</summary>
public sealed class clsNotificarTarifaVencidaCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<clsNotificarTarifaVencidaCommand, clsResultadoDeNotificacionDto>
{
    public async Task<clsResultadoDeNotificacionDto> Handle(clsNotificarTarifaVencidaCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = new HashSet<string>(
            [.. await destinatarios.EmailsPorRolAsync(RolUsuario.AdminBiblioteca, ct),
             .. await destinatarios.EmailsPorRolAsync(RolUsuario.AdminTarifas, ct),
             .. await destinatarios.EmailsPorRolAsync(RolUsuario.ServiciosLogisticos, ct)]);
        if (email.Count == 0)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos en los roles a notificar.");

        var cuerpo = $"""
            <p>Se detectó una tarifa de plaza SAP <strong>vencida</strong> al costear la línea
            <strong>{command.LineaId}</strong>.</p>
            <p>{command.Justificacion}</p>
            <p>La línea quedó costeada igual (nunca por debajo del piso), pero conviene renovar
            esta tarifa cuanto antes.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Alta.</p>
            """;

        var id = await correo.EnviarAsync(new clsCorreoParaEnviar(
            [.. email], "[Nexo] Tarifa de plaza vencida detectada", cuerpo, []), ct);
        return clsResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}

public sealed record clsNotificarLineaFalloTecnicoCommand(Guid TrabajoId, Guid LineaId, string Motivo)
    : ICommand<clsResultadoDeNotificacionDto>;

/// <summary>Urgencia Media: Administrador de Tarifas (AdminTarifas); AdminBiblioteca no entra a la Mesa de Costeo.</summary>
public sealed class clsNotificarLineaFalloTecnicoCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<clsNotificarLineaFalloTecnicoCommand, clsResultadoDeNotificacionDto>
{
    public async Task<clsResultadoDeNotificacionDto> Handle(clsNotificarLineaFalloTecnicoCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = await destinatarios.EmailsPorRolAsync(RolUsuario.AdminTarifas, ct);
        if (email.Count == 0)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos con rol AdminTarifas.");

        var cuerpo = $"""
            <p>Una línea del trabajo <strong>{command.TrabajoId}</strong> no se pudo costear:
            agotó sus reintentos contra una fuente de la Biblioteca.</p>
            <p>Motivo técnico: {command.Motivo}</p>
            <p>El resto del pliego siguió su curso — solo esta línea quedó pendiente.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Media.</p>
            """;

        var id = await correo.EnviarAsync(new clsCorreoParaEnviar(
            email, "[Nexo] Línea sin costear por falla técnica", cuerpo, []), ct);
        return clsResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}

public sealed record clsNotificarCotizacionGanadaCommand(Guid CotizacionId, Guid SolicitudId, DateTimeOffset MarcadaEn)
    : ICommand<clsResultadoDeNotificacionDto>;

/// <summary>Urgencia Baja: Administrador de Reglas (AdminTarifas).</summary>
public sealed class clsNotificarCotizacionGanadaCommandHandler(
    IDestinatariosDeAlertaPort destinatarios, ICorreoPort correo)
    : ICommandHandler<clsNotificarCotizacionGanadaCommand, clsResultadoDeNotificacionDto>
{
    public async Task<clsResultadoDeNotificacionDto> Handle(clsNotificarCotizacionGanadaCommand command, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("La salida de correo corporativo no está configurada.");

        var email = await destinatarios.EmailsPorRolAsync(RolUsuario.AdminTarifas, ct);
        if (email.Count == 0)
            return clsResultadoDeNotificacionDto.NoHabiaQueEnviar("No hay usuarios activos con rol AdminTarifas.");

        var cuerpo = $"""
            <p>La cotización <strong>{command.CotizacionId}</strong> quedó marcada como
            <strong>Ganada</strong>.</p>
            <p>Sus tarifas ya se realimentaron a la Biblioteca de tarifas ganadas, reutilizables
            en futuras propuestas.</p>
            <p style="color:#777;font-size:12px">Correo automático de Nexo — Alerta de urgencia Baja.</p>
            """;

        var id = await correo.EnviarAsync(new clsCorreoParaEnviar(
            email, "[Nexo] Cotización marcada como ganada", cuerpo, []), ct);
        return clsResultadoDeNotificacionDto.Enviada(string.Join(", ", email), id, 0);
    }
}
