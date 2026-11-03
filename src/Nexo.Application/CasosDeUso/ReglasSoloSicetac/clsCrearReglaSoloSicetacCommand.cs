using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Validadores;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>El Administrador de Fletes demarca una ruta + tipo de vehículo como "solo SICE-TAC".</summary>
public sealed record clsCrearReglaSoloSicetacCommand(string Origen, string Destino, string TipoVehiculo) : ICommand<clsReglaSoloSicetacDto>;

public sealed class clsCrearReglaSoloSicetacCommandHandler(
    IReglasSoloSicetacRepository reglas, IMunicipiosRepository municipios, IUnitOfWork uow)
    : ICommandHandler<clsCrearReglaSoloSicetacCommand, clsReglaSoloSicetacDto>
{
    public async Task<clsReglaSoloSicetacDto> Handle(clsCrearReglaSoloSicetacCommand command, CancellationToken ct = default)
    {
        clsCrearReglaSoloSicetacValidador.Validar(command);

        var origen = clsReglaSoloSicetacTexto.Normalizar(command.Origen);
        var destino = clsReglaSoloSicetacTexto.Normalizar(command.Destino);
        var vehiculo = clsReglaSoloSicetacTexto.Normalizar(command.TipoVehiculo);

        if (await reglas.ExisteAsync(origen, destino, vehiculo, ct))
            throw new clsConflictException("REGLA_DUPLICADA", "Ya existe una regla 'solo SICE' para esa ruta y tipo de vehículo.");

        var regla = new Domain.Costeo.Entidades.clsReglaSoloSicetac { Origen = origen, Destino = destino, TipoVehiculo = vehiculo };
        reglas.Agregar(regla);
        await uow.SaveChangesAsync(ct);

        // Mismo criterio de resolución que el listado (#242-adjacent): Origen/Destino
        // suelen ser código DANE, se resuelve a nombre de municipio para la respuesta.
        var catalogo = await municipios.ListarAsync(ct);
        var porCodigoDane = catalogo
            .GroupBy(m => m.CodigoDane)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault(m => m.Pais == "CO") ?? g.First());
        string? NombreMunicipio(string codigoCrudo) =>
            porCodigoDane.TryGetValue(codigoCrudo, out var m) ? $"{m.Municipio}, {m.Departamento}" : null;

        return new clsReglaSoloSicetacDto(
            regla.Id, regla.Origen, regla.Destino, NombreMunicipio(regla.Origen), NombreMunicipio(regla.Destino),
            regla.TipoVehiculo, regla.CreatedAt);
    }
}
