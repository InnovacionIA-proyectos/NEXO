using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Bandeja;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Alta de una solicitud spot (o cabecera de licitación; las líneas se cargan luego en bulk).</summary>
public sealed record CrearSolicitudCommand(
    Guid ClienteId, string Tipo, string? Modalidad, DateTimeOffset? Deadline,
    decimal? ValorEstimado, decimal? ProbabilidadCierre, CrearLineaInput? Linea) : ICommand<Guid>;

public sealed record CrearLineaInput(Guid? RutaId, Guid? ConfigVehiculoId, string? Tipologia, decimal? Peso, decimal? ValorMercancia);

public sealed class CrearSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IClientesPort clientes, IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual, TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow)
    : ICommandHandler<CrearSolicitudCommand, Guid>
{
    public async Task<Guid> Handle(CrearSolicitudCommand command, CancellationToken ct = default)
    {
        if (!await clientes.ExisteAsync(command.ClienteId, ct))
            throw new ValidationFailedException("CLIENTE_INVALIDO", "El cliente indicado no existe.");

        var solicitud = new Solicitud
        {
            ClienteId = command.ClienteId,
            Tipo = ParseEnum<TipoSolicitud>(command.Tipo, "tipo"),
            Modalidad = command.Modalidad is null ? null : ParseEnum<ModalidadSolicitud>(command.Modalidad, "modalidad"),
            Deadline = command.Deadline,
            ValorEstimado = command.ValorEstimado,
            ProbabilidadCierre = command.ProbabilidadCierre,
            Estado = EstadoSolicitud.Pendiente,
            CreadoPor = usuarioActual.Id,
        };

        if (command.Linea is { } lr)
            solicitud.Lineas.Add(new LineaSolicitud
            {
                SolicitudId = solicitud.Id,
                RutaId = lr.RutaId,
                ConfigVehiculoId = lr.ConfigVehiculoId,
                Tipologia = lr.Tipologia is null ? null : ParseEnum<TipologiaCarga>(lr.Tipologia, "tipologia"),
                Peso = lr.Peso,
                ValorMercancia = lr.ValorMercancia,
            });

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        solicitud.ScorePrioridad = (decimal)ScorePrioridadCalculator.Calcular(solicitud, parametros, clock.GetUtcNow()).Total;

        solicitudes.Agregar(solicitud);
        auditoria.Registrar(usuarioActual.Id, "solicitudes", solicitud.Id, "crear", $"{{\"tipo\":\"{solicitud.Tipo}\"}}");
        // La bandeja tiene una fila más. El aviso sale por el Outbox, o sea en la MISMA
        // transacción: no puede quedar la solicitud guardada sin avisar, ni avisado un alta
        // que se acabó revirtiendo.
        await publicador.PublicarAsync(
            new BandejaCambio(MotivoDeCambio.SolicitudNueva, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return solicitud.Id;
    }

    private static T ParseEnum<T>(string value, string campo) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var v)
            ? v
            : throw new ValidationFailedException("ENUM_INVALIDO", $"Valor inválido para '{campo}': '{value}'.");
}
