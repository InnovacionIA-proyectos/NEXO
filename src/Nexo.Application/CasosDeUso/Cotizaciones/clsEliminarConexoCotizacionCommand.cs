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
/// Quitar un conexo de la cotización. S5-HU-18/#262: antes no había forma de deshacer un
/// conexo agregado por error, solo dejarlo y compensar agregando otra fila.
/// S5-HU-20.1/#286 (HU-B24): se admite mientras la cotización no tenga resultado (Borrador o
/// Entregada); Ganada/Perdida → 409 COTIZACION_CERRADA. Tras la entrega deja la marca
/// <c>ModificadaTrasEntregaAt</c> y avisa a quien creó la solicitud (ver
/// <see cref="clsCambioDeConexos"/>). Es el "quitar conexo" del issue: ya existía como
/// DELETE, acá se amplía en vez de duplicarlo con un segundo comando.
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
            conexo.clsServicioConexo?.TipoServicio ?? "Servicio conexo", conexo.Cantidad, conexo.ValorAplicado);

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
