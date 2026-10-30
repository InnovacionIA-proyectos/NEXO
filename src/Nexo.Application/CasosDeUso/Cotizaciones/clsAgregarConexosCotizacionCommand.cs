using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.Cotizaciones.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Notificaciones;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>LineaId (S4-HU-15.4/#250): a qué línea de la cotización aplica — obligatorio,
/// ya no existe el conexo "general" sin línea. Origen (S5-HU-18/#262): "individual" o
/// "general" — ver clsCotizacionConexo.Origen.</summary>
public sealed record clsConexoItem(Guid LineaId, Guid ServicioConexoId, decimal Cantidad, decimal? ValorAplicado, string Origen = "individual");

/// <summary>
/// Agregar servicios conexos (logísticos/adicionales) a la cotización. S5-HU-20.1/#286:
/// se admite mientras la cotización no tenga resultado (Borrador o Entregada); si ya está
/// Ganada/Perdida → 409 COTIZACION_CERRADA. Tras la entrega deja la marca
/// <c>ModificadaTrasEntregaAt</c> y avisa a quien creó la solicitud (ver
/// <see cref="clsCambioDeConexos"/>).
/// </summary>
public sealed record clsAgregarConexosCotizacionCommand(
    Guid CotizacionId, IReadOnlyList<clsConexoItem> Items) : ICommand<clsCotizacionDto>;

public sealed class clsAgregarConexosCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    IServiciosConexosPort serviciosConexos,
    ISolicitudesPort solicitudes,
    IAuditoriaPort auditoria,
    IPublicadorDeMensajes publicador,
    Common.ICurrentUserPort usuarioActual,
    TimeProvider clock,
    IUnitOfWork uow)
    : ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsAgregarConexosCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        clsCambioDeConexos.ExigirSinResultado(c);

        var agregados = new List<clsConexoCambiado>();
        foreach (var item in command.Items)
        {
            if (c.Lineas.All(l => l.Id != item.LineaId))
                throw new clsValidationFailedException("LINEA_INVALIDA", $"La línea {item.LineaId} no pertenece a esta cotización.");

            clsConexoCotizacionValidador.ValidarCantidad(item.Cantidad);
            var origen = clsConexoCotizacionValidador.NormalizarOrigen(item.Origen);

            var servicio = await serviciosConexos.ObtenerPorIdAsync(item.ServicioConexoId, ct)
                ?? throw new clsValidationFailedException("SERVICIO_INVALIDO", $"Servicio logístico {item.ServicioConexoId} no existe.");
            if (!servicio.Activo)
                throw new clsValidationFailedException("SERVICIO_INACTIVO", $"El servicio '{servicio.TipoServicio}' está inactivo.");

            // El costo (ValorAplicado) puede venir en el request; lo que se cotiza sale siempre de la biblioteca: el precio de venta o, si el
            // servicio no tiene, su costo. Queda fijo en el conexo aunque la biblioteca cambie después.
            var valor = item.ValorAplicado ?? servicio.CostoUnitario * item.Cantidad;
            var precioVentaUnitario = Math.Round(servicio.PrecioVenta ?? servicio.CostoUnitario, 2, MidpointRounding.AwayFromZero);
            var conexo = new clsCotizacionConexo
            {
                CotizacionId = c.Id,
                CotizacionLineaId = item.LineaId,
                ServicioConexoId = servicio.Id,
                clsServicioConexo = servicio, // nav explícita: el DTO denormaliza nombre/categoría/dimensiones
                Cantidad = item.Cantidad,
                ValorAplicado = valor,
                PrecioVentaUnitario = precioVentaUnitario,
                Origen = origen,
            };
            c.Conexos.Add(conexo);
            agregados.Add(new clsConexoCambiado(servicio.TipoServicio, item.Cantidad, conexo.ValorVenta));
        }
        clsCotizacionCalculos.Recalcular(c);

        var trasEntrega = c.Estado == EstadoCotizacion.Entregada;
        await clsCambioDeConexos.RegistrarTrasEntregaAsync(c, "agregado", agregados, solicitudes, publicador, usuarioActual, clock, ct);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "agregar_conexos",
            clsCambioDeConexos.DetalleDeAuditoria(agregados, trasEntrega));
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
