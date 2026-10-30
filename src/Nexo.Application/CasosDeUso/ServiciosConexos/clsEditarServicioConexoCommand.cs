using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// PATCH: solo se actualizan los campos presentes (no nulos) en el comando. El precio de venta se borra con <paramref name="LimpiarPrecioVenta"/>
/// (un <c>null</c> en <paramref name="PrecioVenta"/> significa "no tocar"); pedir las dos cosas a la vez es un error.
/// </summary>
public sealed record clsEditarServicioConexoCommand(
    Guid ServicioId, string? CodigoServicio, string? TipoServicio, string? Origen, string? Destino,
    string? Puerto, string? Ruta, string? TipoMaquina, decimal? CostoUnitario, string? Unidad,
    DateOnly? Vigencia, string? Categoria, bool? Activo, decimal? PrecioVenta = null, bool LimpiarPrecioVenta = false)
    : ICommand<clsServicioConexoDto>;

public sealed class clsEditarServicioConexoCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsEditarServicioConexoCommand, clsServicioConexoDto>
{
    public async Task<clsServicioConexoDto> Handle(clsEditarServicioConexoCommand command, CancellationToken ct = default)
    {
        var servicio = await repo.ObtenerAsync(command.ServicioId, ct)
            ?? throw new clsNotFoundException("SERVICIO_NO_ENCONTRADO", "Servicio logístico no encontrado.");

        if (command.LimpiarPrecioVenta && command.PrecioVenta is not null)
            throw new clsValidationFailedException("PRECIO_VENTA_AMBIGUO", "Envía precioVenta o limpiarPrecioVenta, no los dos a la vez.");

        var precioVentaAntes = servicio.PrecioVenta;
        if (command.CodigoServicio is not null) servicio.CodigoServicio = command.CodigoServicio.Trim();
        if (command.TipoServicio is not null) servicio.TipoServicio = command.TipoServicio.Trim();
        if (command.Origen is not null) servicio.Origen = Normalizar(command.Origen);
        if (command.Destino is not null) servicio.Destino = Normalizar(command.Destino);
        if (command.Puerto is not null) servicio.Puerto = Normalizar(command.Puerto);
        if (command.Ruta is not null) servicio.Ruta = Normalizar(command.Ruta);
        if (command.TipoMaquina is not null) servicio.TipoMaquina = Normalizar(command.TipoMaquina);
        if (command.CostoUnitario is not null) servicio.CostoUnitario = command.CostoUnitario.Value;
        if (command.LimpiarPrecioVenta) servicio.PrecioVenta = null;
        else if (command.PrecioVenta is { } precioVenta) servicio.PrecioVenta = Math.Round(precioVenta, 2, MidpointRounding.AwayFromZero);
        if (command.Unidad is not null) servicio.Unidad = command.Unidad.Trim();
        if (command.Vigencia is not null) servicio.Vigencia = command.Vigencia.Value;
        if (command.Categoria is not null) servicio.Categoria = clsServicioConexoCategoria.Validada(command.Categoria);
        if (command.Activo is not null) servicio.Activo = command.Activo.Value;
        servicio.ActualizadoPor = usuarioActual.Id;

        // Cualquiera de las 6 dimensiones pudo cambiar: revalidar duplicado contra el
        // estado final, excluyendo la propia fila.
        if (await repo.ExisteDimensionesAsync(
                servicio.CodigoServicio, servicio.Origen, servicio.Destino, servicio.Puerto,
                servicio.Ruta, servicio.TipoMaquina, servicio.Id, ct))
            throw new clsConflictException("SERVICIO_DUPLICADO",
                $"Ya existe otro servicio '{servicio.CodigoServicio}' con esa misma combinación de origen/destino/puerto/ruta/tipo de máquina.");

        if (servicio.PrecioVenta != precioVentaAntes) repo.RegistrarCambioDePrecioVenta(servicio, precioVentaAntes);

        await uow.SaveChangesAsync(ct);
        return servicio.ToDto();
    }

    private static string Normalizar(string? dimension) =>
        string.IsNullOrWhiteSpace(dimension) ? clsServicioConexo.NoAplica : dimension.Trim();
}
