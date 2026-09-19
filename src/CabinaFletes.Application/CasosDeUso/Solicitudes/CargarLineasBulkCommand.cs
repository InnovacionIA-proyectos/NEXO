using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Mensajes.Solicitudes;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Carga masiva de líneas de un pliego de licitación.
///
/// Guarda las líneas y encola el auto-costeo; NO cuesta nada aquí. Devuelve el
/// identificador del trabajo, que es con lo que el cliente pregunta después por su
/// avance. Insertar 300 filas es rápido; costearlas no, y por eso lo segundo se va
/// a la cola.
/// </summary>
public sealed record CargarLineasBulkCommand(Guid SolicitudId, IReadOnlyList<CrearLineaInput> Lineas) : ICommand<Guid>;

public sealed class CargarLineasBulkCommandHandler(
    ISolicitudesRepository solicitudes,
    IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    IRegistroDeTrabajos trabajos,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<CargarLineasBulkCommand, Guid>
{
    public async Task<Guid> Handle(CargarLineasBulkCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new NotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        if (s.Estado is EstadoSolicitud.Entregada or EstadoSolicitud.Descartada)
            throw new ConflictException("SOLICITUD_NO_EDITABLE", $"No se pueden agregar líneas a una solicitud {s.Estado}.");

        var nuevas = command.Lineas.Select(lr => new LineaSolicitud
        {
            SolicitudId = s.Id,
            RutaId = lr.RutaId,
            ConfigVehiculoId = lr.ConfigVehiculoId,
            Tipologia = lr.Tipologia is null ? null : Enum.Parse<TipologiaCarga>(lr.Tipologia, ignoreCase: true),
            Peso = lr.Peso,
            ValorMercancia = lr.ValorMercancia,
        }).ToList();

        solicitudes.AgregarLineas(nuevas);
        auditoria.Registrar(usuarioActual.Id, "solicitudes", s.Id, "cargar_lineas_bulk", $"{{\"lineas\":{nuevas.Count}}}");

        // El orden importa y es la razón de ser del Outbox: publicar ANTES de confirmar
        // mete el mensaje en la misma transacción que las líneas. Si el commit falla, no
        // queda un trabajo encolado apuntando a líneas que nunca se guardaron.
        var trabajoId = Guid.NewGuid();
        var ahora = clock.GetUtcNow();

        // El trabajo se registra AQUÍ, no cuando la orquestación arranque. Así el
        // identificador que se devuelve con el 202 es consultable desde el instante en
        // que el cliente lo recibe, y no hay una ventana en la que responda 404 para un
        // identificador que sí es válido.
        trabajos.Registrar(trabajoId, s.Id, usuarioActual.Id, ahora);

        await publicador.PublicarAsync(
            new AutoCosteoSolicitado(trabajoId, s.Id, nuevas.Count, usuarioActual.Id, ahora), ct);

        await uow.SaveChangesAsync(ct);
        return trabajoId;
    }
}
