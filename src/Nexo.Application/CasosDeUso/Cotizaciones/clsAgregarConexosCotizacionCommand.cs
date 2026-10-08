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

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>LineaId (S4-HU-15.4/#250): a qué línea de la cotización aplica — obligatorio,
/// ya no existe el conexo "general" sin línea. Origen (S5-HU-18/#262): "individual" o
/// "general" — ver clsCotizacionConexo.Origen.</summary>
public sealed record clsConexoItem(Guid LineaId, Guid ServicioConexoId, decimal Cantidad, decimal? ValorAplicado, string Origen = "individual");

/// <summary>Agregar servicios conexos (logísticos/adicionales) a la cotización.</summary>
public sealed record clsAgregarConexosCotizacionCommand(
    Guid CotizacionId, IReadOnlyList<clsConexoItem> Items) : ICommand<clsCotizacionDto>;

public sealed class clsAgregarConexosCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    IServiciosConexosPort serviciosConexos,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsAgregarConexosCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

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

            var valor = item.ValorAplicado ?? servicio.CostoUnitario * item.Cantidad;
            var conexo = new clsCotizacionConexo
            {
                CotizacionId = c.Id,
                CotizacionLineaId = item.LineaId,
                ServicioConexoId = servicio.Id,
                clsServicioConexo = servicio, // nav explícita: el DTO denormaliza nombre/categoría/dimensiones
                Cantidad = item.Cantidad,
                ValorAplicado = valor,
                Origen = origen,
            };
            c.Conexos.Add(conexo);
        }
        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "agregar_conexos", $"{{\"items\":{command.Items.Count}}}");
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
