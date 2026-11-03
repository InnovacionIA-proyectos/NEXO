using Nexo.Application.CasosDeUso.RangosPeso.DTOs;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.RangosPeso;

public sealed record clsEditarRangoPesoCommand(int Id, int PesoMinimo, int? PesoMaximo) : ICommand<clsRangoPesoDto>;

public sealed class clsEditarRangoPesoCommandHandler(IRangosPesoRepository rangos, IUnitOfWork uow)
    : ICommandHandler<clsEditarRangoPesoCommand, clsRangoPesoDto>
{
    public async Task<clsRangoPesoDto> Handle(clsEditarRangoPesoCommand command, CancellationToken ct = default)
    {
        clsRangoPesoValidador.Validar(command.PesoMinimo, command.PesoMaximo);

        var rango = await rangos.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("RANGO_PESO_NO_ENCONTRADO", "No existe una banda de peso con ese identificador.");

        if (await rangos.ExisteAsync(command.PesoMinimo, command.PesoMaximo, command.Id, ct))
            throw new clsConflictException("RANGO_PESO_DUPLICADO", "Ya existe otra banda de peso con ese mínimo y máximo.");

        rango.PesoMinimo = command.PesoMinimo;
        rango.PesoMaximo = command.PesoMaximo;
        await uow.SaveChangesAsync(ct);
        return new clsRangoPesoDto(rango.Id, rango.PesoMinimo, rango.PesoMaximo);
    }
}
