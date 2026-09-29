using Nexo.Application.CasosDeUso.RangosPeso.DTOs;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.RangosPeso;

/// <summary>El Administrador de Tarifas agrega una banda de peso al catálogo (S4-HU-10.1)
/// — no es un catálogo cerrado, el sistema externo permite bandas puntuales nuevas.</summary>
public sealed record clsCrearRangoPesoCommand(int PesoMinimo, int? PesoMaximo) : ICommand<clsRangoPesoDto>;

public sealed class clsCrearRangoPesoCommandHandler(IRangosPesoRepository rangos, IUnitOfWork uow)
    : ICommandHandler<clsCrearRangoPesoCommand, clsRangoPesoDto>
{
    public async Task<clsRangoPesoDto> Handle(clsCrearRangoPesoCommand command, CancellationToken ct = default)
    {
        clsRangoPesoValidador.Validar(command.PesoMinimo, command.PesoMaximo);

        if (await rangos.ExisteAsync(command.PesoMinimo, command.PesoMaximo, null, ct))
            throw new clsConflictException("RANGO_PESO_DUPLICADO", "Ya existe una banda de peso con ese mínimo y máximo.");

        var rango = new clsRangoPeso { PesoMinimo = command.PesoMinimo, PesoMaximo = command.PesoMaximo };
        rangos.Agregar(rango);
        await uow.SaveChangesAsync(ct);
        return new clsRangoPesoDto(rango.Id, rango.PesoMinimo, rango.PesoMaximo);
    }
}
