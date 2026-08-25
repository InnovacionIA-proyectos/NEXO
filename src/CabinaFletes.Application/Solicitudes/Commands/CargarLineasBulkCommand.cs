using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.Solicitudes.Commands;

/// <summary>Carga masiva de líneas de un pliego de licitación.</summary>
public sealed record CargarLineasBulkCommand(Guid SolicitudId, IReadOnlyList<CrearLineaInput> Lineas) : ICommand<int>;

public sealed class CargarLineasBulkCommandHandler(
    ISolicitudesRepository solicitudes, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<CargarLineasBulkCommand, int>
{
    public async Task<int> Handle(CargarLineasBulkCommand command, CancellationToken ct = default)
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
        await uow.SaveChangesAsync(ct);
        return nuevas.Count;
    }
}
