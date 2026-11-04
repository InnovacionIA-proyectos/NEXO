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

/// <summary>Carga masiva de líneas de un pliego: guarda las líneas y encola el auto-costeo (no cuesta aquí); devuelve el identificador del trabajo para consultar el avance.</summary>
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

        // Horas por línea: si no trae las suyas, el default de ESTA solicitud (CondicionesServicio, o 4h+4h si nunca se serializó).
        var tiemposSolicitud = clsCondicionesServicioDto.Desde(s.CondicionesServicio).TiemposLogisticos!;
        // Numera en orden de este lote (1..N); no continúa la numeración de un bulk anterior sobre la misma solicitud.
        var nuevas = command.Lineas.Select((lr, i) =>
        {
            var (horasCargue, horasDescargue) = clsResolucionHorasLinea.Resolver(
                lr.HorasCargue, lr.HorasDescargue, esDevolucionContenedor: false,
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

        // El trabajo se registra AQUÍ para que el identificador devuelto con el 202 sea consultable desde que el cliente lo recibe, sin ventana de 404.
        trabajos.Registrar(trabajoId, s.Id, usuarioActual.Id, ahora);

        await publicador.PublicarAsync(
            new clsAutoCosteoSolicitado(trabajoId, s.Id, nuevas.Count, usuarioActual.Id, ahora), ct);

        await uow.SaveChangesAsync(ct);
        return trabajoId;
    }
}
