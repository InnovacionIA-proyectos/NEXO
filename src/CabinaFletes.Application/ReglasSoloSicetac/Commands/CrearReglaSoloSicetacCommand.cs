using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.ReglasSoloSicetac.Dtos;
using CabinaFletes.Application.ReglasSoloSicetac.Ports;
using CabinaFletes.Domain.Costeo.Entidades;

namespace CabinaFletes.Application.ReglasSoloSicetac.Commands;

/// <summary>El Administrador de Fletes demarca una ruta + tipo de vehículo como "solo SICE-TAC".</summary>
public sealed record CrearReglaSoloSicetacCommand(string Origen, string Destino, string TipoVehiculo) : ICommand<ReglaSoloSicetacDto>;

public sealed class CrearReglaSoloSicetacCommandHandler(
    IReglasSoloSicetacRepository reglas, IUnitOfWork uow)
    : ICommandHandler<CrearReglaSoloSicetacCommand, ReglaSoloSicetacDto>
{
    public async Task<ReglaSoloSicetacDto> Handle(CrearReglaSoloSicetacCommand command, CancellationToken ct = default)
    {
        var origen = ReglaSoloSicetacTexto.Normalizar(command.Origen);
        var destino = ReglaSoloSicetacTexto.Normalizar(command.Destino);
        var vehiculo = ReglaSoloSicetacTexto.Normalizar(command.TipoVehiculo);
        if (origen.Length == 0 || destino.Length == 0 || vehiculo.Length == 0)
            throw new ValidationFailedException("REGLA_INVALIDA", "Origen, destino y tipo de vehículo son obligatorios.");

        if (await reglas.ExisteAsync(origen, destino, vehiculo, ct))
            throw new ConflictException("REGLA_DUPLICADA", "Ya existe una regla 'solo SICE' para esa ruta y tipo de vehículo.");

        var regla = new Domain.Costeo.Entidades.ReglaSoloSicetac { Origen = origen, Destino = destino, TipoVehiculo = vehiculo };
        reglas.Agregar(regla);
        await uow.SaveChangesAsync(ct);
        return new ReglaSoloSicetacDto(regla.Id, regla.Origen, regla.Destino, regla.TipoVehiculo, regla.CreatedAt);
    }
}
