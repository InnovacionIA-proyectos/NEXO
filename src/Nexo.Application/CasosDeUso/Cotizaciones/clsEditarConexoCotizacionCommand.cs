using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Mappers;
using Nexo.Application.CasosDeUso.Cotizaciones.Validadores;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

/// <summary>
/// S5-HU-18/#262: corregir la cantidad (y opcionalmente el costo, <c>ValorAplicado</c>) de un conexo ya
/// agregado — antes la única forma de corregir un error de cantidad era agregar otra fila,
/// sin poder deshacer la original.
/// </summary>
public sealed record clsEditarConexoCotizacionCommand(
    Guid CotizacionId, Guid ConexoId, decimal Cantidad, decimal? ValorAplicado) : ICommand<clsCotizacionDto>;

public sealed class clsEditarConexoCotizacionCommandHandler(
    ICotizacionRepository cotizaciones, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEditarConexoCotizacionCommand, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsEditarConexoCotizacionCommand command, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(command.CotizacionId, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        if (c.Estado != EstadoCotizacion.Borrador)
            throw new clsConflictException("COTIZACION_NO_EDITABLE", "La cotización ya fue entregada y no admite cambios.");

        var conexo = c.Conexos.FirstOrDefault(x => x.Id == command.ConexoId)
            ?? throw new clsNotFoundException("CONEXO_NO_ENCONTRADO", "El servicio conexo no existe en esta cotización.");

        clsConexoCotizacionValidador.ValidarCantidad(command.Cantidad);

        var valor = command.ValorAplicado
            ?? (conexo.clsServicioConexo is { } servicio
                ? servicio.CostoUnitario * command.Cantidad
                : throw new clsValidationFailedException("VALOR_REQUERIDO", "No se pudo recalcular el valor: envíe valorAplicado."));

        conexo.Cantidad = command.Cantidad;
        conexo.ValorAplicado = valor;
        // El valor de venta se recalcula con el precio de venta que quedó fijo al agregar el conexo; el costo del request no lo cambia.

        clsCotizacionCalculos.Recalcular(c);
        auditoria.Registrar(usuarioActual.Id, "cotizaciones", c.Id, "editar_conexo",
            System.Text.Json.JsonSerializer.Serialize(new { conexoId = conexo.Id, cantidad = command.Cantidad, valorAplicado = valor, precioVentaUnitario = conexo.PrecioVentaUnitario, valorVenta = conexo.ValorVenta }));
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }
}
