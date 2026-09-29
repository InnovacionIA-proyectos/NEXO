using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.Validadores;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

/// <summary>
/// Edita una tarifa de plaza existente. El UPDATE de Valor/Vigencia/DetallesTarifa dispara
/// el trigger de BD TRG_tarifas_plaza_historico, que archiva el valor anterior en
/// tbl_tarifas_plaza_historico — no hay nada especial que hacer acá para eso.
/// </summary>
public sealed record clsEditarTarifaPlazaCommand(
    Guid Id, string LineaNegocio, string ClaseViaje, int OrigenMunicipioId, int DestinoMunicipioId,
    string? ClaseContenedor, int? PesoId, string? TipoVehiculo,
    decimal Valor, DateOnly Vigencia, string DetallesTarifa) : ICommand<clsTarifaPlazaDto>;

public sealed class clsEditarTarifaPlazaCommandHandler(
    ITarifasPlazaRepository tarifas, IMunicipiosRepository municipios, IRangosPesoRepository rangos,
    ILineasNegocioPort lineasNegocio, IClasesViajePort clasesViaje, IUnitOfWork uow)
    : ICommandHandler<clsEditarTarifaPlazaCommand, clsTarifaPlazaDto>
{
    public async Task<clsTarifaPlazaDto> Handle(clsEditarTarifaPlazaCommand command, CancellationToken ct = default)
    {
        clsTarifaPlazaValidador.Validar(
            command.LineaNegocio, command.ClaseViaje, command.ClaseContenedor,
            command.PesoId, command.TipoVehiculo, command.Valor, command.DetallesTarifa);

        var tarifa = await tarifas.ObtenerAsync(command.Id, ct)
            ?? throw new clsNotFoundException("TARIFA_PLAZA_NO_ENCONTRADA", "No existe una tarifa de plaza con ese identificador.");

        var (lineaNegocio, claseViaje) = await clsResolucionDeTarifaPlaza.ResolverCatalogosAsync(
            command.LineaNegocio, command.ClaseViaje, lineasNegocio, clasesViaje, ct);

        var catalogoMunicipios = await municipios.ListarAsync(ct);
        var origen = catalogoMunicipios.FirstOrDefault(m => m.Id == command.OrigenMunicipioId)
            ?? throw new clsValidationFailedException("MUNICIPIO_INVALIDO", "El municipio de origen no existe.");
        var destino = catalogoMunicipios.FirstOrDefault(m => m.Id == command.DestinoMunicipioId)
            ?? throw new clsValidationFailedException("MUNICIPIO_INVALIDO", "El municipio de destino no existe.");

        clsRangoPeso? peso = null;
        if (command.PesoId is { } pesoId)
            peso = await rangos.ObtenerAsync(pesoId, ct)
                ?? throw new clsValidationFailedException("RANGO_PESO_INVALIDO", "El rango de peso indicado no existe.");

        if (await tarifas.ExisteAsync(lineaNegocio, claseViaje, command.OrigenMunicipioId, command.DestinoMunicipioId,
                command.ClaseContenedor, command.PesoId, command.TipoVehiculo, command.Id, ct))
            throw new clsConflictException("TARIFA_PLAZA_DUPLICADA", "Ya existe otra tarifa de plaza para esa combinación exacta.");

        tarifa.LineaNegocio = lineaNegocio;
        tarifa.ClaseViaje = claseViaje;
        tarifa.OrigenMunicipioId = command.OrigenMunicipioId;
        tarifa.DestinoMunicipioId = command.DestinoMunicipioId;
        tarifa.ClaseContenedor = command.ClaseContenedor;
        tarifa.PesoId = command.PesoId;
        tarifa.TipoVehiculo = command.TipoVehiculo;
        tarifa.Valor = command.Valor;
        tarifa.Vigencia = command.Vigencia;
        tarifa.DetallesTarifa = command.DetallesTarifa;
        await uow.SaveChangesAsync(ct);

        tarifa.Origen = origen;
        tarifa.Destino = destino;
        tarifa.Peso = peso;
        return tarifa.ToDto();
    }
}
