using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>S5-HU-18/#262: quitar un conexo agregado por error — antes no había forma de
/// deshacerlo, solo de dejarlo y compensar agregando otra fila.</summary>
public sealed record clsEliminarConexoCotizacionCommand(Guid CotizacionId, Guid ConexoId) : ICommand<clsCotizacionDto>;

public sealed class clsEliminarConexoCotizacionCommandHandler(
    ICotizacionRepository cotizaciones, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEliminarConexoCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsEliminarConexoCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var conexo = c.Conexos.FirstOrDefault(x => x.Id == command.ConexoId)
            ?? throw new clsNotFoundException("CONEXO_NO_ENCONTRADO", "El servicio conexo no existe en esta cotización.");

        c.Conexos.Remove(conexo);
        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "eliminar_conexo", $"{{\"conexoId\":\"{conexo.Id}\"}}");
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
