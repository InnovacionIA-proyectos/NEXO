using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.ServiciosConexos.Dtos;
using CabinaFletes.Application.ServiciosConexos.Ports;

namespace CabinaFletes.Application.ServiciosConexos.Commands;

/// <summary>PATCH: solo se actualizan los campos presentes (no nulos) en el comando.</summary>
public sealed record EditarServicioConexoCommand(
    Guid ServicioId, string? Nombre, string? Categoria, string? ReglaCobro,
    decimal? ValorBase, DateOnly? VigenteHasta, bool? Activo)
    : ICommand<ServicioConexoDto>;

public sealed class EditarServicioConexoCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<EditarServicioConexoCommand, ServicioConexoDto>
{
    public async Task<ServicioConexoDto> Handle(EditarServicioConexoCommand command, CancellationToken ct = default)
    {
        var servicio = await repo.ObtenerAsync(command.ServicioId, ct)
            ?? throw new NotFoundException("SERVICIO_NO_ENCONTRADO", "Servicio logístico no encontrado.");

        if (command.Nombre is not null) servicio.Nombre = command.Nombre.Trim();
        if (command.Categoria is not null) servicio.Categoria = ServicioConexoCategoria.Validada(command.Categoria);
        if (command.ReglaCobro is not null) servicio.ReglaCobro = command.ReglaCobro;
        if (command.ValorBase is not null) servicio.ValorBase = command.ValorBase;
        if (command.VigenteHasta is not null) servicio.VigenteHasta = command.VigenteHasta;
        if (command.Activo is not null) servicio.Activo = command.Activo.Value;
        servicio.ActualizadoPor = usuarioActual.Id;

        await uow.SaveChangesAsync(ct);
        return servicio.ToDto();
    }
}
