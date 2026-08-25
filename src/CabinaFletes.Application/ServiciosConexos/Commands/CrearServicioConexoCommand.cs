using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.ServiciosConexos.Dtos;
using CabinaFletes.Application.ServiciosConexos.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;

namespace CabinaFletes.Application.ServiciosConexos.Commands;

public sealed record CrearServicioConexoCommand(
    string Nombre, string? Categoria, string? ReglaCobro, decimal? ValorBase, DateOnly? VigenteHasta, bool Activo)
    : ICommand<ServicioConexoDto>;

public sealed class CrearServicioConexoCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<CrearServicioConexoCommand, ServicioConexoDto>
{
    public async Task<ServicioConexoDto> Handle(CrearServicioConexoCommand command, CancellationToken ct = default)
    {
        var nombre = command.Nombre.Trim();
        if (await repo.ExisteNombreAsync(nombre, ct))
            throw new ConflictException("SERVICIO_DUPLICADO", $"Ya existe un servicio logístico llamado '{nombre}'.");

        var servicio = new ServicioConexo
        {
            Nombre = nombre,
            Categoria = ServicioConexoCategoria.Validada(command.Categoria),
            ReglaCobro = command.ReglaCobro,
            ValorBase = command.ValorBase,
            VigenteHasta = command.VigenteHasta,
            Activo = command.Activo,
            ActualizadoPor = usuarioActual.Id,
        };
        repo.Agregar(servicio);
        await uow.SaveChangesAsync(ct);
        return servicio.ToDto();
    }
}
