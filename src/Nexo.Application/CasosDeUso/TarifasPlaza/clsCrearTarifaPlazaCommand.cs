using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.CasosDeUso.TarifasPlaza.Interfaces;
using Nexo.Application.CasosDeUso.TarifasPlaza.Validadores;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Costeo.Entidades;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

public sealed record clsCrearTarifaPlazaCommand(
    string LineaNegocio, string ClaseViaje, int OrigenMunicipioId, int DestinoMunicipioId,
    string? ClaseContenedor, int? PesoId, string? TipoVehiculo,
    decimal Valor, DateOnly Vigencia, string DetallesTarifa) : ICommand<clsTarifaPlazaDto>;

public sealed class clsCrearTarifaPlazaCommandHandler(
    ITarifasPlazaRepository tarifas, IMunicipiosRepository municipios, IRangosPesoRepository rangos,
    ILineasNegocioPort lineasNegocio, IClasesViajePort clasesViaje,
    ICurrentUserPort usuarioActual, IUnitOfWork uow)
    : ICommandHandler<clsCrearTarifaPlazaCommand, clsTarifaPlazaDto>
{
    public async Task<clsTarifaPlazaDto> Handle(clsCrearTarifaPlazaCommand command, CancellationToken ct = default)
    {
        clsTarifaPlazaValidador.Validar(
            command.LineaNegocio, command.ClaseViaje, command.ClaseContenedor,
            command.PesoId, command.TipoVehiculo, command.Valor, command.DetallesTarifa);

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
                command.ClaseContenedor, command.PesoId, command.TipoVehiculo, null, ct))
            throw new clsConflictException("TARIFA_PLAZA_DUPLICADA", "Ya existe una tarifa de plaza para esa combinación exacta.");

        var tarifa = new clsTarifaPlaza
        {
            LineaNegocio = lineaNegocio,
            ClaseViaje = claseViaje,
            OrigenMunicipioId = command.OrigenMunicipioId,
            DestinoMunicipioId = command.DestinoMunicipioId,
            ClaseContenedor = command.ClaseContenedor,
            PesoId = command.PesoId,
            TipoVehiculo = command.TipoVehiculo,
            Valor = command.Valor,
            Vigencia = command.Vigencia,
            DetallesTarifa = command.DetallesTarifa,
            CreadoPor = usuarioActual.Id,
        };
        tarifas.Agregar(tarifa);
        await uow.SaveChangesAsync(ct);

        tarifa.Origen = origen;
        tarifa.Destino = destino;
        tarifa.Peso = peso;
        return tarifa.ToDto();
    }
}
