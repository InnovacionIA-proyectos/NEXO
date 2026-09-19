using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.CasosDeUso.ServiciosConexos.Interfaces;
using Nexo.Application.CasosDeUso.ServiciosConexos.Mappers;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

public sealed record clsCrearServicioConexoCommand(
    string Nombre, string? Categoria, string? ReglaCobro, decimal? ValorBase, DateOnly? VigenteHasta, bool Activo)
    : ICommand<clsServicioConexoDto>;

public sealed class clsCrearServicioConexoCommandHandler(
    IServiciosConexosRepository repo, ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsCrearServicioConexoCommand, clsServicioConexoDto>
{
    public async Task<clsServicioConexoDto> Handle(clsCrearServicioConexoCommand command, CancellationToken ct = default)
    {
        var nombre = command.Nombre.Trim();
        if (await repo.ExisteNombreAsync(nombre, ct))
            throw new clsConflictException("SERVICIO_DUPLICADO", $"Ya existe un servicio logístico llamado '{nombre}'.");

        var servicio = new clsServicioConexo
        {
            Nombre = nombre,
            Categoria = clsServicioConexoCategoria.Validada(command.Categoria),
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
