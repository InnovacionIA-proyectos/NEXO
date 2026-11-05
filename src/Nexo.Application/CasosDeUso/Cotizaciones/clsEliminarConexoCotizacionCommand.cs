using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// Quita un conexo de la cotización mientras no tenga resultado (Borrador o Entregada); Ganada/Perdida → 409 COTIZACION_CERRADA.
/// Tras la entrega marca <c>ModificadaTrasEntregaAt</c> y avisa a quien creó la solicitud (<see cref="clsCambioDeConexos"/>).
/// </summary>
public sealed record clsEliminarConexoCotizacionCommand(Guid CotizacionId, Guid ConexoId) : ICommand<clsCotizacionDto>;

public sealed class clsEliminarConexoCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    IPublicadorDeMensajes publicador,
    ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsEliminarConexoCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsEliminarConexoCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        clsCambioDeConexos.ExigirSinResultado(c);

        var conexo = c.Conexos.FirstOrDefault(x => x.Id == command.ConexoId)
            ?? throw new clsNotFoundException("CONEXO_NO_ENCONTRADO", "El servicio conexo no existe en esta cotización.");

        var quitado = new clsConexoCambiado(
            conexo.clsServicioConexo?.TipoServicio ?? "Servicio conexo", conexo.Cantidad, conexo.ValorVenta);

        c.Conexos.Remove(conexo);
        clsCotizacionCalculos.Recalcular(c);

        var trasEntrega = c.Estado == EstadoCotizacion.Entregada;
        await clsCambioDeConexos.RegistrarTrasEntregaAsync(c, "quitado", [quitado], solicitudes, publicador, usuarioActual, clock, ct);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "quitar_conexo",
            clsCambioDeConexos.DetalleDeAuditoria([quitado], trasEntrega));
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
