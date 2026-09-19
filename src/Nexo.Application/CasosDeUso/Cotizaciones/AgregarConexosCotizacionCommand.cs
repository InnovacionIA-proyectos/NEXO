using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

public sealed record ConexoItem(Guid ServicioConexoId, decimal Cantidad, decimal? ValorAplicado);

/// <summary>Agregar servicios conexos (logísticos/adicionales) a la cotización.</summary>
public sealed record AgregarConexosCotizacionCommand(
    Guid CotizacionId, IReadOnlyList<ConexoItem> Items) : ICommand<CotizacionDto>;

public sealed class AgregarConexosCotizacionCommandHandler(
    ICotizacionRepository cotizaciones,
    IServiciosConexosPort serviciosConexos,
    IAuditoriaPort auditoria,
    Common.ICurrentUserPort usuarioActual,
    IUnitOfWork uow)
    : ICommandHandler<AgregarConexosCotizacionCommand, CotizacionDto>
{
    public async Task<CotizacionDto> Handle(AgregarConexosCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new NotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new ConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        foreach (var item in command.Items)
        {
            var servicio = await serviciosConexos.ObtenerPorIdAsync(item.ServicioConexoId, ct)
                ?? throw new ValidationFailedException("SERVICIO_INVALIDO", $"Servicio logístico {item.ServicioConexoId} no existe.");
            if (!servicio.Activo)
                throw new ValidationFailedException("SERVICIO_INACTIVO", $"El servicio '{servicio.Nombre}' está inactivo.");

            var valor = item.ValorAplicado ?? (servicio.ValorBase ?? 0m) * item.Cantidad;
            var conexo = new CotizacionConexo
            {
                CotizacionId = c.Id,
                ServicioConexoId = servicio.Id,
                ServicioConexo = servicio, // nav explícita: el DTO denormaliza nombre/categoría
                Cantidad = item.Cantidad,
                ValorAplicado = valor
            };
            c.Conexos.Add(conexo);
        }
        CotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "agregar_conexos", $"{{\"items\":{command.Items.Count}}}");
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
