using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Validadores;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Costeo.Entidades;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>El Administrador de Fletes demarca una ruta + tipo de vehículo como "solo SICE-TAC".</summary>
public sealed record CrearReglaSoloSicetacCommand(string Origen, string Destino, string TipoVehiculo) : ICommand<ReglaSoloSicetacDto>;

public sealed class CrearReglaSoloSicetacCommandHandler(
    IReglasSoloSicetacRepository reglas, IUnitOfWork uow)
    : ICommandHandler<CrearReglaSoloSicetacCommand, ReglaSoloSicetacDto>
{
    public async Task<ReglaSoloSicetacDto> Handle(CrearReglaSoloSicetacCommand command, CancellationToken ct = default)
    {
        CrearReglaSoloSicetacValidador.Validar(command);

        var origen = ReglaSoloSicetacTexto.Normalizar(command.Origen);
        var destino = ReglaSoloSicetacTexto.Normalizar(command.Destino);
        var vehiculo = ReglaSoloSicetacTexto.Normalizar(command.TipoVehiculo);

        if (await reglas.ExisteAsync(origen, destino, vehiculo, ct))
            throw new ConflictException("REGLA_DUPLICADA", "Ya existe una regla 'solo SICE' para esa ruta y tipo de vehículo.");

        var regla = new Domain.Costeo.Entidades.ReglaSoloSicetac { Origen = origen, Destino = destino, TipoVehiculo = vehiculo };
        reglas.Agregar(regla);
        await uow.SaveChangesAsync(ct);
        return new ReglaSoloSicetacDto(regla.Id, regla.Origen, regla.Destino, regla.TipoVehiculo, regla.CreatedAt);
    }
}
