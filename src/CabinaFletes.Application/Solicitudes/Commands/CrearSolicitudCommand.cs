using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Scoring;

namespace CabinaFletes.Application.Solicitudes.Commands;

/// <summary>Alta de una solicitud spot (o cabecera de licitación; las líneas se cargan luego en bulk).</summary>
public sealed record CrearSolicitudCommand(
    Guid ClienteId, string Tipo, string? Modalidad, DateTimeOffset? Deadline,
    decimal? ValorEstimado, decimal? ProbabilidadCierre, CrearLineaInput? Linea) : ICommand<Guid>;

public sealed record CrearLineaInput(Guid? RutaId, Guid? ConfigVehiculoId, string? Tipologia, decimal? Peso, decimal? ValorMercancia);

public sealed class CrearSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IClientesPort clientes, IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual, TimeProvider clock, IUnitOfWork uow)
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
        await uow.SaveChangesAsync(ct);
        return solicitud.Id;
    }

    private static T ParseEnum<T>(string value, string campo) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var v)
            ? v
            : throw new ValidationFailedException("ENUM_INVALIDO", $"Valor inválido para '{campo}': '{value}'.");
}
