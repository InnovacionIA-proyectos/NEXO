using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Solicitudes;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Carga masiva de líneas de un pliego de licitación.
///
/// Guarda las líneas y encola el auto-costeo; NO cuesta nada aquí. Devuelve el
/// identificador del trabajo, que es con lo que el cliente pregunta después por su
/// avance. Insertar 300 filas es rápido; costearlas no, y por eso lo segundo se va
/// a la cola.
/// </summary>
public sealed record clsCargarLineasBulkCommand(Guid SolicitudId, IReadOnlyList<clsCrearLineaInput> Lineas) : ICommand<Guid>;

public sealed class clsCargarLineasBulkCommandHandler(
    ISolicitudesRepository solicitudes,
    IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual,
    IPublicadorDeMensajes publicador,
    IRegistroDeTrabajos trabajos,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsCargarLineasBulkCommand, Guid>
{
    public async Task<Guid> Handle(clsCargarLineasBulkCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");

        if (s.Estado is EstadoSolicitud.Entregada or EstadoSolicitud.Descartada)
            throw new clsConflictException("SOLICITUD_NO_EDITABLE", $"No se pueden agregar líneas a una solicitud {s.Estado}.");

        // S5-HU-17/#263: horas por línea, mismo criterio que clsCapturarSolicitudCommand
        // (#251) — si la línea no trae las suyas, cae al default de ESTA solicitud (la
        // que ya tenga en CondicionesServicio, o 4h+4h si nunca se serializó — ver
        // clsCrearSolicitudCommand, que no guarda condiciones propias).
        var tiemposSolicitud = clsCondicionesServicioDto.Desde(s.CondicionesServicio).TiemposLogisticos!;
        // S5-HU-18.3/#275: numera en orden de este lote (1..N). Pensado para el caso
        // común — un bulk sobre una solicitud recién creada, sin líneas previas — no
        // continúa la numeración de un bulk anterior sobre la misma solicitud.
        var nuevas = command.Lineas.Select((lr, i) =>
        {
            var (horasCargue, horasDescargue) = clsResolucionHorasLinea.Resolver(
                lr.HorasCargue, lr.HorasDescargue, retornaContenedor: false,
                tiemposSolicitud.HorasCargue, tiemposSolicitud.HorasDescargue);
            return new clsLineaSolicitud
            {
                SolicitudId = s.Id,
                RutaId = lr.RutaId,
                ConfigVehiculoId = lr.ConfigVehiculoId,
                Tipologia = lr.Tipologia is null ? null : Enum.Parse<TipologiaCarga>(lr.Tipologia, ignoreCase: true),
                Peso = lr.Peso,
                ValorMercancia = lr.ValorMercancia,
                HorasCargue = horasCargue,
                HorasDescargue = horasDescargue,
                GrupoRutaId = lr.GrupoRutaId,
                NumeroLinea = i + 1,
            };
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
            new clsAutoCosteoSolicitado(trabajoId, s.Id, nuevas.Count, usuarioActual.Id, ahora), ct);

        await uow.SaveChangesAsync(ct);
        return trabajoId;
    }
}
