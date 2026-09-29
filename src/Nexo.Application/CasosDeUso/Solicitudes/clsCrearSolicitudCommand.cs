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
public sealed record clsCrearSolicitudCommand(
    Guid? ClienteId, string? ClienteBp, string Tipo, string? Modalidad, DateTimeOffset? Deadline,
    decimal? ValorEstimado, decimal? ProbabilidadCierre, clsCrearLineaInput? Linea) : ICommand<Guid>;

public sealed record clsCrearLineaInput(Guid? RutaId, Guid? ConfigVehiculoId, string? Tipologia, decimal? Peso, decimal? ValorMercancia);

public sealed class clsCrearSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IClientesPort clientes, IClientesDwPort clientesDw, IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual, TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow)
    : ICommandHandler<clsCrearSolicitudCommand, Guid>
{
    public async Task<Guid> Handle(clsCrearSolicitudCommand command, CancellationToken ct = default)
    {
        // Issue #206: el buscador de clientes nunca traía un clienteId ya resuelto para
        // un cliente real del catálogo DW — clienteBp resuelve/crea el espejo local por
        // NIT, mismo mecanismo ya probado en clsCapturarSolicitudCommand.
        var clienteId = await clsResolucionDeCliente.ResolverAsync(command.ClienteId, command.ClienteBp, clientes, clientesDw, ct);

        var solicitud = new clsSolicitud
        {
            ClienteId = clienteId,
            Tipo = ParseEnum<TipoSolicitud>(command.Tipo, "tipo"),
            Modalidad = command.Modalidad is null ? null : ParseEnum<ModalidadSolicitud>(command.Modalidad, "modalidad"),
            Deadline = command.Deadline,
            ValorEstimado = command.ValorEstimado,
            ProbabilidadCierre = command.ProbabilidadCierre,
            Estado = EstadoSolicitud.Pendiente,
            CreadoPor = usuarioActual.Id,
        };

        if (command.Linea is { } lr)
            solicitud.Lineas.Add(new clsLineaSolicitud
            {
                SolicitudId = solicitud.Id,
                RutaId = lr.RutaId,
                ConfigVehiculoId = lr.ConfigVehiculoId,
                Tipologia = lr.Tipologia is null ? null : ParseEnum<TipologiaCarga>(lr.Tipologia, "tipologia"),
                Peso = lr.Peso,
                ValorMercancia = lr.ValorMercancia,
            });

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        solicitud.ScorePrioridad = (decimal)clsScorePrioridadCalculator.Calcular(solicitud, parametros, clock.GetUtcNow()).Total;

        solicitudes.Agregar(solicitud);
        auditoria.Registrar(usuarioActual.Id, "solicitudes", solicitud.Id, "crear", $"{{\"tipo\":\"{solicitud.Tipo}\"}}");
        // La bandeja tiene una fila más. El aviso sale por el Outbox, o sea en la MISMA
        // transacción: no puede quedar la solicitud guardada sin avisar, ni avisado un alta
        // que se acabó revirtiendo.
        await publicador.PublicarAsync(
            new clsBandejaCambio(clsMotivoDeCambio.SolicitudNueva, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return solicitud.Id;
    }

    private static T ParseEnum<T>(string value, string campo) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var v)
            ? v
            : throw new clsValidationFailedException("ENUM_INVALIDO", $"Valor inválido para '{campo}': '{value}'.");
}
